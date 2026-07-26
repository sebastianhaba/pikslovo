#if __ANDROID__
using Pikslovo.Droid;
using Pikslovo.Droid.Services;
#endif

namespace Pikslovo;

public sealed partial class MainPage
{
    private void LoadSettings()
    {
        _settingsPersistence.Load(_viewModel);
        ApplyViewModelToControls();
        UpdateFontScaleValue();
        UpdateRecognitionConfidenceValue();
        UpdateOcrImageScaleValue();
        UpdateOcrJpegQualityValue();
        UpdateOcrJpegQualityControl();
        UpdateGroupingPowerValue();
        UpdateFloatingButtonValues();
        UpdateSettingSummaries();
        UpdateSessionToggle();
    }

    private bool SaveSettings(bool requireValidTranslationSettings)
    {
        if (_isLoading)
        {
            return true;
        }

        UpdateViewModelFromControls();
        if (requireValidTranslationSettings && _viewModel.GlobalHotkeyEnabled && _viewModel.HotkeyCodes.Length == 0)
        {
            ShowStatus("Ustaw skrót albo wyłącz globalny hotkey.");
            return false;
        }

        if (requireValidTranslationSettings && !_viewModel.CreateTranslationSettings().IsValid)
        {
            ShowStatus("Wpisz klucz API i wybierz oba języki.");
            return false;
        }

        return _settingsPersistence.Save(_viewModel);
    }

#if __ANDROID__
    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        AndroidTranslationHost.SessionStateChanged += OnSessionStateChanged;
        AndroidTranslationHost.SettingsExportFileCreated += OnSettingsExportFileCreated;
        AndroidTranslationHost.SettingsImportFileSelected += OnSettingsImportFileSelected;
        AndroidTranslationHost.NotificationPermissionResult += OnNotificationPermissionResult;
        UpdateSessionToggle();
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        AndroidTranslationHost.SessionStateChanged -= OnSessionStateChanged;
        AndroidTranslationHost.SettingsExportFileCreated -= OnSettingsExportFileCreated;
        AndroidTranslationHost.SettingsImportFileSelected -= OnSettingsImportFileSelected;
        AndroidTranslationHost.NotificationPermissionResult -= OnNotificationPermissionResult;
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

    private async void OnSettingsExportFileCreated(global::Android.App.Result resultCode, global::Android.Content.Intent? data)
    {
        try
        {
            await _settingsPersistence.ExportAsync(resultCode, data);
            ShowStatus("Ustawienia wyeksportowano do pliku JSON.");
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format("Nie można wyeksportować ustawień: {0}", exception.Message));
        }
    }

    private async void OnSettingsImportFileSelected(global::Android.App.Result resultCode, global::Android.Content.Intent? data)
    {
        try
        {
            var profile = await _settingsPersistence.ImportAsync(resultCode, data);
            if (profile is null)
            {
                return;
            }

            ApplySettingsProfile(profile);
            ShowStatus("Ustawienia zaimportowano z pliku JSON.");
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format("Nie można zaimportować ustawień: {0}", exception.Message));
        }
    }

    private void ApplySettingsProfile(SettingsProfile profile)
    {
        _settingsPersistence.ApplyProfile(_viewModel, profile);

        var wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            ApplyViewModelToControls();
            UpdateFontScaleValue();
            UpdateRecognitionConfidenceValue();
            UpdateOcrImageScaleValue();
            UpdateOcrJpegQualityValue();
            UpdateOcrJpegQualityControl();
            UpdateGroupingPowerValue();
            UpdateFloatingButtonValues();
            UpdateSettingSummaries();
            UpdateSessionToggle();
        }
        finally
        {
            _isLoading = wasLoading;
        }

        RefreshFloatingButtonConfiguration();
    }
#endif

    private void ExportSettings_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is not { } activity)
        {
            ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
            return;
        }

        try
        {
            AndroidTranslationHost.CreateSettingsExportFile(activity);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format("Nie można otworzyć wyboru pliku: {0}", exception.Message));
        }
