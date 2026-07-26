using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Display;
using Android.Media;
using Android.Media.Projection;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using System.Diagnostics;
using Pikslovo.Core;
using Pikslovo.Services;
using Java.Interop;

namespace Pikslovo.Droid.Services;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMediaProjection)]
public sealed class TranslationForegroundService : Service
{
    public const string StartSessionAction = "app.pikslovo.action.START_SESSION";
    public const string CaptureAndTranslateAction = "app.pikslovo.action.CAPTURE_AND_TRANSLATE";
    public const string DismissOverlayAction = "app.pikslovo.action.DISMISS_OVERLAY";
    public const string StopSessionAction = "app.pikslovo.action.STOP_SESSION";
    public const string RefreshAppearanceAction = "app.pikslovo.action.REFRESH_APPEARANCE";
    public const string ProjectionResultCodeExtra = "projection_result_code";
    public const string ProjectionResultDataExtra = "projection_result_data";

    private const int NotificationId = 1001;
    private const int CaptureSurfaceRefreshDelayMilliseconds = 200;
    private const string NotificationChannelId = "translation_session";
    private readonly object _stateLock = new();
    private MediaProjection? _mediaProjection;
    private VirtualDisplay? _virtualDisplay;
    private ImageReader? _imageReader;
    private AndroidOverlayPresenter? _overlayPresenter;
    private FloatingTranslationTrigger? _floatingTrigger;
    private CaptureRegionSelectorOverlay? _captureRegionSelector;
    private CancellationTokenSource? _sessionCancellation;
    private bool _isProcessing;
    private bool _isStopping;

