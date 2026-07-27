#if __ANDROID__
using Pikslovo.Droid.Services;
using global::Android.App;
#endif

namespace Pikslovo;

internal sealed class MainPagePermissionsService
{
#if __ANDROID__
    public bool RequestOverlayPermission(Activity activity, out string statusMessage)
    {
        var alreadyAllowed = AndroidTranslationHost.RequestOverlayPermission(activity);
        statusMessage = alreadyAllowed
            ? AppStrings.Keys.OverlayPermissionIsAlreadyGranted
            : AppStrings.Keys.OpeningAndroidOverlaySettings;
        return alreadyAllowed;
    }

    public bool RequestNotificationPermission(Activity activity, out string statusMessage)
    {
        var alreadyAllowed = AndroidTranslationHost.RequestNotificationPermission(activity);
        statusMessage = alreadyAllowed
            ? AppStrings.Keys.NotificationPermissionIsAlreadyGranted
            : AppStrings.Keys.ShowingAndroidNotificationPermissionRequest;
        return alreadyAllowed;
    }

    public void OpenAccessibilitySettings(Activity activity) =>
        AndroidTranslationHost.OpenAccessibilitySettings(activity);

    public bool CanDrawOverlays() =>
        global::Android.Provider.Settings.CanDrawOverlays(global::Android.App.Application.Context!);
#else
    public bool CanDrawOverlays() => false;
#endif
}
