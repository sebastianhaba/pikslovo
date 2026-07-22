using GameTranslator.Core;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Globalization;

#if __ANDROID__
using GameTranslator.Droid;
using GameTranslator.Droid.Services;
using AndroidToast = Android.Widget.Toast;
using AndroidToastLength = Android.Widget.ToastLength;
#endif

namespace GameTranslator;

public sealed partial class MainPage : Page
{
    private bool _isLoading;
    private bool _updatingSessionToggle;

    public MainPage()
    {
        _isLoading = true;
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
            ShowStatus($"Nie można jeszcze odczytać ustawień: {exception.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void LoadSettings()
    {
#if __ANDROID__
        var settings = AndroidSettingsStore.Load(global::Android.App.Application.Context!);
        ApiKeyBox.Password = settings.Translation.ApiKey;
        SelectLanguage(SourceLanguageBox, settings.Translation.SourceLanguage);
        SelectLanguage(TargetLanguageBox, settings.Translation.TargetLanguage);
        FontScaleSlider.Value = settings.Translation.FontScale;
        RecognitionConfidenceSlider.Value = settings.Translation.RecognitionConfidence;
        HideIdenticalTranslationsToggle.IsOn = settings.Translation.HideIdenticalTranslations;
        HotkeyCodeBox.Text = settings.HotkeyCode == 0 ? string.Empty : settings.HotkeyCode.ToString(CultureInfo.InvariantCulture);
        HoldToPreviewToggle.IsOn = settings.HoldToPreview;
        GlobalHotkeyToggle.IsOn = settings.GlobalHotkeyEnabled;
#else
        SelectLanguage(SourceLanguageBox, "ja");
        SelectLanguage(TargetLanguageBox, "pl");
        FontScaleSlider.Value = TranslationSettings.DefaultFontScale;
        RecognitionConfidenceSlider.Value = TranslationSettings.DefaultRecognitionConfidence;
        HideIdenticalTranslationsToggle.IsOn = false;
#endif
        UpdateFontScaleValue();
        UpdateRecognitionConfidenceValue();
        UpdateSessionToggle();
    }

    private void OpenSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string section })
        {
            return;
        }

        DetailTitle.Text = section switch
        {
            "translation" => "Tłumaczenie",
            "api" => "Google Cloud API",
            "appearance" => "Wygląd nakładki",
            "recognition" => "Rozpoznawanie tekstu",
            "triggers" => "Globalny hotkey",
            "permissions" => "Uprawnienia",
            _ => string.Empty
        };

        TranslationSection.Visibility = section == "translation" ? Visibility.Visible : Visibility.Collapsed;
        ApiSection.Visibility = section == "api" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceSection.Visibility = section == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        RecognitionSection.Visibility = section == "recognition" ? Visibility.Visible : Visibility.Collapsed;
        TriggersSection.Visibility = section == "triggers" ? Visibility.Visible : Visibility.Collapsed;
        PermissionsSection.Visibility = section == "permissions" ? Visibility.Visible : Visibility.Collapsed;
        HomeHeader.Visibility = Visibility.Collapsed;
        DetailHeader.Visibility = Visibility.Visible;
        HomeView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        DetailView.ChangeView(null, 0, null, true);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        HomeHeader.Visibility = Visibility.Visible;
        DetailHeader.Visibility = Visibility.Collapsed;
        HomeView.Visibility = Visibility.Visible;
        DetailView.Visibility = Visibility.Collapsed;
        HomeView.ChangeView(null, 0, null, true);
    }

    private void Setting_Changed(object sender, RoutedEventArgs e) => SaveSettings(requireValidTranslationSettings: false);

    private void RequestOverlayPermission_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            try
            {
                var alreadyAllowed = AndroidTranslationHost.RequestOverlayPermission(activity);
                ShowStatus(alreadyAllowed
                    ? "Uprawnienie nakładki jest już przyznane."
                    : "Otwieram ustawienia nakładki Androida.");
            }
            catch (Exception exception)
            {
                ShowStatus($"Nie można otworzyć ustawień nakładki: {exception.Message}");
            }
        }
        else
        {
            ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
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
                ShowStatus("Otwieram ustawienia dostępności Androida.");
                AndroidTranslationHost.OpenAccessibilitySettings(activity);
            }
            catch (Exception exception)
            {
                ShowStatus($"Nie można otworzyć ustawień dostępności: {exception.Message}");
            }
        }
        else
        {
            ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
        }
