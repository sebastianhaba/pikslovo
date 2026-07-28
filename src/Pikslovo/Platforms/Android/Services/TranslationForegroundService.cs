using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media.Projection;
using Android.OS;
using Android.Widget;
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
    private const string NotificationChannelId = "translation_session";
    private AndroidServiceSettingsAdapter? _settingsAdapter;
    private TranslationSessionCoordinator? _sessionCoordinator;
    private TranslationOverlayCoordinator? _overlayCoordinator;
    private TranslationCapturePipeline? _capturePipeline;
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
                    _overlayCoordinator?.DismissOverlay();
                    break;
                case StopSessionAction:
                    StopSession();
                    break;
                case RefreshAppearanceAction:
                    _overlayCoordinator?.RefreshAppearance();
                    RefreshNotification();
                    break;
            }
        }
        catch (Java.Lang.SecurityException)
        {
            ShowMessage(AppStrings.Keys.ScreenCaptureConsentExpired);
            StopSession();
        }
        catch (Exception exception)
        {
            Android.Util.Log.Error("Pikslovo", exception.ToString());
            ShowMessage(AppStrings.Format(AppStrings.Keys.CouldNotStartSession, exception.Message));
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
        if (IsSessionActive)
        {
            return;
        }

        CreateNotificationChannel();
        StartForeground(NotificationId, BuildNotification(), ForegroundService.TypeMediaProjection);

        var resultCode = (Result)intent.GetIntExtra(ProjectionResultCodeExtra, (int)Result.Canceled);
        var resultData = GetProjectionResultData(intent);
        if (resultCode != Result.Ok || resultData is null)
        {
            ShowMessage(AppStrings.Keys.ScreenCaptureConsentDenied);
            StopSession();
            return;
        }

        _settingsAdapter ??= new AndroidServiceSettingsAdapter(this);
        _overlayCoordinator ??= new TranslationOverlayCoordinator(
            this,
            _settingsAdapter,
            onCaptureRequested: () => _ = CaptureAndTranslateAsync(),
            isSessionActive: () => _sessionCoordinator?.IsActive == true,
            isProcessing: () => _sessionCoordinator?.IsProcessing == true,
            onStopSession: StopSession,
            showMessage: ShowMessage);
        _sessionCoordinator ??= new TranslationSessionCoordinator(this);
        _capturePipeline ??= new TranslationCapturePipeline(
            _settingsAdapter,
            _sessionCoordinator,
            _overlayCoordinator,
            ShowMessage);

        if (!_sessionCoordinator.Start(resultCode, resultData, StopSession))
        {
            ShowMessage(AppStrings.Keys.ScreenCaptureStartFailed);
            StopSession();
            return;
        }

        SetSessionActive(true);
        _overlayCoordinator.InitializeSessionUi();
        ShowMessage(AppStrings.Keys.TranslatorIsActive);
    }

    private async Task CaptureAndTranslateAsync()
    {
        if (_sessionCoordinator is null || _overlayCoordinator is null || _capturePipeline is null)
        {
            return;
        }

        if (_overlayCoordinator.IsOverlayVisible)
        {
            _overlayCoordinator.DismissOverlay();
            return;
        }

        if (!_sessionCoordinator.TryBeginProcessing(out var cancellationToken))
        {
            return;
        }

        _overlayCoordinator.SetTriggerState(FloatingTranslationTriggerState.Processing);
        var resultShown = false;
        try
        {
            resultShown = await _capturePipeline.ExecuteAsync(cancellationToken).ConfigureAwait(false);
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
            _sessionCoordinator.EndProcessing();
            if (!resultShown)
            {
                _overlayCoordinator.DismissOverlay();
            }
        }
    }

    private void StopSession()
    {
        if (_isStopping)
        {
            return;
        }

        _isStopping = true;
        try
        {
            SetSessionActive(false);
            _sessionCoordinator?.Stop();
            _overlayCoordinator?.DismissAll();
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
        }
        finally
        {
            _isStopping = false;
        }
    }

    private void SetSessionActive(bool isActive)
    {
        IsSessionActive = isActive;
        AndroidTranslationHost.NotifySessionStateChanged();
    }

    private static Intent? GetProjectionResultData(Intent intent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return intent.GetParcelableExtra(
                ProjectionResultDataExtra,
                Java.Lang.Class.FromType(typeof(Intent))) as Intent;
        }

        return intent.GetParcelableExtra(ProjectionResultDataExtra) as Intent;
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var channel = new NotificationChannel(
            NotificationChannelId,
            AppStrings.Get(AppStrings.Keys.ActiveTranslatorSession),
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
            .SetContentTitle(AppStrings.Get(AppStrings.Keys.PikslovoIsActive))
            .SetContentText(AppStrings.Get(AppStrings.Keys.HotkeyAndFloatingButtonReady))
            .SetSmallIcon(Resource.Drawable.ic_translate)
            .SetOngoing(true)
            .AddAction(new Notification.Action.Builder(null, AppStrings.Get(AppStrings.Keys.Stop), pendingIntent).Build())
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
}
