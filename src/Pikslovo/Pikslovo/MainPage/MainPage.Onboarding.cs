#if __ANDROID__
using Pikslovo.Droid;
#endif

namespace Pikslovo;

public sealed partial class MainPage
{
    private void InitializeOnboarding()
    {
#if __ANDROID__
        if (_settingsPersistence.HasCompletedOnboarding())
        {
            return;
        }

        _onboardingService.Initialize(_viewModel);
        OnboardingLayout.Visibility = Visibility.Visible;
#endif
    }

    private async void EditOnboardingSourceLanguage_Click(object sender, RoutedEventArgs e) =>
        await EditOnboardingLanguageAsync(isSource: true);

    private async void EditOnboardingTargetLanguage_Click(object sender, RoutedEventArgs e) =>
        await EditOnboardingLanguageAsync(isSource: false);

    private async Task EditOnboardingLanguageAsync(bool isSource)
    {
        var picker = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var selectedLanguage = isSource ? _viewModel.OnboardingSourceLanguage : _viewModel.OnboardingTargetLanguage;

        foreach (var language in _onboardingService.GetLanguageOptions(isSource))
        {
            picker.Items.Add(new ComboBoxItem { Tag = language.Code, Content = language.Label });
        }

        SelectLanguage(picker, selectedLanguage);
        if (!await ShowEditorAsync(AppStrings.Get(isSource ? "Język źródłowy" : "Język docelowy"), picker))
        {
            return;
        }

        if (isSource)
        {
            _viewModel.OnboardingSourceLanguage = GetLanguage(picker);
        }
        else
        {
            _viewModel.OnboardingTargetLanguage = GetLanguage(picker);
        }

    }

    private void ContinueOnboardingLanguage_Click(object sender, RoutedEventArgs e)
    {
        SelectLanguage(SourceLanguageBox, _viewModel.OnboardingSourceLanguage);
        SelectLanguage(TargetLanguageBox, _viewModel.OnboardingTargetLanguage);
        SaveSettings(requireValidTranslationSettings: false);
        ShowOnboardingStep(OnboardingNotificationStep);
    }

    private void RequestOnboardingNotificationPermission_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            _awaitingOnboardingNotificationPermission = true;
            if (_permissionsService.RequestNotificationPermission(activity, out _))
            {
                _awaitingOnboardingNotificationPermission = false;
                ShowOnboardingStep(OnboardingOverlayStep);
                return;
            }

            OnboardingNotificationPermissionButton.IsEnabled = false;
            return;
        }
#endif
        ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
    }

    private void RequestOnboardingOverlayPermission_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is { } activity)
        {
            _permissionsService.RequestOverlayPermission(activity, out _);
        }
#endif
        ShowOnboardingStep(OnboardingApiKeyStep);
    }

    private async void TestOnboardingApiKey_Click(object sender, RoutedEventArgs e) =>
        await TestApiKeyAsync(OnboardingApiKeyBox, OnboardingApiKeyTestButton);

    private void FinishOnboarding_Click(object sender, RoutedEventArgs e)
    {
        ApiKeyBox.Password = OnboardingApiKeyBox.Password.Trim();
        SaveSettings(requireValidTranslationSettings: false);
        _settingsPersistence.CompleteOnboarding();
        OnboardingLayout.Visibility = Visibility.Collapsed;
    }

    private void ShowOnboardingStep(UIElement activeStep)
    {
        OnboardingLanguageStep.Visibility = ReferenceEquals(activeStep, OnboardingLanguageStep) ? Visibility.Visible : Visibility.Collapsed;
        OnboardingNotificationStep.Visibility = ReferenceEquals(activeStep, OnboardingNotificationStep) ? Visibility.Visible : Visibility.Collapsed;
        OnboardingOverlayStep.Visibility = ReferenceEquals(activeStep, OnboardingOverlayStep) ? Visibility.Visible : Visibility.Collapsed;
        OnboardingApiKeyStep.Visibility = ReferenceEquals(activeStep, OnboardingApiKeyStep) ? Visibility.Visible : Visibility.Collapsed;
        OnboardingLanguageFooter.Visibility = ReferenceEquals(activeStep, OnboardingLanguageStep) ? Visibility.Visible : Visibility.Collapsed;
        OnboardingNotificationFooter.Visibility = ReferenceEquals(activeStep, OnboardingNotificationStep) ? Visibility.Visible : Visibility.Collapsed;
        OnboardingOverlayFooter.Visibility = ReferenceEquals(activeStep, OnboardingOverlayStep) ? Visibility.Visible : Visibility.Collapsed;
        OnboardingApiKeyFooter.Visibility = ReferenceEquals(activeStep, OnboardingApiKeyStep) ? Visibility.Visible : Visibility.Collapsed;
    }
#if __ANDROID__
    private void OnNotificationPermissionResult(bool granted) => _ = DispatcherQueue.TryEnqueue(() =>
    {
        if (!_awaitingOnboardingNotificationPermission)
        {
            return;
        }

        _awaitingOnboardingNotificationPermission = false;
        OnboardingNotificationPermissionButton.IsEnabled = true;
        if (granted)
        {
            ShowOnboardingStep(OnboardingOverlayStep);
            return;
        }

        ShowStatus("Uprawnienie nie zostało przyznane. Możesz spróbować ponownie.");
    });
#endif
}
