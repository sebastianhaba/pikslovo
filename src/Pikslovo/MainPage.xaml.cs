using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.Mvvm.Input;
using Pikslovo.Controls;
using Pikslovo.Core;
using Pikslovo.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;

#if __ANDROID__
using Pikslovo.Droid;
using Pikslovo.Droid.Services;
using AndroidToast = Android.Widget.Toast;
using AndroidToastLength = Android.Widget.ToastLength;
#endif

namespace Pikslovo;

public sealed partial class MainPage : Page
{
    private static string ApiKeyTestButtonText => AppStrings.Get(AppStrings.Keys.CheckKey);
    private static string ApiKeyValidationInProgressButtonText => AppStrings.Get(AppStrings.Keys.Checking);

    private readonly MainPageViewModel _viewModel = new();
    private readonly MainPageSettingsPersistenceService _settingsPersistence = new();
    private readonly MainPageOnboardingService _onboardingService = new();
    private readonly MainPagePermissionsService _permissionsService = new();
    private readonly MainPageDiagnosticsService _diagnosticsService = new();
    private bool _isLoading;
#if __ANDROID__
    private bool _updatingSessionToggle;
    private bool _awaitingOnboardingNotificationPermission;
    private bool _updatingGlobalHotkeyToggle;
    private FloatingTranslationTrigger? _floatingButtonPreview;
#endif

    public MainPage()
    {
        _isLoading = true;
        ConfigureCommands();
        InitializeComponent();
        PopulateLanguageSelectors();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        AppVersionText.Text = AppMetadata.DisplayVersionLabel;
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
            SelectLanguage(TargetLanguageBox, LanguageCatalog.GetDefaultTargetLanguage());
            ShowStatus(AppStrings.Format(AppStrings.Keys.SettingsReadFailed, exception.Message));
        }
        finally
        {
            _isLoading = false;
        }