    public static bool IsSessionActive { get; private set; }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        try
        {
            switch (intent?.Action)
            {
                case StartSessionAction:
                    StartSession(intent);
                    break;
                case CaptureAndTranslateAction:
                    _ = CaptureAndTranslateAsync();
                    break;
                case DismissOverlayAction:
                    DismissOverlay();
                    break;
                case StopSessionAction:
                    StopSession();
                    break;
                case RefreshAppearanceAction:
                    UpdateFloatingTriggerVisibility();
                    RefreshNotification();
                    break;
            }
        }
        catch (Java.Lang.SecurityException)
        {
            ShowMessage("Zgoda na nagrywanie ekranu wygasła. Uruchom tłumacza ponownie i zaakceptuj nowy monit.");
            StopSession();
        }
        catch (Exception exception)
        {
            Android.Util.Log.Error("Pikslovo", exception.ToString());
            ShowMessage(AppStrings.Format("Nie udało się uruchomić sesji: {0}", exception.Message));
            StopSession();
        }

        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        StopSession();
        base.OnDestroy();
    }

    private void StartSession(Intent intent)
    {
        // Android 14 permits one virtual display for each projection token.
        // A duplicated START_SESSION intent must not reuse the current token.
        if (IsSessionActive || _mediaProjection is not null)
        {
            return;
        }

        CreateNotificationChannel();
        StartForeground(NotificationId, BuildNotification(), ForegroundService.TypeMediaProjection);

        var resultCode = (Result)intent.GetIntExtra(ProjectionResultCodeExtra, (int)Result.Canceled);
        Intent? resultData;
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            resultData = intent.GetParcelableExtra(
                ProjectionResultDataExtra,
                Java.Lang.Class.FromType(typeof(Intent))) as Intent;
        }
        else
        {
            resultData = intent.GetParcelableExtra(ProjectionResultDataExtra) as Intent;
        }
        if (resultCode != Result.Ok || resultData is null)
        {
            ShowMessage("Nie udzielono zgody na przechwytywanie ekranu.");
            StopSession();
            return;
        }

        var manager = (MediaProjectionManager?)GetSystemService(MediaProjectionService);
        _mediaProjection = manager?.GetMediaProjection((int)resultCode, resultData);
        if (_mediaProjection is null)
        {
            ShowMessage("Nie udało się uruchomić przechwytywania ekranu.");
            StopSession();
            return;
        }

        _mediaProjection.RegisterCallback(new ProjectionCallback(this), new Handler(Looper.MainLooper!));
        CreateCaptureSurface();
        _sessionCancellation = new CancellationTokenSource();
        _overlayPresenter = new AndroidOverlayPresenter(this);
        IsSessionActive = true;
        UpdateFloatingTriggerVisibility();
        AndroidTranslationHost.NotifySessionStateChanged();
        ShowMessage("Tłumacz jest aktywny.");
    }

    private void CreateCaptureSurface()
    {
        var windowManager = GetSystemService(WindowService)?.JavaCast<IWindowManager>();
        var bounds = windowManager?.CurrentWindowMetrics?.Bounds;
        var width = bounds?.Width() ?? 0;
        var height = bounds?.Height() ?? 0;
        var density = Resources?.Configuration?.DensityDpi ?? 0;
        if (width <= 0 || height <= 0 || density <= 0 || _mediaProjection is null)
        {
            throw new InvalidOperationException(AppStrings.Get("Nie można odczytać rozmiaru ekranu."));
        }

        // ImageFormat.RGBA_8888 is represented as value 1 by the Android API.
        _imageReader = ImageReader.NewInstance(width, height, (ImageFormatType)1, 2);
        _virtualDisplay = _mediaProjection.CreateVirtualDisplay(
            "PikslovoCapture",
            width,
            height,
            (int)density,
            (DisplayFlags)(int)VirtualDisplayFlags.AutoMirror,
            _imageReader.Surface,
            null,
            null);
    }

    private void UpdateFloatingTriggerVisibility()
    {
        var settings = AndroidSettingsStore.Load(this);
        var shouldShowButton = settings.FloatingButton.AlwaysVisible || !settings.GlobalHotkeyEnabled;
        if (!Settings.CanDrawOverlays(this))
        {
            _floatingTrigger?.Dismiss();
            return;
        }

        _floatingTrigger ??= new FloatingTranslationTrigger(this);
        if (_floatingTrigger.IsAttached)
        {
            _floatingTrigger.RefreshConfiguration();
            _floatingTrigger.SetButtonVisibility(shouldShowButton);
            return;
        }

        _floatingTrigger.Show(
            () => _ = CaptureAndTranslateAsync(),
            ShowCaptureRegionSelector,
            StopSession,
            shouldShowButton);
    }

    private void ShowCaptureRegionSelector()
    {
        lock (_stateLock)
        {
            if (!IsSessionActive || _isProcessing || _captureRegionSelector?.IsShowing == true)
            {
                return;
            }
        }

        new Handler(Looper.MainLooper!).Post(() =>
        {
            _overlayPresenter?.Dismiss();
            _ = _floatingTrigger?.HideForCaptureAsync();

            _captureRegionSelector ??= new CaptureRegionSelectorOverlay(this);
            var initialRegion = AndroidSettingsStore.Load(this).CaptureRegion;
            _captureRegionSelector.Show(
                initialRegion,
                region => SaveCaptureRegion(region),
                RestoreFloatingTrigger);
        });
    }

    private void SaveCaptureRegion(CaptureRegionSettings region)
    {
        var settings = AndroidSettingsStore.Load(this);
        AndroidSettingsStore.Save(this, settings with { CaptureRegion = region.Normalize() });
        ShowMessage("Obszar dialogu zapisany.");
        RestoreFloatingTrigger();
    }

    private void RestoreFloatingTrigger()
    {
        _captureRegionSelector?.Dismiss();
        UpdateFloatingTriggerVisibility();
    }

    private async Task CaptureAndTranslateAsync()
    {
        var operationStopwatch = Stopwatch.StartNew();
        CancellationToken cancellationToken;
        lock (_stateLock)
        {
            if (!IsSessionActive || _isProcessing)
            {
                return;
            }

            if (_overlayPresenter?.IsShowing == true)
            {
                DismissOverlay();
                return;
            }

            _isProcessing = true;
            cancellationToken = _sessionCancellation?.Token ?? CancellationToken.None;
            _floatingTrigger?.SetState(FloatingTranslationTriggerState.Processing);
        }

        var resultShown = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Settings.CanDrawOverlays(this))
            {
                ShowMessage("Przyznaj uprawnienie do wyświetlania nad innymi aplikacjami.");
                return;
            }

            var bitmap = await CaptureBitmapAsync(cancellationToken).ConfigureAwait(false);
            if (bitmap is null)
            {
                ShowMessage("Nie udało się pobrać klatki ekranu.");
                return;
            }
            var processingAccent = global::Pikslovo.App.GetAccentColor(AndroidSettingsStore.Load(this).Accent);
            new Handler(Looper.MainLooper!).Post(() =>
                _overlayPresenter?.ShowProcessingFrame(Color.Rgb(processingAccent.R, processingAccent.G, processingAccent.B)));
            _floatingTrigger?.ShowAfterCapture();
            cancellationToken.ThrowIfCancellationRequested();

            using (bitmap)
            using (var stream = new MemoryStream())
            {
                var appSettings = AndroidSettingsStore.Load(this);
                var settings = appSettings.Translation;
                var cropBounds = appSettings.CaptureRegion.ToPixelRect(bitmap.Width, bitmap.Height);
                using var croppedBitmap = appSettings.CaptureRegion.IsEnabled
                    ? Bitmap.CreateBitmap(bitmap, cropBounds.Left, cropBounds.Top, cropBounds.Width, cropBounds.Height)
                    : null;
                var bitmapForOcr = croppedBitmap ?? bitmap;
                using var scaledBitmap = CreateScaledOcrBitmap(bitmapForOcr, settings.OcrImageScale);
                var bitmapForVision = scaledBitmap ?? bitmapForOcr;
                var encodingStopwatch = Stopwatch.StartNew();
                var imageFormat = settings.UseJpegForOcr ? Bitmap.CompressFormat.Jpeg! : Bitmap.CompressFormat.Png!;
                var imageQuality = settings.UseJpegForOcr ? settings.OcrJpegQuality : 100;
                if (!bitmapForVision.Compress(imageFormat, imageQuality, stream))
                {
                    throw new InvalidOperationException(AppStrings.Get("Nie udało się zakodować obrazu OCR."));
                }

                var imageBytes = stream.ToArray();
                var encodingMilliseconds = encodingStopwatch.ElapsedMilliseconds;
                var captureAndImageEncodingMilliseconds = operationStopwatch.ElapsedMilliseconds;
                var imageFormatName = settings.UseJpegForOcr ? $"JPEG {imageQuality}%" : "PNG";
                Android.Util.Log.Debug(
                    "Pikslovo",
                    $"Capture + encode: {captureAndImageEncodingMilliseconds} ms; {imageFormatName} encode: {encodingMilliseconds} ms; {bitmapForVision.Width}x{bitmapForVision.Height}; image={imageBytes.Length / 1024d:0.0} KiB");
                var execution = await AppServices.TranslationOrchestrator
                    .TranslateWithTimingsAsync(imageBytes, settings, cancellationToken)
                    .ConfigureAwait(false);
                var result = execution.Result;
                AppServices.Diagnostics.RecordTranslation(
                    captureAndImageEncodingMilliseconds,
                    encodingMilliseconds,
                    execution.CloudVisionOcrMilliseconds,
                    execution.CloudTranslationMilliseconds,
                    operationStopwatch.ElapsedMilliseconds);
                Android.Util.Log.Debug(
                    "Pikslovo",
                    $"Cloud Vision OCR: {execution.CloudVisionOcrMilliseconds} ms; Cloud Translation: {execution.CloudTranslationMilliseconds} ms; total={operationStopwatch.ElapsedMilliseconds} ms");
                if (result is null)
                {
                    return;
                }

                if (result.Regions.Count == 0)
                {
                    ShowMessage("Nie znaleziono tekstu na ekranie.");
                    return;
                }

                if (scaledBitmap is not null)
                {
                    result = ScaleRegions(
                        result,
                        bitmapForOcr.Width / (float)bitmapForVision.Width,
                        bitmapForOcr.Height / (float)bitmapForVision.Height);
                }

                if (appSettings.CaptureRegion.IsEnabled)
                {
                    result = OffsetRegions(result, cropBounds.Left, cropBounds.Top);
                }

                var accent = global::Pikslovo.App.GetAccentColor(appSettings.Accent);
                var overlay = AndroidOverlayRenderer.Render(
                    bitmap,
                    result,
                    settings.FontScale,
                    Color.Rgb(accent.R, accent.G, accent.B));
                cancellationToken.ThrowIfCancellationRequested();
                resultShown = true;
                new Handler(Looper.MainLooper!).Post(() =>
                {
                    if (cancellationToken.IsCancellationRequested || !IsSessionActive)
                    {
                        overlay.Dispose();
                        return;
                    }

                    _overlayPresenter?.Show(overlay, DismissOverlay);
                    _floatingTrigger?.BringToFront();
                    _floatingTrigger?.SetState(FloatingTranslationTriggerState.ResultVisible);
                });
            }
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Android.Util.Log.Error("Pikslovo", exception.ToString());
            ShowMessage(exception is HttpRequestException
                ? "Nie udało się połączyć z Google Cloud. Sprawdź połączenie z Internetem i spróbuj ponownie."
                : exception.Message);
        }
        finally
        {
            lock (_stateLock)
            {
                _isProcessing = false;
            }

            if (!resultShown)
            {
                DismissOverlay();
            }
        }
    }

    private static TranslationResult OffsetRegions(TranslationResult result, int offsetX, int offsetY) =>
        new(result.Regions.Select(region => new TranslatedRegion(
            region.SourceText,
            region.TranslatedText,
            new PixelRect(
                region.Bounds.Left + offsetX,
                region.Bounds.Top + offsetY,
                region.Bounds.Right + offsetX,
                region.Bounds.Bottom + offsetY))).ToArray());

    private static Bitmap? CreateScaledOcrBitmap(Bitmap bitmap, float scale)
    {
        if (scale >= 1f)
        {
            return null;
        }

        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        return Bitmap.CreateScaledBitmap(bitmap, width, height, filter: false);
    }

    private static TranslationResult ScaleRegions(TranslationResult result, float scaleX, float scaleY) =>
        new(result.Regions.Select(region => new TranslatedRegion(
            region.SourceText,
            region.TranslatedText,
            new PixelRect(
                (int)Math.Round(region.Bounds.Left * scaleX),
                (int)Math.Round(region.Bounds.Top * scaleY),
                Math.Max((int)Math.Round(region.Bounds.Left * scaleX) + 1, (int)Math.Round(region.Bounds.Right * scaleX)),
                Math.Max((int)Math.Round(region.Bounds.Top * scaleY) + 1, (int)Math.Round(region.Bounds.Bottom * scaleY))))).ToArray());

    private async Task<Bitmap?> CaptureBitmapAsync(CancellationToken cancellationToken)
    {
        if (_floatingTrigger is not null)
        {
            await _floatingTrigger.HideForCaptureAsync().ConfigureAwait(false);
        }

        // Let the virtual display receive a frame after every app-owned overlay is gone.
        await Task.Delay(CaptureSurfaceRefreshDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var image = _imageReader?.AcquireLatestImage();
        if (image is null)
        {
            return null;
        }

        try
        {
            var planes = image.GetPlanes();
            if (planes is null)
            {
                return null;
            }

            var plane = planes.FirstOrDefault();
            if (plane is null)
            {
                return null;
            }

            var rowPadding = plane.RowStride - (plane.PixelStride * image.Width);
            using var paddedBitmap = Bitmap.CreateBitmap(
                image.Width + (rowPadding / plane.PixelStride),
                image.Height,
                Bitmap.Config.Argb8888!);
            var buffer = plane.Buffer;
            if (buffer is null)
            {
                return null;
            }

            paddedBitmap.CopyPixelsFromBuffer(buffer);
            return Bitmap.CreateBitmap(paddedBitmap, 0, 0, image.Width, image.Height);
        }
        finally
        {
            image.Close();
            image.Dispose();
        }
    }

    private void DismissOverlay()
    {
        new Handler(Looper.MainLooper!).Post(() =>
        {
            _overlayPresenter?.Dismiss();
            _floatingTrigger?.SetState(FloatingTranslationTriggerState.Ready);
        });
    }

    private void StopSession()
    {
        if (_isStopping)
        {
            return;
        }

        _isStopping = true;
        CancellationTokenSource? sessionCancellation;
        lock (_stateLock)
        {
            IsSessionActive = false;
            _isProcessing = false;
            sessionCancellation = _sessionCancellation;
            _sessionCancellation = null;
        }
        sessionCancellation?.Cancel();
        AndroidTranslationHost.NotifySessionStateChanged();

        DismissOverlay();
        _floatingTrigger?.Dismiss();
        _floatingTrigger = null;
        _captureRegionSelector?.Dismiss();
        _captureRegionSelector = null;
        _virtualDisplay?.Release();
        _virtualDisplay?.Dispose();
        _virtualDisplay = null;
        _imageReader?.Close();
        _imageReader?.Dispose();
        _imageReader = null;
        try
        {
            _mediaProjection?.Stop();
            _mediaProjection?.Dispose();
            _mediaProjection = null;
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
        }
        finally
        {
            _isStopping = false;
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var channel = new NotificationChannel(
            NotificationChannelId,
            AppStrings.Get("Aktywna sesja tłumacza"),
            NotificationImportance.Low);
        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        var stopIntent = new Intent(this, typeof(TranslationForegroundService));
        stopIntent.SetAction(StopSessionAction);
        var pendingIntent = PendingIntent.GetService(
            this,
            0,
            stopIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new Notification.Builder(this, NotificationChannelId)
            .SetContentTitle(AppStrings.Get("Pikslovo jest aktywne"))
            .SetContentText(AppStrings.Get("Hotkey i przycisk pływający są gotowe."))
            .SetSmallIcon(Resource.Mipmap.icon)
            .SetOngoing(true)
            .AddAction(new Notification.Action.Builder(null, AppStrings.Get("Zatrzymaj"), pendingIntent).Build())
            .Build();
    }

    private void RefreshNotification()
    {
        if (!IsSessionActive)
        {
            return;
        }

        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.Notify(NotificationId, BuildNotification());
    }

    private void ShowMessage(string message)
    {
        new Handler(Looper.MainLooper!).Post(() => Toast.MakeText(this, AppStrings.Get(message), ToastLength.Long)?.Show());
    }

    private sealed class ProjectionCallback(TranslationForegroundService service) : MediaProjection.Callback
    {
        public override void OnStop()
        {
            service.StopSession();
        }
    }
}