#endif
    }

    private void ImportSettings_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is not { } activity)
        {
            ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
            return;
        }

        try
        {
            AndroidTranslationHost.OpenSettingsImportFile(activity);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format("Nie można otworzyć wyboru pliku: {0}", exception.Message));
        }
#endif
    }

    private async void RestoreDefaultSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!await ShowConfirmationAsync(
                "Przywrócić ustawienia domyślne?",
                "Zostaną zresetowane ustawienia OCR, w tym kompresja JPEG i jakość obrazu, nakładka, obszar przechwytywania oraz przycisk pływający. Klucz API, języki, hotkey i wygląd aplikacji pozostaną bez zmian."))
        {
            return;
        }

#if __ANDROID__
        try
        {
            ApplySettingsProfile(SettingsProfile.Defaults);
            ShowStatus("Przywrócono domyślne ustawienia konfiguracji.");
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format("Nie można przywrócić ustawień: {0}", exception.Message));
        }
#endif
    }

    private void ApplyViewModelToControls()
    {
        ApiKeyBox.Password = _viewModel.ApiKey;
        SelectLanguage(SourceLanguageBox, _viewModel.SourceLanguage);
        SelectLanguage(TargetLanguageBox, _viewModel.TargetLanguage);
        FontScaleSlider.Value = _viewModel.FontScale;
        RecognitionConfidenceSlider.Value = _viewModel.RecognitionConfidence;
        OcrImageScaleSlider.Value = _viewModel.OcrImageScale;
        UseJpegForOcrToggle.IsOn = _viewModel.UseJpegForOcr;
        OcrJpegQualitySlider.Value = _viewModel.OcrJpegQuality;
        GroupingPowerSlider.Value = _viewModel.GroupingPower;
        HideIdenticalTranslationsToggle.IsOn = _viewModel.HideIdenticalTranslations;
        GlobalHotkeyToggle.IsOn = _viewModel.GlobalHotkeyEnabled;
        SetThemeMode(_viewModel.ThemeMode);
        SetAccent(_viewModel.Accent);
        SetApplicationLanguage(_viewModel.LanguageMode);
        FloatingButtonAlwaysVisibleToggle.IsOn = _viewModel.FloatingButtonAlwaysVisible;
        FloatingButtonScaleSlider.Value = _viewModel.FloatingButtonScale;
        FloatingButtonHorizontalPositionSlider.Value = _viewModel.FloatingButtonHorizontalPosition;
        FloatingButtonVerticalPositionSlider.Value = _viewModel.FloatingButtonVerticalPosition;
    }

    private void UpdateViewModelFromControls()
    {
        _viewModel.ApiKey = ApiKeyBox.Password.Trim();
        _viewModel.SourceLanguage = GetLanguage(SourceLanguageBox);
        _viewModel.TargetLanguage = GetLanguage(TargetLanguageBox);
        _viewModel.FontScale = (float)FontScaleSlider.Value;
        _viewModel.RecognitionConfidence = (float)RecognitionConfidenceSlider.Value;
        _viewModel.OcrImageScale = (float)OcrImageScaleSlider.Value;
        _viewModel.UseJpegForOcr = UseJpegForOcrToggle.IsOn;
        _viewModel.OcrJpegQuality = (int)Math.Round(OcrJpegQualitySlider.Value);
        _viewModel.GroupingPower = (float)GroupingPowerSlider.Value;
        _viewModel.HideIdenticalTranslations = HideIdenticalTranslationsToggle.IsOn;
        _viewModel.GlobalHotkeyEnabled = GlobalHotkeyToggle.IsOn;
        _viewModel.FloatingButtonAlwaysVisible = FloatingButtonAlwaysVisibleToggle.IsOn;
        _viewModel.FloatingButtonScale = (float)FloatingButtonScaleSlider.Value;
        _viewModel.FloatingButtonHorizontalPosition = (float)FloatingButtonHorizontalPositionSlider.Value;
        _viewModel.FloatingButtonVerticalPosition = (float)FloatingButtonVerticalPositionSlider.Value;
    }
}
