using Android.Content;
using Android.Graphics;
using Android.OS;

namespace Pikslovo.Droid.Services;

internal sealed class TranslationOverlayCoordinator(
    Context context,
    AndroidServiceSettingsAdapter settingsAdapter,
    Action onCaptureRequested,
    Func<bool> isSessionActive,
    Func<bool> isProcessing,
    Action onStopSession,
    Action<string> showMessage)
{
    private readonly Handler _mainHandler = new(Looper.MainLooper!);
    private AndroidOverlayPresenter? _overlayPresenter;
    private FloatingTranslationTrigger? _floatingTrigger;
    private CaptureRegionSelectorOverlay? _captureRegionSelector;

    public bool IsOverlayVisible => _overlayPresenter?.IsShowing == true;

    public void InitializeSessionUi() => UpdateFloatingTriggerVisibility();

    public void RefreshAppearance() => UpdateFloatingTriggerVisibility();

    public void SetTriggerState(FloatingTranslationTriggerState state)
    {
        _floatingTrigger?.SetState(state);
    }

    public Task PrepareForCaptureAsync() => _floatingTrigger?.HideForCaptureAsync() ?? Task.CompletedTask;

    public void ShowProcessingFrame(Color borderColor)
    {
        EnsureOverlayPresenter();
        _mainHandler.Post(() => _overlayPresenter?.ShowProcessingFrame(borderColor));
    }

    public void ShowTriggerAfterCapture()
    {
        _floatingTrigger?.ShowAfterCapture();
    }

    public void ShowResult(Bitmap bitmap, CancellationToken cancellationToken, Func<bool> shouldStillShow)
    {
        EnsureOverlayPresenter();
        _mainHandler.Post(() =>
        {
            if (cancellationToken.IsCancellationRequested || !shouldStillShow())
            {
                bitmap.Dispose();
                return;
            }

            _overlayPresenter?.Show(bitmap, DismissOverlay);
            _floatingTrigger?.BringToFront();
            _floatingTrigger?.SetState(FloatingTranslationTriggerState.ResultVisible);
        });
    }

    public void DismissOverlay()
    {
        _mainHandler.Post(() =>
        {
            _overlayPresenter?.Dismiss();
            _floatingTrigger?.SetState(FloatingTranslationTriggerState.Ready);
        });
    }

    public void DismissAll()
    {
        DismissOverlay();
        _floatingTrigger?.Dismiss();
        _floatingTrigger = null;
        _captureRegionSelector?.Dismiss();
        _captureRegionSelector = null;
    }

    private void UpdateFloatingTriggerVisibility()
    {
        var settings = settingsAdapter.Load();
        var shouldShowButton = settings.FloatingButton.AlwaysVisible || !settings.GlobalHotkeyEnabled;
        if (!settingsAdapter.CanDrawOverlays())
        {
            _floatingTrigger?.Dismiss();
            return;
        }

        _floatingTrigger ??= new FloatingTranslationTrigger(context);
        if (_floatingTrigger.IsAttached)
        {
            _floatingTrigger.RefreshConfiguration();
            _floatingTrigger.SetButtonVisibility(shouldShowButton);
            return;
        }

        _floatingTrigger.Show(
            onCaptureRequested,
            ShowCaptureRegionSelector,
            onStopSession,
            shouldShowButton);
    }

    private void ShowCaptureRegionSelector()
    {
        if (!isSessionActive() || isProcessing() || _captureRegionSelector?.IsShowing == true)
        {
            return;
        }

        _mainHandler.Post(() =>
        {
            _overlayPresenter?.Dismiss();
            _ = _floatingTrigger?.HideForCaptureAsync();

            _captureRegionSelector ??= new CaptureRegionSelectorOverlay(context);
            var initialRegion = settingsAdapter.Load().CaptureRegion;
            _captureRegionSelector.Show(
                initialRegion,
                SaveCaptureRegion,
                RestoreFloatingTrigger);
        });
    }

    private void SaveCaptureRegion(CaptureRegionSettings region)
    {
        var settings = settingsAdapter.Load();
        settingsAdapter.Save(settings with { CaptureRegion = region.Normalize() });
        showMessage(AppStrings.Keys.DialogRegionSaved);
        RestoreFloatingTrigger();
    }

    private void RestoreFloatingTrigger()
    {
        _captureRegionSelector?.Dismiss();
        UpdateFloatingTriggerVisibility();
    }

    private void EnsureOverlayPresenter()
    {
        _overlayPresenter ??= new AndroidOverlayPresenter(context);
    }
}
