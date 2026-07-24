using GameTranslator.Core;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System.Globalization;
using System.Threading.Tasks;

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
    private int[] _hotkeyCodes = [];
    private AppThemeMode _themeMode = AppThemeMode.System;
    private AppAccent _accent = AppAccent.Lavender;
#if __ANDROID__
    private FloatingTranslationTrigger? _floatingButtonPreview;
#endif

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
        _hotkeyCodes = settings.HotkeyCodes;
        GlobalHotkeyToggle.IsOn = settings.GlobalHotkeyEnabled;
        SetThemeMode(settings.ThemeMode);
        SetAccent(settings.Accent);
        FloatingButtonAlwaysVisibleToggle.IsOn = settings.FloatingButton.AlwaysVisible;
        FloatingButtonScaleSlider.Value = settings.FloatingButton.Scale;
        FloatingButtonHorizontalPositionSlider.Value = settings.FloatingButton.HorizontalPosition;
        FloatingButtonVerticalPositionSlider.Value = settings.FloatingButton.VerticalPosition;
#else
        SelectLanguage(SourceLanguageBox, "ja");
        SelectLanguage(TargetLanguageBox, "pl");
        FontScaleSlider.Value = TranslationSettings.DefaultFontScale;
        RecognitionConfidenceSlider.Value = TranslationSettings.DefaultRecognitionConfidence;
        HideIdenticalTranslationsToggle.IsOn = false;
        SetThemeMode(AppThemeMode.System);
        SetAccent(AppAccent.Lavender);
        FloatingButtonAlwaysVisibleToggle.IsOn = true;
        FloatingButtonScaleSlider.Value = 1;
        FloatingButtonHorizontalPositionSlider.Value = 1;
        FloatingButtonVerticalPositionSlider.Value = 0.1;