#endif
    }

    private void SessionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _updatingSessionToggle)
        {
            return;
        }

#if __ANDROID__
        var activity = MainActivity.CurrentActivity;
        if (activity is null)
        {
            ShowStatus("Aktywność Androida nie jest jeszcze gotowa.");
            UpdateSessionToggle();
            return;
        }

        if (!SessionToggle.IsOn)
        {
            AndroidTranslationHost.StopSession(activity);
            ShowStatus("Zatrzymano sesję tłumacza.");
            return;
        }

        if (!SaveSettings(requireValidTranslationSettings: true))
        {
            UpdateSessionToggle();
            return;
        }

        try
        {
            ShowStatus("Otwieram dialog udostępniania ekranu Androida.");
            AndroidTranslationHost.RequestSession(activity);
        }
        catch (Exception exception)
        {
            ShowStatus($"Nie można uruchomić przechwytywania ekranu: {exception.Message}");
            UpdateSessionToggle();
        }
#endif
    }

#if __ANDROID__
    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        AndroidTranslationHost.SessionStateChanged += OnSessionStateChanged;
        UpdateSessionToggle();
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e) => AndroidTranslationHost.SessionStateChanged -= OnSessionStateChanged;

    private void OnSessionStateChanged() => _ = DispatcherQueue.TryEnqueue(UpdateSessionToggle);
#endif

    private bool SaveSettings(bool requireValidTranslationSettings)
    {
        if (_isLoading)
        {
            return true;
        }

        var hotkeyText = HotkeyCodeBox.Text?.Trim();
        var hotkeyCode = 0;
        if (!string.IsNullOrEmpty(hotkeyText) && (!int.TryParse(hotkeyText, out hotkeyCode) || hotkeyCode < 0))
        {
            ShowStatus("Android key code musi być liczbą całkowitą większą lub równą zero.");
            return false;
        }

        if (requireValidTranslationSettings && GlobalHotkeyToggle.IsOn && hotkeyCode == 0)
        {
            ShowStatus("Podaj Android key code albo wyłącz globalny hotkey.");
            return false;
        }

        var settings = new TranslationSettings(
            ApiKeyBox.Password.Trim(),
            GetLanguage(SourceLanguageBox),
            GetLanguage(TargetLanguageBox),
            (float)RecognitionConfidenceSlider.Value,
            (float)FontScaleSlider.Value,
            HideIdenticalTranslationsToggle.IsOn);
        if (requireValidTranslationSettings && !settings.IsValid)
        {
            ShowStatus("Wpisz klucz API i wybierz oba języki.");
            return false;
        }

#if __ANDROID__
        AndroidSettingsStore.Save(
            global::Android.App.Application.Context!,
            new AndroidAppSettings(settings, hotkeyCode, HoldToPreviewToggle.IsOn, GlobalHotkeyToggle.IsOn));
#endif
        return true;
    }

    private void UpdateSessionToggle()
    {
#if __ANDROID__
        _updatingSessionToggle = true;
        SessionToggle.IsOn = TranslationForegroundService.IsSessionActive;
        _updatingSessionToggle = false;
#endif
    }

    private void ShowStatus(string message)
    {
#if __ANDROID__
        AndroidToast.MakeText(global::Android.App.Application.Context!, message, AndroidToastLength.Short)?.Show();
#endif
    }

    private static string GetLanguage(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;

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

    private void RecognitionConfidenceSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateRecognitionConfidenceValue();
        SaveSettings(requireValidTranslationSettings: false);
    }

    private void FontScaleSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateFontScaleValue();
        SaveSettings(requireValidTranslationSettings: false);
    }

    private void UpdateFontScaleValue() => FontScaleValue.Text = $"{FontScaleSlider.Value.ToString("0.0", CultureInfo.CurrentCulture)}x";

    private void UpdateRecognitionConfidenceValue() => RecognitionConfidenceValue.Text = RecognitionConfidenceSlider.Value.ToString("0.0", CultureInfo.CurrentCulture);
}
