using GameTranslator.Core;
using System.Globalization;

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
#if __ANDROID__
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
#endif
        try
        {
            LoadSettings();
        }
        catch (Exception exception)
        {
            SelectLanguage(SourceLanguageBox, "ja");
            SelectLanguage(TargetLanguageBox, "pl");
            StatusText.Text = $"Nie można jeszcze odczytać ustawień: {exception.Message}";
        }
    }

    private void LoadSettings()
    {
#if __ANDROID__
        var settings = AndroidSettingsStore.Load(global::Android.App.Application.Context!);
        ApiKeyBox.Password = settings.Translation.ApiKey;
        SelectLanguage(SourceLanguageBox, settings.Translation.SourceLanguage);
        SelectLanguage(TargetLanguageBox, settings.Translation.TargetLanguage);
        RecognitionConfidenceBox.Text = settings.Translation.RecognitionConfidence.ToString("0.##", CultureInfo.InvariantCulture);
        HotkeyCodeBox.Text = settings.HotkeyCode == 0 ? string.Empty : settings.HotkeyCode.ToString();
        HoldToPreviewToggle.IsOn = settings.HoldToPreview;
        GlobalHotkeyToggle.IsOn = settings.GlobalHotkeyEnabled;
        UpdateSessionButton();
#else
        SelectLanguage(SourceLanguageBox, "ja");
        SelectLanguage(TargetLanguageBox, "pl");
        RecognitionConfidenceBox.Text = TranslationSettings.DefaultRecognitionConfidence.ToString("0.##", CultureInfo.InvariantCulture);
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
                    ? "Uprawnienie nakładki jest już przyznane."
                    : "Otwieram ustawienia nakładki Androida.";
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Nie można otworzyć ustawień nakładki: {exception.Message}";
            }
        }
        else
        {
            StatusText.Text = "Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.";
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
                StatusText.Text = "Otwieram ustawienia dostępności Androida.";
                AndroidTranslationHost.OpenAccessibilitySettings(activity);
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Nie można otworzyć ustawień dostępności: {exception.Message}";
            }
        }
        else
        {
            StatusText.Text = "Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.";
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
            StatusText.Text = "Aktywność Androida nie jest jeszcze gotowa.";
            return;
        }
        if (TranslationForegroundService.IsSessionActive)
        {
            AndroidTranslationHost.StopSession(activity);
            StatusText.Text = "Zatrzymano sesję tłumacza.";
        }
        else
        {
            try
            {
                StatusText.Text = "Otwieram dialog udostępniania ekranu Androida.";
                AndroidTranslationHost.RequestSession(activity);
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Nie można uruchomić przechwytywania ekranu: {exception.Message}";
            }
        }

#endif
    }

#if __ANDROID__
    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        AndroidTranslationHost.SessionStateChanged += OnSessionStateChanged;
        UpdateSessionButton();
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        AndroidTranslationHost.SessionStateChanged -= OnSessionStateChanged;
    }

    private void OnSessionStateChanged()
    {
        _ = DispatcherQueue.TryEnqueue(UpdateSessionButton);
    }
#endif

    private bool SaveSettings()
    {
        var hotkeyText = HotkeyCodeBox.Text?.Trim();
        var hotkeyCode = 0;
        if (!string.IsNullOrEmpty(hotkeyText) && (!int.TryParse(hotkeyText, out hotkeyCode) || hotkeyCode < 0))
        {
            StatusText.Text = "Android key code musi być liczbą całkowitą większą lub równą zero.";
            return false;
        }

        if (GlobalHotkeyToggle.IsOn && hotkeyCode == 0)
        {
            StatusText.Text = "Podaj Android key code albo wyłącz globalny hotkey.";
            return false;
        }

        var recognitionConfidenceText = RecognitionConfidenceBox.Text?.Trim() ?? string.Empty;
        if (!TryParseRecognitionConfidence(recognitionConfidenceText, out var recognitionConfidence))
        {
            StatusText.Text = "Pewność rozpoznawania tekstu musi być liczbą od 0 do 1.";
            return false;
        }

        var settings = new TranslationSettings(
            ApiKeyBox.Password.Trim(),
            GetLanguage(SourceLanguageBox),
            GetLanguage(TargetLanguageBox),
            recognitionConfidence);
        if (!settings.IsValid)
        {
            StatusText.Text = "Wpisz klucz API i wybierz oba języki.";
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
        StartSessionButton.Content = TranslationForegroundService.IsSessionActive ? "Zatrzymaj tłumacza" : "Włącz tłumacza";
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

    private static bool TryParseRecognitionConfidence(string text, out float value)
    {
        if (!float.TryParse(text, CultureInfo.InvariantCulture, out value) &&
            !float.TryParse(text, CultureInfo.CurrentCulture, out value))
        {
            return false;
        }

        return float.IsFinite(value) && value is >= 0f and <= 1f;
    }
}
