using System.Globalization;
using Pikslovo.Core;

namespace Pikslovo.Tests;

public sealed class AppStringsTests
{
    [TearDown]
    public void TearDown() => AppStrings.SetLanguageMode(AppLanguageMode.System);

    [Test]
    public void Explicit_language_selection_overrides_the_system_language()
    {
        AppStrings.SetLanguageMode(AppLanguageMode.English);
        AppStrings.Get("Settings").Should().Be("Settings");

        AppStrings.SetLanguageMode(AppLanguageMode.Polish);
        AppStrings.Get("Settings").Should().Be("Ustawienia");
    }

    [Test]
    public void Continue_is_localized_to_polish()
    {
        AppStrings.SetLanguageMode(AppLanguageMode.Polish);

        AppStrings.Get(AppStrings.Keys.Continue).Should().Be("Kontynuuj");
    }

    [Test]
    public void System_language_falls_back_to_english_when_polish_is_not_available()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            AppStrings.SetLanguageMode(AppLanguageMode.System);

            AppStrings.Get("Settings").Should().Be("Settings");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
