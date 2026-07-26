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
                ShowStatus(AppStrings.Format("Nie można otworzyć ustawień nakładki: {0}", exception.Message));
            }
        }
        else
        {
            ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
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
                ShowStatus(AppStrings.Format("Nie można poprosić o uprawnienie powiadomień: {0}", exception.Message));
            }
        }
        else
        {
            ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
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
                ShowStatus("Otwieram ustawienia dostępności Androida.");
                _permissionsService.OpenAccessibilitySettings(activity);
            }
            catch (Exception exception)
            {
                ShowStatus(AppStrings.Format("Nie można otworzyć ustawień dostępności: {0}", exception.Message));
            }
        }
        else
        {
            ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
        }
#endif
    }
}
