#if __ANDROID__
using Pikslovo.Droid.Services;
using global::Android.AccessibilityServices;
using global::Android.App;
using global::Android.Content;
using global::Android.Views.Accessibility;
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

    public bool RequestAccessibilityPermission(Activity activity, out string statusMessage)
    {
        var alreadyAllowed = HasAccessibilityPermission();
        statusMessage = alreadyAllowed
            ? AppStrings.Keys.AccessibilityPermissionIsAlreadyGranted
            : AppStrings.Keys.OpeningAccessibilitySettings;
        if (!alreadyAllowed)
        {
            AndroidTranslationHost.OpenAccessibilitySettings(activity);
        }

        return alreadyAllowed;
    }

    public bool HasAccessibilityPermission()
    {
        var context = global::Android.App.Application.Context!;
        var accessibilityManager = context.GetSystemService(Context.AccessibilityService) as AccessibilityManager;
        if (accessibilityManager is null || !accessibilityManager.IsEnabled)
        {
            return false;
        }

        var enabledServices = accessibilityManager
            .GetEnabledAccessibilityServiceList(FeedbackFlags.AllMask)?
            .Select(service => service.ResolveInfo?.ServiceInfo)
            .Where(serviceInfo => serviceInfo is not null)
            .ToArray();
        if (enabledServices is null || enabledServices.Length == 0)
        {
            return HasAccessibilityPermissionFromSecureSettings(context);
        }

        return enabledServices.Any(serviceInfo =>
            string.Equals(serviceInfo!.PackageName, context.PackageName, StringComparison.OrdinalIgnoreCase));
    }

    public bool CanDrawOverlays() =>
        global::Android.Provider.Settings.CanDrawOverlays(global::Android.App.Application.Context!);

    private static bool HasAccessibilityPermissionFromSecureSettings(Context context)
    {
        var accessibilityEnabled = global::Android.Provider.Settings.Secure.GetInt(
            context.ContentResolver,
            global::Android.Provider.Settings.Secure.AccessibilityEnabled,
            0) == 1;
        if (!accessibilityEnabled)
        {
            return false;
        }

        var enabledServices = global::Android.Provider.Settings.Secure.GetString(
            context.ContentResolver,
            global::Android.Provider.Settings.Secure.EnabledAccessibilityServices);
        if (string.IsNullOrWhiteSpace(enabledServices))
        {
            return false;
        }

        var componentName = new ComponentName(context, Java.Lang.Class.FromType(typeof(GlobalHotkeyAccessibilityService)));
        var fullName = componentName.FlattenToString();
        var shortName = componentName.FlattenToShortString();

        return enabledServices
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(service =>
                string.Equals(service, fullName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(service, shortName, StringComparison.OrdinalIgnoreCase));
    }
#else
    public bool HasAccessibilityPermission() => false;
    public bool CanDrawOverlays() => false;
#endif
}