#endif
        UpdateFontScaleValue();
        UpdateRecognitionConfidenceValue();
        UpdateFloatingButtonValues();
        UpdateSettingSummaries();
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
            "appTheme" => "Wygląd aplikacji",
            "recognition" => "Rozpoznawanie tekstu",
            "triggers" => "Globalny hotkey",
            "floatingButton" => "Przycisk pływający",
            "permissions" => "Uprawnienia",
            _ => string.Empty
        };

        TranslationSection.Visibility = section == "translation" ? Visibility.Visible : Visibility.Collapsed;
        ApiSection.Visibility = section == "api" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceSection.Visibility = section == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        RecognitionSection.Visibility = section == "recognition" ? Visibility.Visible : Visibility.Collapsed;
        TriggersSection.Visibility = section == "triggers" ? Visibility.Visible : Visibility.Collapsed;
        FloatingButtonSection.Visibility = section == "floatingButton" ? Visibility.Visible : Visibility.Collapsed;
        PermissionsSection.Visibility = section == "permissions" ? Visibility.Visible : Visibility.Collapsed;
        ThemeSection.Visibility = section == "appTheme" ? Visibility.Visible : Visibility.Collapsed;
        HomeHeader.Visibility = Visibility.Collapsed;
        DetailHeader.Visibility = Visibility.Visible;
        HomeView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        DetailView.ChangeView(null, 0, null, true);
        UpdateFloatingButtonPreview();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        HomeHeader.Visibility = Visibility.Visible;
        DetailHeader.Visibility = Visibility.Collapsed;
        HomeView.Visibility = Visibility.Visible;
        DetailView.Visibility = Visibility.Collapsed;
        HomeView.ChangeView(null, 0, null, true);
        DismissFloatingButtonPreview();
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        SaveSettings(requireValidTranslationSettings: false);
        UpdateFloatingButtonValues();
        UpdateSettingSummaries();
        if (ReferenceEquals(sender, GlobalHotkeyToggle))
        {
            RefreshFloatingButtonConfiguration();
        }
        else
        {
            UpdateFloatingButtonPreview();
        }
    }

    private void ThemeModeOption_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_isLoading || sender is not Border { Tag: string value } ||
            !Enum.TryParse<AppThemeMode>(value, out var mode))
        {
            return;
        }

        SetThemeMode(mode);
        SaveSettings(requireValidTranslationSettings: false);
        UpdateSettingSummaries();
    }

    private void AccentOption_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_isLoading || sender is not Border { Tag: string value } ||
            !Enum.TryParse<AppAccent>(value, out var accent))
        {
            return;
        }

        SetAccent(accent);
        if (SaveSettings(requireValidTranslationSettings: false))
        {
#if __ANDROID__
            AndroidTranslationHost.RefreshFloatingTriggerConfiguration(global::Android.App.Application.Context!);
            UpdateFloatingButtonPreview();
#endif
        }
        UpdateSettingSummaries();
    }

    private async void EditSourceLanguage_Click(object sender, RoutedEventArgs e) => await EditLanguageAsync(SourceLanguageBox, "Język źródłowy");

    private async void EditTargetLanguage_Click(object sender, RoutedEventArgs e) => await EditLanguageAsync(TargetLanguageBox, "Język docelowy");

    private async Task EditLanguageAsync(ComboBox source, string title)
    {
        var picker = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var entry in source.Items)
        {
            if (entry is ComboBoxItem item)
            {
                picker.Items.Add(new ComboBoxItem { Tag = item.Tag, Content = item.Content });
            }
        }

        picker.SelectedIndex = source.SelectedIndex;
        if (await ShowEditorAsync(title, picker))
        {
            source.SelectedIndex = picker.SelectedIndex;
            UpdateSettingSummaries();
        }
    }

    private async void EditApiKey_Click(object sender, RoutedEventArgs e)
    {
        var editor = new PasswordBox { Password = ApiKeyBox.Password, PlaceholderText = "AIza..." };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = "Klucz jest przechowywany lokalnie w Android Keystore.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(editor);

        if (await ShowEditorAsync("Google Cloud API key", content))
        {
            ApiKeyBox.Password = editor.Password;
            SaveSettings(requireValidTranslationSettings: false);
            UpdateSettingSummaries();
        }
    }

    private async void EditHotkeyCode_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is not { } activity)
        {
            ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
            return;
        }

        var hotkeyCodes = await HotkeyCaptureDialog.ShowAsync(activity);
        if (hotkeyCodes is { Length: > 0 })
        {
            _hotkeyCodes = hotkeyCodes;
            SaveSettings(requireValidTranslationSettings: false);
            UpdateSettingSummaries();
        }
#endif
    }

    private async Task<bool> ShowEditorAsync(string title, object content)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "Zapisz",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

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

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        AndroidTranslationHost.SessionStateChanged -= OnSessionStateChanged;
        DismissFloatingButtonPreview();
    }

    private void OnSessionStateChanged() => _ = DispatcherQueue.TryEnqueue(() =>
    {
        UpdateSessionToggle();
        if (TranslationForegroundService.IsSessionActive)
        {
            DismissFloatingButtonPreview();
        }
    });
