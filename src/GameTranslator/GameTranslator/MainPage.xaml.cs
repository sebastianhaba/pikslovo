using GameTranslator.Core;

#if __ANDROID__
using GameTranslator.Droid;
using GameTranslator.Droid.Services;
#endif

namespace GameTranslator;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        try
        {
            LoadSettings();
        }
        catch (Exception exception)
        {
            SelectLanguage(SourceLanguageBox, "ja");
            SelectLanguage(TargetLanguageBox, "pl");
            StatusText.Text = $"Nie mozna jeszcze odczytac ustawien: {exception.Message}";
        }
    }

    private void LoadSettings()
    {
#if __ANDROID__
        var settings = AndroidSettingsStore.Load(global::Android.App.Application.Context!);
        ApiKeyBox.Password = settings.Translation.ApiKey;
        SelectLanguage(SourceLanguageBox, settings.Translation.SourceLanguage);
        SelectLanguage(TargetLanguageBox, settings.Translation.TargetLanguage);
        HotkeyCodeBox.Text = settings.HotkeyCode == 0 ? string.Empty : settings.HotkeyCode.ToString();
        HoldToPreviewToggle.IsOn = settings.HoldToPreview;
        GlobalHotkeyToggle.IsOn = settings.GlobalHotkeyEnabled;
        UpdateSessionButton();
#else
        SelectLanguage(SourceLanguageBox, "ja");
        SelectLanguage(TargetLanguageBox, "pl");
#endif
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (SaveSettings())
        {
            StatusText.Text = "Ustawienia zapisane.";
        }
    }

    private void RequestOverlayPermission_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            try
            {
                var alreadyAllowed = AndroidTranslationHost.RequestOverlayPermission(activity);
                StatusText.Text = alreadyAllowed
                    ? "Uprawnienie nakladki jest juz przyznane."
                    : "Otwieram ustawienia nakladki Androida.";
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Nie mozna otworzyc ustawien nakladki: {exception.Message}";
            }
        }
        else
        {
            StatusText.Text = "Aktywnosc Androida nie jest gotowa. Zamknij i otworz aplikacje ponownie.";
        }
#endif
    }

    private void OpenAccessibilitySettings_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            try
            {
                StatusText.Text = "Otwieram ustawienia dostepnosci Androida.";
                AndroidTranslationHost.OpenAccessibilitySettings(activity);
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Nie mozna otworzyc ustawien dostepnosci: {exception.Message}";
            }
        }
        else
        {
            StatusText.Text = "Aktywnosc Androida nie jest gotowa. Zamknij i otworz aplikacje ponownie.";
        }
#endif
    }

    private void ToggleSession_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (!SaveSettings())
        {
            return;
        }

        var activity = MainActivity.CurrentActivity;
        if (activity is null)
        {
            StatusText.Text = "Aktywnosc Androida nie jest jeszcze gotowa.";
            return;
        }
        if (TranslationForegroundService.IsSessionActive)
        {
            AndroidTranslationHost.StopSession(activity);
            StatusText.Text = "Zatrzymano sesje tlumacza.";
        }
        else
        {
            try
            {
                StatusText.Text = "Otwieram dialog udostepniania ekranu Androida.";
                AndroidTranslationHost.RequestSession(activity);
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Nie mozna uruchomic przechwytywania ekranu: {exception.Message}";
            }
        }

        UpdateSessionButton();
#endif
    }

    private bool SaveSettings()
    {
        var hotkeyText = HotkeyCodeBox.Text?.Trim();
        var hotkeyCode = 0;
        if (!string.IsNullOrEmpty(hotkeyText) && (!int.TryParse(hotkeyText, out hotkeyCode) || hotkeyCode < 0))
        {
            StatusText.Text = "Android key code musi byc liczba calkowita wieksza lub rowna zero.";
            return false;
        }

        if (GlobalHotkeyToggle.IsOn && hotkeyCode == 0)
        {
            StatusText.Text = "Podaj Android key code albo wylacz globalny hotkey.";
            return false;
        }

        var settings = new TranslationSettings(
            ApiKeyBox.Password.Trim(),
            GetLanguage(SourceLanguageBox),
            GetLanguage(TargetLanguageBox));
        if (!settings.IsValid)
        {
            StatusText.Text = "Wpisz klucz API i wybierz oba jezyki.";
            return false;
        }

#if __ANDROID__
        AndroidSettingsStore.Save(
            global::Android.App.Application.Context!,
            new AndroidAppSettings(
                settings,
                hotkeyCode,
                HoldToPreviewToggle.IsOn,
                GlobalHotkeyToggle.IsOn));
#endif
        return true;
    }

    private void UpdateSessionButton()
    {
#if __ANDROID__
        StartSessionButton.Content = TranslationForegroundService.IsSessionActive ? "Zatrzymaj tlumacza" : "Wlacz tlumacza";
#endif
    }

    private static string GetLanguage(ComboBox box) =>
        ((ComboBoxItem)box.SelectedItem).Tag?.ToString() ?? "";

    private static void SelectLanguage(ComboBox box, string language)
    {
        for (var index = 0; index < box.Items.Count; index++)
        {
            if (box.Items[index] is ComboBoxItem item && item.Tag?.ToString() == language)
            {
                box.SelectedIndex = index;
                return;
            }
        }

        box.SelectedIndex = 0;
    }
}
