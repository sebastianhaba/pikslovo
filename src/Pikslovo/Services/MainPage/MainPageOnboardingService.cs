namespace Pikslovo;

internal sealed class MainPageOnboardingService
{
    public void Initialize(MainPageViewModel viewModel)
    {
        viewModel.OnboardingSourceLanguage = "ja";
        viewModel.OnboardingTargetLanguage = LanguageCatalog.GetDefaultTargetLanguage();
    }

    public IReadOnlyList<LanguageOption> GetLanguageOptions(bool isSource) =>
        LanguageCatalog.GetOptions(isSource);
}