        InitializeOnboarding();
        LocalizeXamlStrings(this);
    }

    private void ConfigureCommands()
    {
        _viewModel.BackCommand = new RelayCommand(Back);
        _viewModel.OpenSectionCommand = new RelayCommand<string>(OpenSection);
        _viewModel.ExportSettingsCommand = new RelayCommand(ExportSettings);
        _viewModel.ImportSettingsCommand = new RelayCommand(ImportSettings);
        _viewModel.RestoreDefaultSettingsCommand = new RelayCommand(async () => await RestoreDefaultSettingsAsync());
        _viewModel.ExportDiagnosticsCommand = new RelayCommand(async () => await ExportDiagnostics());
        _viewModel.OpenGitHubPageCommand = new RelayCommand(OpenGitHubPage);
        _viewModel.OpenWikiPageCommand = new RelayCommand(OpenWikiPage);
        _viewModel.OpenSupportPageCommand = new RelayCommand(OpenSupportPage);
        _viewModel.EditSourceLanguageCommand = new RelayCommand(async () => await EditSourceLanguageAsync());
        _viewModel.EditTargetLanguageCommand = new RelayCommand(async () => await EditTargetLanguageAsync());
        _viewModel.OpenGoogleCloudApiKeyGuideCommand = new RelayCommand(OpenGoogleCloudApiKeyGuide);
        _viewModel.OpenAccessibilitySettingsCommand = new RelayCommand(OpenAccessibilitySettings);
        _viewModel.EditHotkeyCodeCommand = new RelayCommand(async () => await EditHotkeyCodeAsync());
        _viewModel.OpenHotkeyBlockedHelpCommand = new RelayCommand(OpenHotkeyBlockedHelpPage);
        _viewModel.RequestOverlayPermissionCommand = new RelayCommand(RequestOverlayPermission);
        _viewModel.RequestNotificationPermissionCommand = new RelayCommand(RequestNotificationPermission);
        _viewModel.TestApiKeyCommand = new RelayCommand(async () => await TestApiKeyMainAsync());
        _viewModel.SelectThemeModeCommand = new RelayCommand<string>(SelectThemeMode);
        _viewModel.SelectAccentCommand = new RelayCommand<string>(SelectAccent);
        _viewModel.SelectApplicationLanguageCommand = new RelayCommand<string>(SelectApplicationLanguage);
        _viewModel.EditOnboardingSourceLanguageCommand = new RelayCommand(async () => await EditOnboardingSourceLanguageAsync());
        _viewModel.EditOnboardingTargetLanguageCommand = new RelayCommand(async () => await EditOnboardingTargetLanguageAsync());
        _viewModel.ContinueOnboardingLanguageCommand = new RelayCommand(ContinueOnboardingLanguage);
        _viewModel.RequestOnboardingNotificationPermissionCommand = new RelayCommand(RequestOnboardingNotificationPermission);
        _viewModel.RequestOnboardingOverlayPermissionCommand = new RelayCommand(RequestOnboardingOverlayPermission);
        _viewModel.TestOnboardingApiKeyCommand = new RelayCommand(async () => await TestOnboardingApiKeyAsync());
        _viewModel.FinishOnboardingCommand = new RelayCommand(FinishOnboarding);
    }

    private void OpenSection(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        DetailTitle.Text = AppStrings.Get(section switch
        {
            "translation" => AppStrings.Keys.Translation,
            "api" => AppStrings.Keys.GoogleCloudApi,
            "appTheme" => AppStrings.Keys.AppAppearance,
            "recognition" => AppStrings.Keys.TextProcessing,
            "triggers" => AppStrings.Keys.GlobalHotkey,
            "floatingButton" => AppStrings.Keys.FloatingButton,
            "permissions" => AppStrings.Keys.Permissions,
            "diagnostics" => AppStrings.Keys.Diagnostics,
            _ => string.Empty
        });

        TranslationSection.Visibility = section == "translation" ? Visibility.Visible : Visibility.Collapsed;
        ApiSection.Visibility = section == "api" ? Visibility.Visible : Visibility.Collapsed;
        ApiKeyTestFooter.Visibility = section == "api" ? Visibility.Visible : Visibility.Collapsed;
        RecognitionSection.Visibility = section == "recognition" ? Visibility.Visible : Visibility.Collapsed;
        TriggersSection.Visibility = section == "triggers" ? Visibility.Visible : Visibility.Collapsed;
        FloatingButtonSection.Visibility = section == "floatingButton" ? Visibility.Visible : Visibility.Collapsed;
        PermissionsSection.Visibility = section == "permissions" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsSection.Visibility = section == "diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        ThemeSection.Visibility = section == "appTheme" ? Visibility.Visible : Visibility.Collapsed;
        HomeHeader.Visibility = Visibility.Collapsed;
        DetailHeader.Visibility = Visibility.Visible;
        HomeView.Visibility = Visibility.Collapsed;
        DetailLayout.Visibility = Visibility.Visible;
        DetailView.ChangeView(null, 0, null, true);
        RefreshAccessibilityPermissionState();
        UpdateDiagnostics();
        UpdateFloatingButtonPreview();
    }

    private void Back()
    {
        HomeHeader.Visibility = Visibility.Visible;
        DetailHeader.Visibility = Visibility.Collapsed;
        HomeView.Visibility = Visibility.Visible;
        DetailLayout.Visibility = Visibility.Collapsed;
        ApiKeyTestFooter.Visibility = Visibility.Collapsed;
        HomeView.ChangeView(null, 0, null, true);
        DismissFloatingButtonPreview();
    }

    private async Task EditSourceLanguageAsync() => await EditLanguageAsync(SourceLanguageBox, AppStrings.Get(AppStrings.Keys.SourceLanguage));

    private async Task EditTargetLanguageAsync() => await EditLanguageAsync(TargetLanguageBox, AppStrings.Get(AppStrings.Keys.TargetLanguage));

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        SaveSettings(requireValidTranslationSettings: false);
        if (ReferenceEquals(sender, GlobalHotkeyToggle))
        {
            RefreshFloatingButtonConfiguration();
        }
        else
        {
            UpdateFloatingButtonPreview();
        }
    }

    private void SelectThemeMode(string? value)
    {
        if (_isLoading || !Enum.TryParse<AppThemeMode>(value, out var mode))
        {
            return;
        }

        SetThemeMode(mode);
        SaveSettings(requireValidTranslationSettings: false);
    }

    private void SelectAccent(string? value)
    {
        if (_isLoading || !Enum.TryParse<AppAccent>(value, out var accent))
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
    }

    private void SelectApplicationLanguage(string? value)
    {
        if (_isLoading || !Enum.TryParse<AppLanguageMode>(value, out var languageMode))
        {
            return;
        }

        SetApplicationLanguage(languageMode);
        if (!SaveSettings(requireValidTranslationSettings: false))
        {
            return;
        }

        AppStrings.SetLanguageMode(languageMode);
#if __ANDROID__
        AndroidTranslationHost.RefreshFloatingTriggerConfiguration(global::Android.App.Application.Context!);
#endif
        (global::Microsoft.UI.Xaml.Application.Current as App)?.ReloadMainPage();
    }

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
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading || !MainPageViewModel.IsAutoPersistedProperty(e.PropertyName))
        {
            return;
        }

        SaveSettings(requireValidTranslationSettings: false);
        if (MainPageViewModel.RequiresFloatingButtonRefresh(e.PropertyName))
        {
            RefreshFloatingButtonConfiguration();
            return;
        }

        UpdateFloatingButtonPreview();
    }

    private void OpenGitHubPage() =>
        OpenSupportWebPage("https://github.com/sebastianhaba/pikslovo", AppStrings.Keys.OpenPikslovoPageFailed);

    private void OpenWikiPage() =>
        OpenSupportWebPage("https://github.com/sebastianhaba/pikslovo/wiki", AppStrings.Keys.OpenPikslovoPageFailed);

    private void OpenGoogleCloudApiKeyGuide() =>
        OpenSupportWebPage(
            "https://github.com/sebastianhaba/pikslovo/wiki/Set-up-a-Google-Cloud-API-key",
            AppStrings.Keys.OpenPikslovoPageFailed);

    private void OpenSupportPage() =>
        OpenSupportWebPage("https://ko-fi.com/pikslovo", AppStrings.Keys.OpenSupportPageFailed);

    private void OpenHotkeyBlockedHelpPage() =>
        OpenSupportWebPage(
            "https://github.com/sebastianhaba/pikslovo/wiki/Enable-accessibility-for-the-global-hotkey",
            AppStrings.Keys.OpenSupportPageFailed);

    private void OpenSupportWebPage(string url, string errorMessage)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is not { } activity)
        {
            ShowStatus(AppStrings.Keys.AndroidActivityNotReady);
            return;
        }

        try
        {
            AndroidTranslationHost.OpenWebPage(activity, url);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format(errorMessage, exception.Message));
        }
