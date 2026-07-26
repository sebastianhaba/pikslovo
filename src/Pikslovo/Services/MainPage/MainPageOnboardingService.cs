using System.Globalization;

namespace Pikslovo;

internal sealed class MainPageOnboardingService
{
    public void Initialize(MainPageViewModel viewModel)
    {
        viewModel.OnboardingSourceLanguage = "ja";
        viewModel.OnboardingTargetLanguage = GetSystemTargetLanguage();
    }

    public (string Code, string Label)[] GetLanguageOptions(bool isSource) =>
        isSource
            ? GetLanguageOptions("ja", "en", "ko", "zh", "de")
            : GetLanguageOptions("pl", "en", "de", "es");

    private static string GetSystemTargetLanguage()
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return language is "pl" or "en" or "de" or "es" ? language : "pl";
    }

    private static (string Code, string Label)[] GetLanguageOptions(params string[] languageCodes) =>
        languageCodes.Select(code => (code, AppStrings.GetLanguageName(code))).ToArray();
}
