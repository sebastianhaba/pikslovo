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
using GameTranslator.Services;
using Java.Interop;

namespace GameTranslator.Droid.Services;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMediaProjection)]
public sealed class TranslationForegroundService : Service
{
    public const string StartSessionAction = "com.gametranslator.action.START_SESSION";
    public const string CaptureAndTranslateAction = "com.gametranslator.action.CAPTURE_AND_TRANSLATE";
    public const string DismissOverlayAction = "com.gametranslator.action.DISMISS_OVERLAY";
    public const string StopSessionAction = "com.gametranslator.action.STOP_SESSION";
    public const string RefreshAppearanceAction = "com.gametranslator.action.REFRESH_APPEARANCE";
    public const string ProjectionResultCodeExtra = "projection_result_code";
    public const string ProjectionResultDataExtra = "projection_result_data";

    private const int NotificationId = 1001;
    private const string NotificationChannelId = "translation_session";
    private readonly object _stateLock = new();
    private MediaProjection? _mediaProjection;
    private VirtualDisplay? _virtualDisplay;
    private ImageReader? _imageReader;
    private AndroidOverlayPresenter? _overlayPresenter;
    private FloatingTranslationTrigger? _floatingTrigger;
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
            Android.Util.Log.Error("GameTranslator", exception.ToString());
            ShowMessage($"Nie udało się uruchomić sesji: {exception.Message}");
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
            throw new InvalidOperationException("Nie można odczytać rozmiaru ekranu.");
        }

        // ImageFormat.RGBA_8888 is represented as value 1 by the Android API.
        _imageReader = ImageReader.NewInstance(width, height, (ImageFormatType)1, 2);
        _virtualDisplay = _mediaProjection.CreateVirtualDisplay(
            "GameTranslatorCapture",
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

        _floatingTrigger.Show(() => _ = CaptureAndTranslateAsync(), shouldShowButton);
    }

    private async Task CaptureAndTranslateAsync()
    {
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
            _floatingTrigger?.SetState(FloatingTranslationTriggerState.Processing);
        }

        var resultShown = false;
        try
        {
            if (!Settings.CanDrawOverlays(this))
            {
                ShowMessage("Przyznaj uprawnienie do wyświetlania nad innymi aplikacjami.");
                return;
            }

            var bitmap = await CaptureBitmapAsync().ConfigureAwait(false);
            if (bitmap is null)
            {
                ShowMessage("Nie udało się pobrać klatki ekranu.");
                return;
            }

            var processingAccent = global::GameTranslator.App.GetAccentColor(AndroidSettingsStore.Load(this).Accent);
            new Handler(Looper.MainLooper!).Post(() =>
                _overlayPresenter?.ShowProcessingFrame(Color.Rgb(processingAccent.R, processingAccent.G, processingAccent.B)));
            _floatingTrigger?.ShowAfterCapture();

            using (bitmap)
            using (var stream = new MemoryStream())
            {
                bitmap.Compress(Bitmap.CompressFormat.Png!, 100, stream);
                var appSettings = AndroidSettingsStore.Load(this);
                var settings = appSettings.Translation;
                var result = await AppServices.TranslationOrchestrator
                    .TranslateAsync(stream.ToArray(), settings, CancellationToken.None)
                    .ConfigureAwait(false);
                if (result is null)
                {
                    return;
                }

                if (result.Regions.Count == 0)
                {
                    ShowMessage("Nie znaleziono tekstu na ekranie.");
                    return;
                }

                var accent = global::GameTranslator.App.GetAccentColor(appSettings.Accent);
                var overlay = AndroidOverlayRenderer.Render(
                    bitmap,
                    result,
                    settings.FontScale,
                    Color.Rgb(accent.R, accent.G, accent.B));
                resultShown = true;
                new Handler(Looper.MainLooper!).Post(() =>
                {
                    _overlayPresenter?.Show(overlay, DismissOverlay);
                    _floatingTrigger?.BringToFront();
                    _floatingTrigger?.SetState(FloatingTranslationTriggerState.ResultVisible);
                });
            }
        }
        catch (Exception exception)
        {
            ShowMessage(exception.Message);
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

    private async Task<Bitmap?> CaptureBitmapAsync()
    {
        await Task.Delay(100).ConfigureAwait(false);
        if (_floatingTrigger is not null)
        {
            await _floatingTrigger.HideForCaptureAsync().ConfigureAwait(false);
            await Task.Delay(32).ConfigureAwait(false);
        }

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
        lock (_stateLock)
        {
            IsSessionActive = false;
            _isProcessing = false;
        }
        AndroidTranslationHost.NotifySessionStateChanged();

        DismissOverlay();
        _floatingTrigger?.Dismiss();
        _floatingTrigger = null;
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
            "Aktywna sesja tłumacza",
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
            .SetContentTitle("GameTranslator jest aktywny")
            .SetContentText("Hotkey i przycisk pływający są gotowe.")
            .SetSmallIcon(Resource.Mipmap.icon)
            .SetOngoing(true)
            .AddAction(new Notification.Action.Builder(null, "Zatrzymaj", pendingIntent).Build())
            .Build();
    }

    private void ShowMessage(string message)
    {
        new Handler(Looper.MainLooper!).Post(() => Toast.MakeText(this, message, ToastLength.Long)?.Show());
    }

    private sealed class ProjectionCallback(TranslationForegroundService service) : MediaProjection.Callback
    {
        public override void OnStop()
        {
            service.StopSession();
        }
    }
}
