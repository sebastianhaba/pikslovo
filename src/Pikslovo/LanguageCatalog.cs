using System.Globalization;

namespace Pikslovo;

internal static class LanguageCatalog
{
    private static readonly LanguageOption[] AllTargetLanguages =
    [
        new("af", "Afrikaans"),
        new("sq", "Albanian"),
        new("am", "Amharic"),
        new("ar", "Arabic"),
        new("hy", "Armenian"),
        new("as", "Assamese"),
        new("ay", "Aymara"),
        new("az", "Azerbaijani"),
        new("bm", "Bambara"),
        new("eu", "Basque"),
        new("be", "Belarusian"),
        new("bn", "Bengali"),
        new("bho", "Bhojpuri"),
        new("bs", "Bosnian"),
        new("bg", "Bulgarian"),
        new("ca", "Catalan"),
        new("ceb", "Cebuano"),
        new("zh-CN", "Chinese (Simplified)"),
        new("zh-TW", "Chinese (Traditional)"),
        new("co", "Corsican"),
        new("hr", "Croatian"),
        new("cs", "Czech"),
        new("da", "Danish"),
        new("dv", "Dhivehi"),
        new("doi", "Dogri"),
        new("nl", "Dutch"),
        new("en", "English"),
        new("eo", "Esperanto"),
        new("et", "Estonian"),
        new("ee", "Ewe"),
        new("fil", "Filipino"),
        new("fi", "Finnish"),
        new("fr", "French"),
        new("fy", "Frisian"),
        new("gl", "Galician"),
        new("ka", "Georgian"),
        new("de", "German"),
        new("el", "Greek"),
        new("gn", "Guarani"),
        new("gu", "Gujarati"),
        new("ht", "Haitian Creole"),
        new("ha", "Hausa"),
        new("haw", "Hawaiian"),
        new("he", "Hebrew"),
        new("hi", "Hindi"),
        new("hmn", "Hmong"),
        new("hu", "Hungarian"),
        new("is", "Icelandic"),
        new("ig", "Igbo"),
        new("ilo", "Iloko"),
        new("id", "Indonesian"),
        new("ga", "Irish"),
        new("it", "Italian"),
        new("ja", "Japanese"),
        new("jv", "Javanese"),
        new("kn", "Kannada"),
        new("kk", "Kazakh"),
        new("km", "Khmer"),
        new("rw", "Kinyarwanda"),
        new("gom", "Konkani"),
        new("ko", "Korean"),
        new("kri", "Krio"),
        new("ku", "Kurdish (Kurmanji)"),
        new("ckb", "Kurdish (Sorani)"),
        new("ky", "Kyrgyz"),
        new("lo", "Lao"),
        new("la", "Latin"),
        new("lv", "Latvian"),
        new("ln", "Lingala"),
        new("lt", "Lithuanian"),
        new("lg", "Luganda"),
        new("lb", "Luxembourgish"),
        new("mk", "Macedonian"),
        new("mai", "Maithili"),
        new("mg", "Malagasy"),
        new("ms", "Malay"),
        new("ml", "Malayalam"),
        new("mt", "Maltese"),
        new("mi", "Maori"),
        new("mr", "Marathi"),
        new("mni-Mtei", "Meiteilon (Manipuri)"),
        new("lus", "Mizo"),
        new("mn", "Mongolian"),
        new("my", "Myanmar (Burmese)"),
        new("ne", "Nepali"),
        new("no", "Norwegian"),
        new("ny", "Nyanja"),
        new("or", "Odia (Oriya)"),
        new("om", "Oromo"),
        new("ps", "Pashto"),
        new("fa", "Persian"),
        new("pl", "Polish"),
        new("pt", "Portuguese"),
        new("pa", "Punjabi"),
        new("qu", "Quechua"),
        new("ro", "Romanian"),
        new("ru", "Russian"),
        new("sm", "Samoan"),
        new("sa", "Sanskrit"),
        new("gd", "Scots Gaelic"),
        new("nso", "Sepedi"),
        new("sr", "Serbian"),
        new("st", "Sesotho"),
        new("sn", "Shona"),
        new("sd", "Sindhi"),
        new("si", "Sinhala"),
        new("sk", "Slovak"),
        new("sl", "Slovenian"),
        new("so", "Somali"),
        new("es", "Spanish"),
        new("su", "Sundanese"),
        new("sw", "Swahili"),
        new("sv", "Swedish"),
        new("tg", "Tajik"),
        new("ta", "Tamil"),
        new("tt", "Tatar"),
        new("te", "Telugu"),
        new("th", "Thai"),
        new("ti", "Tigrinya"),
        new("ts", "Tsonga"),
        new("tr", "Turkish"),
        new("tk", "Turkmen"),
        new("ak", "Twi"),
        new("uk", "Ukrainian"),
        new("ur", "Urdu"),
        new("ug", "Uyghur"),
        new("uz", "Uzbek"),
        new("vi", "Vietnamese"),
        new("cy", "Welsh"),
        new("xh", "Xhosa"),
        new("yi", "Yiddish"),
        new("yo", "Yoruba"),
        new("zu", "Zulu"),
    ];

