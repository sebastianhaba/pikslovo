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
            ? [("ja", "Japoński"), ("en", "Angielski"), ("ko", "Koreański"), ("zh", "Chiński (uproszczony)"), ("de", "Niemiecki")]
            : [("pl", "Polski"), ("en", "Angielski"), ("de", "Niemiecki"), ("es", "Hiszpański")];

    public string GetLanguageName(string language) => AppStrings.Get(language switch
    {
        "ja" => "Japoński",
        "en" => "Angielski",
        "ko" => "Koreański",
        "zh" => "Chiński (uproszczony)",
        "de" => "Niemiecki",
        "es" => "Hiszpański",
        _ => "Polski"
    });

    private static string GetSystemTargetLanguage()
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return language is "pl" or "en" or "de" or "es" ? language : "pl";
    }
}
