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
        UpdateHotkeyCodesSummary();
        UpdateDiagnostics();
        ApplyViewModelToControls();
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
            ShowStatus(AppStrings.Keys.SetShortcutOrDisableHotkey);
            return false;
        }

        if (requireValidTranslationSettings && !_viewModel.CreateTranslationSettings().IsValid)
        {
            ShowStatus(AppStrings.Keys.EnterApiKeyAndLanguages);
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
            ShowStatus(AppStrings.Keys.SettingsExported);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format(AppStrings.Keys.ExportSettingsFailed, exception.Message));
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
            ShowStatus(AppStrings.Keys.SettingsImported);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format(AppStrings.Keys.ImportSettingsFailed, exception.Message));
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
            UpdateHotkeyCodesSummary();
            UpdateDiagnostics();
            UpdateSessionToggle();
        }
        finally
        {
            _isLoading = wasLoading;
        }

        RefreshFloatingButtonConfiguration();
    }
#endif

    private void ExportSettings()
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is not { } activity)
        {
            ShowStatus(AppStrings.Keys.AndroidActivityNotReady);
            return;
        }

        try
        {
            AndroidTranslationHost.CreateSettingsExportFile(activity);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format(AppStrings.Keys.OpenFilePickerFailed, exception.Message));
        }
#endif
    }

    private void ImportSettings()
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is not { } activity)
        {
            ShowStatus(AppStrings.Keys.AndroidActivityNotReady);
            return;
        }

        try
        {
            AndroidTranslationHost.OpenSettingsImportFile(activity);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format(AppStrings.Keys.OpenFilePickerFailed, exception.Message));
        }
#endif
    }

    private async Task RestoreDefaultSettingsAsync()
    {
        if (!await ShowConfirmationAsync(
                AppStrings.Keys.RestoreDefaultsQuestion,
                AppStrings.Keys.RestoreDefaultsMessage))
        {
            return;
        }

#if __ANDROID__
        try
        {
            ApplySettingsProfile(SettingsProfile.Defaults);
            ShowStatus(AppStrings.Keys.DefaultConfigurationRestored);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format(AppStrings.Keys.RestoreSettingsFailed, exception.Message));
        }
#endif
    }

    private void ApplyViewModelToControls()
    {
        ApiKeyBox.Password = _viewModel.ApiKey;
        SelectLanguage(SourceLanguageBox, _viewModel.SourceLanguage);
        SelectLanguage(TargetLanguageBox, _viewModel.TargetLanguage);
        SetThemeMode(_viewModel.ThemeMode);
        SetAccent(_viewModel.Accent);
        SetApplicationLanguage(_viewModel.LanguageMode);
    }

    private void UpdateViewModelFromControls()
    {
        _viewModel.ApiKey = ApiKeyBox.Password.Trim();
        _viewModel.SourceLanguage = GetLanguage(SourceLanguageBox);
        _viewModel.TargetLanguage = GetLanguage(TargetLanguageBox);
    }

    private void UpdateHotkeyCodesSummary()
    {
#if __ANDROID__
        _viewModel.SetHotkeyCodesSummary(HotkeyCaptureDialog.Format(_viewModel.HotkeyCodes));
#else
        _viewModel.SetHotkeyCodesSummary(AppStrings.Get(AppStrings.Keys.NotSet));
#endif
    }
}
