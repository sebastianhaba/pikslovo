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
[IntentFilter(["android.accessibilityservice.AccessibilityService"])]
[MetaData("android.accessibilityservice", Resource = "@xml/global_hotkey_accessibility_service")]
public sealed class GlobalHotkeyAccessibilityService : AccessibilityService
{
    private readonly HashSet<int> _heldHotkeyCodes = [];
    private bool _hotkeyTriggered;

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();

        // The manifest makes the service discoverable; applying this at runtime
        // ensures Android forwards hardware key events after the user enables it.
        if (ServiceInfo is { } serviceInfo)
        {
            serviceInfo.Flags |= AccessibilityServiceFlags.RequestFilterKeyEvents;
            SetServiceInfo(serviceInfo);
        }
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
    }

    protected override bool OnKeyEvent(KeyEvent? e)
    {
        if (e is null || !TranslationForegroundService.IsSessionActive)
        {
            _heldHotkeyCodes.Clear();
            _hotkeyTriggered = false;
            return base.OnKeyEvent(e);
        }

        var settings = AndroidSettingsStore.Load(this);
        var hotkeyCodes = settings.HotkeyCodes;
        var keyCode = (int)e.KeyCode;
        if (!settings.GlobalHotkeyEnabled || hotkeyCodes.Length == 0 || !hotkeyCodes.Contains(keyCode))
        {
            return base.OnKeyEvent(e);
        }

        if (e.Action == KeyEventActions.Down && e.RepeatCount == 0)
        {
            _heldHotkeyCodes.Add(keyCode);
            if (!_hotkeyTriggered && hotkeyCodes.All(_heldHotkeyCodes.Contains))
            {
                _hotkeyTriggered = true;
                SendServiceAction(TranslationForegroundService.CaptureAndTranslateAction);
            }

            return true;
        }

        if (e.Action == KeyEventActions.Up)
        {
            _heldHotkeyCodes.Remove(keyCode);
            if (!hotkeyCodes.All(_heldHotkeyCodes.Contains))
            {
                _hotkeyTriggered = false;
            }

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