    private static readonly HashSet<string> SourceLanguageCodes =
    [
        "af", "sq", "ar", "hy", "as", "az", "eu", "be", "bn", "bho", "bs", "bg", "ca", "ceb", "zh-CN", "zh-TW",
        "hr", "cs", "da", "doi", "nl", "en", "et", "fil", "fi", "fr", "gl", "de", "el", "gu", "he", "hi", "hu",
        "is", "id", "ga", "it", "ja", "kn", "kk", "km", "gom", "ko", "ky", "lo", "lv", "lt", "mk", "mai", "ms",
        "ml", "mr", "mni-Mtei", "lus", "mn", "my", "ne", "no", "or", "fa", "pl", "pt", "pa", "ro", "ru", "sa",
        "sr", "sd", "si", "sk", "sl", "es", "su", "sw", "sv", "tg", "ta", "tt", "te", "th", "ti", "tr", "uk",
        "ur", "uz", "vi", "cy", "xh", "yi", "zu",
    ];

    private static readonly IReadOnlyDictionary<string, LanguageOption> AllByCode =
        AllTargetLanguages.ToDictionary(option => option.Code, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<LanguageOption> GetOptions(bool isSource) =>
        isSource
            ? AllTargetLanguages.Where(option => SourceLanguageCodes.Contains(option.Code)).ToArray()
            : AllTargetLanguages;

    public static string GetDisplayName(string languageCode)
    {
        var normalized = NormalizeCode(languageCode);
        return AllByCode.TryGetValue(normalized, out var option)
            ? option.Name
            : normalized;
    }

    public static string NormalizeCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return string.Empty;
        }

        var normalized = languageCode.Trim();
        return normalized switch
        {
            "zh" => "zh-CN",
            "iw" => "he",
            "nb" or "nn" => "no",
            _ => normalized
        };
    }

    public static string GetDefaultTargetLanguage()
    {
        var culture = CultureInfo.CurrentUICulture;
        var cultureName = culture.Name;
        if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return cultureName.Contains("TW", StringComparison.OrdinalIgnoreCase) ||
                cultureName.Contains("HK", StringComparison.OrdinalIgnoreCase) ||
                cultureName.Contains("MO", StringComparison.OrdinalIgnoreCase) ||
                cultureName.Contains("Hant", StringComparison.OrdinalIgnoreCase)
                ? "zh-TW"
                : "zh-CN";
        }

        var normalized = NormalizeCode(culture.TwoLetterISOLanguageName);
        return AllByCode.ContainsKey(normalized) ? normalized : "en";
    }
}

internal sealed record LanguageOption(string Code, string Name);