#endif
    }

    private void ApiKeyInput_PasswordSubmitted(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != global::Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        ApiKeyTestButton.Focus(FocusState.Programmatic);
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            AndroidTranslationHost.HideKeyboard(activity);
        }
#endif
    }

    private void OnboardingApiKeyInput_PasswordSubmitted(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != global::Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        OnboardingApiKeyTestButton.Focus(FocusState.Programmatic);
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            AndroidTranslationHost.HideKeyboard(activity);
        }
#endif
    }

    private async Task TestApiKeyMainAsync() => await TestApiKeyAsync(ApiKeyInput, ApiKeyTestButton);

    private async Task TestApiKeyAsync(ApiKeyInputControl apiKeyInput, Button button)
    {
        var apiKey = apiKeyInput.Password.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await ShowMessageAsync(
                AppStrings.Keys.MissingApiKeyTitle,
                AppStrings.Keys.MissingApiKeyMessage);
            return;
        }

        button.IsEnabled = false;
        button.Content = ApiKeyValidationInProgressButtonText;
        var stopwatch = Stopwatch.StartNew();
        string? errorTitle = null;
        string? errorMessage = null;
        try
        {
            await AppServices.GoogleCloudApiKeyValidator.ValidateAsync(apiKey, CancellationToken.None);
        }
        catch (GoogleCloudApiKeyValidationException exception)
        {
            errorTitle = AppStrings.Keys.KeyDoesNotWorkTitle;
            errorMessage = exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? AppStrings.Format(AppStrings.Keys.ApiKeyNoAccess, exception.ServiceName)
                : AppStrings.Format(AppStrings.Keys.ApiKeyCheckFailed, exception.ServiceName, (int)exception.StatusCode);
        }
        catch (HttpRequestException)
        {
            errorTitle = AppStrings.Keys.NoConnectionTitle;
            errorMessage = AppStrings.Keys.NoConnectionMessage;
        }
        catch (TaskCanceledException)
        {
            errorTitle = AppStrings.Keys.TimeoutTitle;
            errorMessage = AppStrings.Keys.TimeoutMessage;
        }
        finally
        {
            AppServices.Diagnostics.RecordApiKeyValidation(stopwatch.ElapsedMilliseconds);
            button.IsEnabled = true;
            button.Content = ApiKeyTestButtonText;
        }

        if (errorTitle is not null)
        {
            await ShowMessageAsync(errorTitle, errorMessage!);
            return;
        }

        await ShowMessageAsync(
            AppStrings.Keys.KeyWorksTitle,
            AppStrings.Keys.KeyWorksMessage);
    }

    private async Task EditHotkeyCodeAsync()
    {
#if __ANDROID__
        RefreshAccessibilityPermissionState();
        if (!_viewModel.HasAccessibilityPermission)
        {
            ShowStatus(AppStrings.Keys.EnableTheSystemServiceForTheGlobalHotkey);
            return;
        }

        if (MainActivity.CurrentActivity is not { } activity)
        {
            ShowStatus(AppStrings.Keys.AndroidActivityNotReady);
            return;
        }

        var hotkeyCodes = await HotkeyCaptureDialog.ShowAsync(activity);
        if (hotkeyCodes is { Length: > 0 })
        {
            _viewModel.HotkeyCodes = hotkeyCodes;
            _viewModel.GlobalHotkeyEnabled = true;
            ApplyGlobalHotkeyState();
        }
        else if (!_viewModel.GlobalHotkeyEnabled)
        {
            ClearGlobalHotkey();
        }
        else
        {
            SyncGlobalHotkeyToggle();
            UpdateHotkeyCodesSummary();
        }
#endif
    }

    private async void GlobalHotkeyToggle_Toggled(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (_isLoading || _updatingGlobalHotkeyToggle)
        {
            return;
        }

        RefreshAccessibilityPermissionState();
        if (!_viewModel.HasAccessibilityPermission)
        {
            _viewModel.GlobalHotkeyEnabled = false;
            SyncGlobalHotkeyToggle();
            ShowStatus(AppStrings.Keys.EnableTheSystemServiceForTheGlobalHotkey);
            return;
        }

        if (GlobalHotkeyToggle.IsOn)
        {
            await EditHotkeyCodeAsync();
            return;
        }

        ClearGlobalHotkey();
#endif
    }

    private void ApplyGlobalHotkeyState()
    {
        UpdateHotkeyCodesSummary();
        SyncGlobalHotkeyToggle();
        SaveSettings(requireValidTranslationSettings: false);
        RefreshFloatingButtonConfiguration();
    }

    private void ClearGlobalHotkey()
    {
        _viewModel.HotkeyCodes = [];
        _viewModel.GlobalHotkeyEnabled = false;
        ApplyGlobalHotkeyState();
    }

    private void SyncGlobalHotkeyToggle()
    {
#if __ANDROID__
        _updatingGlobalHotkeyToggle = true;
        try
        {
            GlobalHotkeyToggle.IsOn = _viewModel.GlobalHotkeyEnabled;
        }
        finally
        {
            _updatingGlobalHotkeyToggle = false;
        }
#endif
    }

    private void OnActivityResumed()
    {
#if __ANDROID__
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            RefreshAccessibilityPermissionState();
            SyncGlobalHotkeyToggle();
        });
