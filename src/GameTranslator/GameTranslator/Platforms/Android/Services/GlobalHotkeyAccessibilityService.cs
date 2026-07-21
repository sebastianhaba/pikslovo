using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.Views;
using Android.Views.Accessibility;

namespace GameTranslator.Droid.Services;

[Service(
    Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE",
    Exported = true,
    Label = "GameTranslator hotkey")]
[MetaData("android.accessibilityservice", Resource = "@xml/global_hotkey_accessibility_service")]
public sealed class GlobalHotkeyAccessibilityService : AccessibilityService
{
    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        ServiceInfo!.Flags |= AccessibilityServiceFlags.RequestFilterKeyEvents;
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
    }

    protected override bool OnKeyEvent(KeyEvent? e)
    {
        if (e is null || !TranslationForegroundService.IsSessionActive)
        {
            return base.OnKeyEvent(e);
        }

        var settings = AndroidSettingsStore.Load(this);
        if (!settings.GlobalHotkeyEnabled || settings.HotkeyCode == 0 || e.KeyCode != (Keycode)settings.HotkeyCode)
        {
            return base.OnKeyEvent(e);
        }

        if (e.Action == KeyEventActions.Down && e.RepeatCount == 0)
        {
            SendServiceAction(TranslationForegroundService.CaptureAndTranslateAction);
            return true;
        }

        if (e.Action == KeyEventActions.Up && settings.HoldToPreview)
        {
            SendServiceAction(TranslationForegroundService.DismissOverlayAction);
            return true;
        }

        return true;
    }

    public override void OnInterrupt()
    {
    }

    private void SendServiceAction(string action)
    {
        var intent = new Intent(this, typeof(TranslationForegroundService));
        intent.SetAction(action);
        StartService(intent);
    }
}
