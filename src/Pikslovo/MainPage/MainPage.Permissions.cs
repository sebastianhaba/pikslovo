#if __ANDROID__
using Pikslovo.Droid;
#endif

namespace Pikslovo;

public sealed partial class MainPage
{
    private void RequestOverlayPermission()
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            try
            {
                _permissionsService.RequestOverlayPermission(activity, out var statusMessage);
                ShowStatus(statusMessage);
            }
            catch (Exception exception)
            {
                ShowStatus(AppStrings.Format(AppStrings.Keys.OpenOverlaySettingsFailed, exception.Message));
            }
        }
        else
        {
            ShowStatus(AppStrings.Keys.AndroidActivityNotReady);
        }
#endif
    }

    private void RequestNotificationPermission()
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            try
            {
                _permissionsService.RequestNotificationPermission(activity, out var statusMessage);
                ShowStatus(statusMessage);
            }
            catch (Exception exception)
            {
                ShowStatus(AppStrings.Format(AppStrings.Keys.RequestNotificationPermissionFailed, exception.Message));
            }
        }
        else
        {
            ShowStatus(AppStrings.Keys.AndroidActivityNotReady);
        }
#endif
    }

    private void OpenAccessibilitySettings()
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            try
            {
                ShowStatus(AppStrings.Keys.OpeningAccessibilitySettings);
                _permissionsService.OpenAccessibilitySettings(activity);
            }
            catch (Exception exception)
            {
                ShowStatus(AppStrings.Format(AppStrings.Keys.OpenAccessibilitySettingsFailed, exception.Message));
            }
        }
        else
        {
            ShowStatus(AppStrings.Keys.AndroidActivityNotReady);
        }
#endif
    }
}