#endif

    private bool SaveSettings(bool requireValidTranslationSettings)
    {
        if (_isLoading)
        {
            return true;
        }

        if (requireValidTranslationSettings && GlobalHotkeyToggle.IsOn && _hotkeyCodes.Length == 0)
        {
            ShowStatus("Ustaw skrót albo wyłącz globalny hotkey.");
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
            new AndroidAppSettings(
                settings,
                _hotkeyCodes,
                GlobalHotkeyToggle.IsOn,
                _themeMode,
                _accent,
                new FloatingButtonSettings(
                    FloatingButtonAlwaysVisibleToggle.IsOn,
                    (float)FloatingButtonScaleSlider.Value,
                    (float)FloatingButtonHorizontalPositionSlider.Value,
                    (float)FloatingButtonVerticalPositionSlider.Value)));
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

    private static string GetLanguageLabel(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Nie wybrano";

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

    private void FloatingButtonSetting_Changed(object sender, RoutedEventArgs e)
    {
        SaveSettings(requireValidTranslationSettings: false);
        UpdateFloatingButtonValues();
        RefreshFloatingButtonConfiguration();
    }

    private void FloatingButtonScaleSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateFloatingButtonValues();
        SaveSettings(requireValidTranslationSettings: false);
        RefreshFloatingButtonConfiguration();
    }

    private void FloatingButtonHorizontalPositionSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateFloatingButtonValues();
        SaveSettings(requireValidTranslationSettings: false);
        RefreshFloatingButtonConfiguration();
    }

    private void FloatingButtonVerticalPositionSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateFloatingButtonValues();
        SaveSettings(requireValidTranslationSettings: false);
        RefreshFloatingButtonConfiguration();
    }

    private void UpdateFontScaleValue() => FontScaleValue.Text = FormatFontScale(FontScaleSlider.Value);

    private void UpdateRecognitionConfidenceValue() => RecognitionConfidenceValue.Text = FormatRecognitionConfidence(RecognitionConfidenceSlider.Value);

    private void UpdateFloatingButtonValues()
    {
        FloatingButtonScaleValue.Text = FormatFontScale(FloatingButtonScaleSlider.Value);
        FloatingButtonHorizontalPositionValue.Text = FormatPosition(FloatingButtonHorizontalPositionSlider.Value);
        FloatingButtonVerticalPositionValue.Text = FormatPosition(FloatingButtonVerticalPositionSlider.Value);
        FloatingButtonVisibilityDescription.Text = FloatingButtonAlwaysVisibleToggle.IsOn
            ? "W aktywnej sesji jest widoczny niezależnie od globalnego hotkeya."
            : GlobalHotkeyToggle.IsOn
                ? "W aktywnej sesji jest ukryty, gdy globalny hotkey jest włączony."
                : "W aktywnej sesji jest widoczny, ponieważ globalny hotkey jest wyłączony.";
    }

    private void RefreshFloatingButtonConfiguration()
    {
#if __ANDROID__
        AndroidTranslationHost.RefreshFloatingTriggerConfiguration(global::Android.App.Application.Context!);
#endif
        UpdateFloatingButtonPreview();
    }

    private void UpdateFloatingButtonPreview()
    {
#if __ANDROID__
        if (FloatingButtonSection.Visibility != Visibility.Visible || TranslationForegroundService.IsSessionActive)
        {
            DismissFloatingButtonPreview();
            return;
        }

        var context = global::Android.App.Application.Context!;
        if (!global::Android.Provider.Settings.CanDrawOverlays(context))
        {
            DismissFloatingButtonPreview();
            return;
        }

        if (_floatingButtonPreview is null)
        {
            _floatingButtonPreview = new FloatingTranslationTrigger(context);
            _floatingButtonPreview.ShowPreview();
            return;
        }

        _floatingButtonPreview.RefreshConfiguration();
#endif
    }

    private void DismissFloatingButtonPreview()
    {
#if __ANDROID__
        _floatingButtonPreview?.Dismiss();
        _floatingButtonPreview = null;
#endif
    }

    private void UpdateSettingSummaries()
    {
        SourceLanguageValue.Text = GetLanguageLabel(SourceLanguageBox);
        TargetLanguageValue.Text = GetLanguageLabel(TargetLanguageBox);
        ApiKeyValue.Text = string.IsNullOrWhiteSpace(ApiKeyBox.Password) ? "Wymagany do uruchomienia tłumacza" : "Klucz zapisany";
#if __ANDROID__
        HotkeyCodeValue.Text = HotkeyCaptureDialog.Format(_hotkeyCodes);
#else
        HotkeyCodeValue.Text = "Nie ustawiono";
#endif
        var themeMode = _themeMode switch
        {
            AppThemeMode.Dark => "Ciemny",
            AppThemeMode.Light => "Jasny",
            _ => "Systemowy"
        };
        ThemeModeValue.Text = $"{themeMode} · {GetAccentLabel(_accent)}";
    }

    private void SetThemeMode(AppThemeMode mode)
    {
        _themeMode = mode;
        SetThemeModeOptionStyle(SystemThemeOption, mode == AppThemeMode.System);
        SetThemeModeOptionStyle(DarkThemeOption, mode == AppThemeMode.Dark);
        SetThemeModeOptionStyle(LightThemeOption, mode == AppThemeMode.Light);
        (global::Microsoft.UI.Xaml.Application.Current as App)?.SetThemeMode(mode);
    }

    private void SetAccent(AppAccent accent)
    {
        _accent = accent;
        UpdateAccentOptionSelection();
        SetThemeModeOptionStyle(SystemThemeOption, _themeMode == AppThemeMode.System);
        SetThemeModeOptionStyle(DarkThemeOption, _themeMode == AppThemeMode.Dark);
        SetThemeModeOptionStyle(LightThemeOption, _themeMode == AppThemeMode.Light);
        (global::Microsoft.UI.Xaml.Application.Current as App)?.SetAccent(accent);
    }

    private void SetThemeModeOptionStyle(Border option, bool selected)
    {
        option.Style = (Style)Resources[selected ? "SelectedThemeModeOptionBorder" : "ThemeModeOptionBorder"];
        if (selected)
        {
            option.BorderBrush = new SolidColorBrush(App.GetAccentColor(_accent));
            return;
        }

        option.BorderBrush = new SolidColorBrush(GetThemeOptionBorderColor());
    }

    private global::Windows.UI.Color GetThemeOptionBorderColor()
    {
        var isDark = _themeMode == AppThemeMode.Dark ||
            (_themeMode == AppThemeMode.System && ActualTheme == ElementTheme.Dark);
        return isDark
            ? global::Windows.UI.Color.FromArgb(255, 73, 69, 79)
            : global::Windows.UI.Color.FromArgb(255, 228, 225, 230);
    }

    private void UpdateAccentOptionSelection()
    {
        SetAccentOptionSelection(LavenderAccentOption, AppAccent.Lavender);
        SetAccentOptionSelection(CoralAccentOption, AppAccent.Coral);
        SetAccentOptionSelection(AmberAccentOption, AppAccent.Amber);
        SetAccentOptionSelection(LimeAccentOption, AppAccent.Lime);
        SetAccentOptionSelection(MintAccentOption, AppAccent.Mint);
        SetAccentOptionSelection(TealAccentOption, AppAccent.Teal);
        SetAccentOptionSelection(AquaAccentOption, AppAccent.Aqua);
        SetAccentOptionSelection(SkyAccentOption, AppAccent.Sky);
        SetAccentOptionSelection(SteelAccentOption, AppAccent.Steel);
        SetAccentOptionSelection(OrchidAccentOption, AppAccent.Orchid);
        SetAccentOptionSelection(RoseAccentOption, AppAccent.Rose);
    }

    private void SetAccentOptionSelection(Border option, AppAccent accent) =>
        option.BorderThickness = accent == _accent ? new Thickness(2) : new Thickness(0);

    private static string GetAccentLabel(AppAccent accent) => accent switch
    {
        AppAccent.Coral => "Koralowy",
        AppAccent.Amber => "Bursztynowy",
        AppAccent.Lime => "Limonkowy",
        AppAccent.Mint => "Miętowy",
        AppAccent.Teal => "Morski",
        AppAccent.Aqua => "Aqua",
        AppAccent.Sky => "Błękitny",
        AppAccent.Steel => "Stalowy",
        AppAccent.Orchid => "Orchidea",
        AppAccent.Rose => "Różowy",
        _ => "Lawendowy"
    };

    private static string FormatFontScale(double value) => $"{value.ToString("0.0", CultureInfo.CurrentCulture)}x";

    private static string FormatPosition(double value) => value.ToString("0.00", CultureInfo.CurrentCulture);

    private static string FormatRecognitionConfidence(double value) => value.ToString("0.0", CultureInfo.CurrentCulture);
}
