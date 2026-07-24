using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;

namespace GameTranslator.Droid.Services;

internal static class AndroidTranslationHost
{
    public const int ProjectionRequestCode = 4817;
    private const int NotificationRequestCode = 4818;

    public static event Action? SessionStateChanged;

    public static void RequestSession(MainActivity activity)
    {
        // A screen-capture consent token cannot be retained or reused. Ensure a
        // previous failed service instance cannot receive the new start command.
        activity.StopService(new Intent(activity, typeof(TranslationForegroundService)));
        RequestNotificationPermission(activity);
        var manager = (Android.Media.Projection.MediaProjectionManager?)activity.GetSystemService(Context.MediaProjectionService);
        var captureIntent = OperatingSystem.IsAndroidVersionAtLeast(34)
            ? manager!.CreateScreenCaptureIntent(
                Android.Media.Projection.MediaProjectionConfig.CreateConfigForDefaultDisplay())
            : manager!.CreateScreenCaptureIntent();
        activity.StartActivityForResult(captureIntent, ProjectionRequestCode);
    }

    public static void HandleProjectionResult(MainActivity activity, Result resultCode, Intent? data)
    {
        if (resultCode != Result.Ok || data is null)
        {
            return;
        }

        var serviceIntent = new Intent(activity, typeof(TranslationForegroundService));
        serviceIntent.SetAction(TranslationForegroundService.StartSessionAction);
        serviceIntent.PutExtra(TranslationForegroundService.ProjectionResultCodeExtra, (int)resultCode);
        serviceIntent.PutExtra(TranslationForegroundService.ProjectionResultDataExtra, data);
        activity.StartForegroundService(serviceIntent);
    }

    public static void StopSession(Context context)
    {
        var intent = new Intent(context, typeof(TranslationForegroundService));
        intent.SetAction(TranslationForegroundService.StopSessionAction);
        context.StartService(intent);
    }

    public static void RefreshFloatingTriggerAppearance(Context context)
    {
        if (!TranslationForegroundService.IsSessionActive)
        {
            return;
        }

        var intent = new Intent(context, typeof(TranslationForegroundService));
        intent.SetAction(TranslationForegroundService.RefreshAppearanceAction);
        context.StartService(intent);
    }

    public static void NotifySessionStateChanged()
    {
        SessionStateChanged?.Invoke();
    }

    public static bool RequestOverlayPermission(Activity activity)
    {
        if (Settings.CanDrawOverlays(activity))
        {
            return true;
        }

        var intent = new Intent(Settings.ActionManageOverlayPermission);
        intent.SetData(Android.Net.Uri.Parse($"package:{activity.PackageName}"));
        activity.StartActivity(intent);
        return false;
    }

    public static void OpenAccessibilitySettings(Activity activity)
    {
        activity.StartActivity(new Intent(Settings.ActionAccessibilitySettings));
    }

    private static void RequestNotificationPermission(Activity activity)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            activity.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Android.Content.PM.Permission.Granted)
        {
            activity.RequestPermissions([Android.Manifest.Permission.PostNotifications], NotificationRequestCode);
        }
    }
}
