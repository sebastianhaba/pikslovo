using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Views.InputMethods;

namespace Pikslovo.Droid.Services;

internal static class AndroidTranslationHost
{
    public const int ProjectionRequestCode = 4817;
    public const int ExportSettingsRequestCode = 4819;
    public const int ImportSettingsRequestCode = 4820;
    private const int NotificationRequestCode = 4818;

    public static event Action? SessionStateChanged;
    public static event Action<Result, Intent?>? SettingsExportFileCreated;
    public static event Action<Result, Intent?>? SettingsImportFileSelected;
    public static event Action<bool>? NotificationPermissionResult;

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

    public static void RefreshFloatingTriggerConfiguration(Context context)
    {
        if (!TranslationForegroundService.IsSessionActive)
        {
            return;
        }

        var intent = new Intent(context, typeof(TranslationForegroundService));
        intent.SetAction(TranslationForegroundService.RefreshAppearanceAction);
        context.StartService(intent);
    }

    public static void CreateSettingsExportFile(Activity activity)
    {
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/json");
        intent.PutExtra(Intent.ExtraTitle, "pikslovo-settings.json");
        activity.StartActivityForResult(intent, ExportSettingsRequestCode);
    }

    public static void OpenSettingsImportFile(Activity activity)
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/json");
        activity.StartActivityForResult(intent, ImportSettingsRequestCode);
    }

    public static bool HandleSettingsFileResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode == ExportSettingsRequestCode)
        {
            SettingsExportFileCreated?.Invoke(resultCode, data);
            return true;
        }

        if (requestCode == ImportSettingsRequestCode)
        {
            SettingsImportFileSelected?.Invoke(resultCode, data);
            return true;
        }

        return false;
    }

    public static bool HandlePermissionRequestResult(int requestCode, Activity activity)
    {
        if (requestCode != NotificationRequestCode)
        {
            return false;
        }

        var granted = !OperatingSystem.IsAndroidVersionAtLeast(33) ||
            activity.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Permission.Granted;
        NotificationPermissionResult?.Invoke(granted);
        return true;
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

    public static void OpenMainSettings(Context context)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        context.StartActivity(intent);
    }

    public static void OpenWebPage(Activity activity, string address)
    {
        var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(address));
        activity.StartActivity(intent);
    }

    public static void HideKeyboard(Activity activity)
    {
        var inputManager = activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
        inputManager?.HideSoftInputFromWindow(activity.Window?.DecorView?.WindowToken, HideSoftInputFlags.None);
    }

    public static bool RequestNotificationPermission(Activity activity)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33) ||
            activity.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Android.Content.PM.Permission.Granted)
        {
            return true;
        }

        activity.RequestPermissions([Android.Manifest.Permission.PostNotifications], NotificationRequestCode);
        return false;
    }
}