#endif
    }

    private async Task<bool> ShowEditorAsync(string title, object content)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = AppStrings.Get(AppStrings.Keys.Save),
            CloseButtonText = AppStrings.Get(AppStrings.Keys.Cancel),
            DefaultButton = ContentDialogButton.Primary
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = AppStrings.Get(title),
            Content = new TextBlock { Text = AppStrings.Get(message), TextWrapping = TextWrapping.Wrap },
            CloseButtonText = AppStrings.Get(AppStrings.Keys.Close)
        };

        await dialog.ShowAsync();
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = AppStrings.Get(title),
            Content = new TextBlock { Text = AppStrings.Get(message), TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = AppStrings.Get(AppStrings.Keys.Restore),
            CloseButtonText = AppStrings.Get(AppStrings.Keys.Cancel),
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void SessionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading || IsSessionToggleUpdating())
        {
            return;
        }

#if __ANDROID__
        var activity = MainActivity.CurrentActivity;
        if (activity is null)
        {
            ShowStatus(AppStrings.Keys.AndroidActivityNotReadyYet);
            UpdateSessionToggle();
            return;
        }

        if (!SessionToggle.IsOn)
        {
            AndroidTranslationHost.StopSession(activity);
            ShowStatus(AppStrings.Keys.TranslatorSessionStopped);
            return;
        }

        if (!SaveSettings(requireValidTranslationSettings: true))
        {
            UpdateSessionToggle();
            return;
        }

        try
        {
            ShowStatus(AppStrings.Keys.OpeningScreenSharingDialog);
            AndroidTranslationHost.RequestSession(activity);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format(AppStrings.Keys.StartScreenCaptureFailed, exception.Message));
            UpdateSessionToggle();
        }
#endif
    }

    private void UpdateSessionToggle()
    {
#if __ANDROID__
        _updatingSessionToggle = true;
        SessionToggle.IsOn = TranslationForegroundService.IsSessionActive;
        _updatingSessionToggle = false;
#endif
    }

    private bool IsSessionToggleUpdating()
    {
#if __ANDROID__
        return _updatingSessionToggle;
#else
        return false;
#endif
    }

    private void ShowStatus(string message)
    {
#if __ANDROID__
        AndroidToast.MakeText(global::Android.App.Application.Context!, AppStrings.Get(message), AndroidToastLength.Short)?.Show();
#endif
    }

    private static string GetLanguage(ComboBox box) =>
        LanguageCatalog.NormalizeCode((box.SelectedItem as ComboBoxItem)?.Tag?.ToString());

    private static void SelectLanguage(ComboBox box, string language)
    {
        var normalized = LanguageCatalog.NormalizeCode(language);
        for (var index = 0; index < box.Items.Count; index++)
        {
            if (box.Items[index] is ComboBoxItem item &&
                string.Equals(LanguageCatalog.NormalizeCode(item.Tag?.ToString()), normalized, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedIndex = index;
                return;
            }
        }

        box.SelectedIndex = 0;
    }

    private void PopulateLanguageSelectors()
    {
        PopulateLanguageSelector(SourceLanguageBox, isSource: true);
        PopulateLanguageSelector(TargetLanguageBox, isSource: false);
    }

    private static void PopulateLanguageSelector(ComboBox box, bool isSource)
    {
        box.Items.Clear();
        foreach (var language in LanguageCatalog.GetOptions(isSource))
        {
            box.Items.Add(new ComboBoxItem
            {
                Tag = language.Code,
                Content = language.Name
            });
        }
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

        if (!_permissionsService.CanDrawOverlays())
        {
            DismissFloatingButtonPreview();
            return;
        }

        var context = global::Android.App.Application.Context!;
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

    private void SetThemeMode(AppThemeMode mode)
    {
        _viewModel.ThemeMode = mode;
        SetThemeModeOptionStyle(SystemThemeOption, mode == AppThemeMode.System);
        SetThemeModeOptionStyle(DarkThemeOption, mode == AppThemeMode.Dark);
        SetThemeModeOptionStyle(LightThemeOption, mode == AppThemeMode.Light);
        (global::Microsoft.UI.Xaml.Application.Current as App)?.SetThemeMode(mode);
    }

    private void SetAccent(AppAccent accent)
    {
        _viewModel.Accent = accent;
        UpdateAccentOptionSelection();
        SetThemeModeOptionStyle(SystemThemeOption, _viewModel.ThemeMode == AppThemeMode.System);
        SetThemeModeOptionStyle(DarkThemeOption, _viewModel.ThemeMode == AppThemeMode.Dark);
        SetThemeModeOptionStyle(LightThemeOption, _viewModel.ThemeMode == AppThemeMode.Light);
        (global::Microsoft.UI.Xaml.Application.Current as App)?.SetAccent(accent);
    }

    private void SetApplicationLanguage(AppLanguageMode mode)
    {
        _viewModel.LanguageMode = mode;
        SetThemeModeOptionStyle(SystemLanguageOption, mode == AppLanguageMode.System);
        SetThemeModeOptionStyle(EnglishLanguageOption, mode == AppLanguageMode.English);
        SetThemeModeOptionStyle(PolishLanguageOption, mode == AppLanguageMode.Polish);
    }

    private void SetThemeModeOptionStyle(Button option, bool selected)
    {
        option.Style = (Style)Resources[selected ? "SelectedThemeModeOptionBorder" : "ThemeModeOptionBorder"];
        if (selected)
        {
            option.BorderBrush = new SolidColorBrush(App.GetAccentColor(_viewModel.Accent));
            return;
        }

        option.BorderBrush = new SolidColorBrush(GetThemeOptionBorderColor());
    }

    private global::Windows.UI.Color GetThemeOptionBorderColor()
    {
        var isDark = _viewModel.ThemeMode == AppThemeMode.Dark ||
            (_viewModel.ThemeMode == AppThemeMode.System && ActualTheme == ElementTheme.Dark);
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

    private void SetAccentOptionSelection(Button option, AppAccent accent) =>
        option.BorderThickness = accent == _viewModel.Accent ? new Thickness(2) : new Thickness(0);

    private static string FormatFontScale(double value) => $"{value.ToString("0.0", CultureInfo.CurrentCulture)}x";

    private static void LocalizeXamlStrings(DependencyObject element)
    {
        switch (element)
        {
            case TextBlock textBlock:
                if (AppStrings.HasKey(textBlock.Text)) textBlock.Text = AppStrings.Get(textBlock.Text ?? string.Empty);
                break;
            case Button { Content: string content } button:
                if (AppStrings.HasKey(content)) button.Content = AppStrings.Get(content);
                break;
            case ComboBoxItem { Content: string content } item:
                if (AppStrings.HasKey(content)) item.Content = AppStrings.Get(content);
                break;
            case PasswordBox passwordBox:
                if (AppStrings.HasKey(passwordBox.PlaceholderText)) passwordBox.PlaceholderText = AppStrings.Get(passwordBox.PlaceholderText ?? string.Empty);
                break;
        }

        if (ToolTipService.GetToolTip(element) is string toolTip)
        {
            if (AppStrings.HasKey(toolTip)) ToolTipService.SetToolTip(element, AppStrings.Get(toolTip));
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            LocalizeXamlStrings(VisualTreeHelper.GetChild(element, index));
        }
    }
}
