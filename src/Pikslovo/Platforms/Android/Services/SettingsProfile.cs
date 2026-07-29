using System.Text.Json;
using Pikslovo.Core;

namespace Pikslovo.Droid.Services;

internal sealed record SettingsProfile(
    int SchemaVersion,
    OcrProfile Ocr,
    CaptureRegionProfile CaptureRegion,
    FloatingButtonProfile FloatingButton,
    TranslationProfile? Translation = null,
    GlobalHotkeyProfile? GlobalHotkey = null,
    AppearanceProfile? Appearance = null)
{
    public const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static SettingsProfile FromSettings(AndroidAppSettings settings) => new(
        CurrentSchemaVersion,
        new OcrProfile(
            settings.Translation.RecognitionConfidence,
            settings.Translation.OcrImageScale,
            settings.Translation.GroupingPower,
            settings.Translation.FontScale,
            settings.Translation.HideIdenticalTranslations)
        {
            UseJpeg = settings.Translation.UseJpegForOcr,
            JpegQuality = settings.Translation.OcrJpegQuality,
        },
        new CaptureRegionProfile(
            settings.CaptureRegion.IsEnabled,
            settings.CaptureRegion.Left,
            settings.CaptureRegion.Top,
            settings.CaptureRegion.Right,
            settings.CaptureRegion.Bottom),
        new FloatingButtonProfile(
            settings.FloatingButton.AlwaysVisible,
            settings.FloatingButton.Scale,
            settings.FloatingButton.HorizontalPosition,
            settings.FloatingButton.VerticalPosition),
        new TranslationProfile(
            settings.Translation.SourceLanguage,
            settings.Translation.TargetLanguage),
        new GlobalHotkeyProfile(
            settings.GlobalHotkeyEnabled,
            settings.HotkeyCodes),
        new AppearanceProfile(
            settings.ThemeMode,
            settings.Accent,
            settings.LanguageMode));

    public static SettingsProfile Defaults { get; } = new(
        CurrentSchemaVersion,
        new OcrProfile(
            TranslationSettings.DefaultRecognitionConfidence,
            TranslationSettings.DefaultOcrImageScale,
            TranslationSettings.DefaultGroupingPower,
            TranslationSettings.DefaultFontScale,
            TranslationSettings.DefaultHideIdenticalTranslations)
        {
            UseJpeg = TranslationSettings.DefaultUseJpegForOcr,
            JpegQuality = TranslationSettings.DefaultOcrJpegQuality,
        },
        new CaptureRegionProfile(false, 0f, 0f, 1f, 1f),
        new FloatingButtonProfile(
            true,
            FloatingButtonSettings.DefaultScale,
            FloatingButtonSettings.DefaultHorizontalPosition,
            FloatingButtonSettings.DefaultVerticalPosition),
        new TranslationProfile("ja", "pl"),
        new GlobalHotkeyProfile(false, []),
        new AppearanceProfile(
            AppThemeMode.System,
            AppAccent.Lavender,
            AppLanguageMode.System));

    public static Task WriteAsync(Stream stream, SettingsProfile profile, CancellationToken cancellationToken) =>
        JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken).AsTask();

    public static async Task<SettingsProfile> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var profile = await JsonSerializer.DeserializeAsync<SettingsProfile>(stream, JsonOptions, cancellationToken);
        if (profile is null)
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.ConfigurationFileMissing));
        }

        profile.Validate();
        return profile;
    }

    public AndroidAppSettings ApplyTo(AndroidAppSettings settings) => settings with
    {
        Translation = settings.Translation with
        {
            SourceLanguage = Translation?.SourceLanguage ?? settings.Translation.SourceLanguage,
            TargetLanguage = Translation?.TargetLanguage ?? settings.Translation.TargetLanguage,
            RecognitionConfidence = Ocr.RecognitionConfidence,
            OcrImageScale = Ocr.ImageScale,
            GroupingPower = Ocr.GroupingPower,
            FontScale = Ocr.FontScale,
            HideIdenticalTranslations = Ocr.HideIdenticalTranslations,
            UseJpegForOcr = Ocr.UseJpeg,
            OcrJpegQuality = Ocr.JpegQuality,
        },
        CaptureRegion = new CaptureRegionSettings(
            CaptureRegion.IsEnabled,
            CaptureRegion.Left,
            CaptureRegion.Top,
            CaptureRegion.Right,
            CaptureRegion.Bottom).Normalize(),
        FloatingButton = new FloatingButtonSettings(
            FloatingButton.AlwaysVisible,
            FloatingButton.Scale,
            FloatingButton.HorizontalPosition,
            FloatingButton.VerticalPosition),
        HotkeyCodes = GlobalHotkey?.Codes ?? settings.HotkeyCodes,
        GlobalHotkeyEnabled = GlobalHotkey?.IsEnabled ?? settings.GlobalHotkeyEnabled,
        ThemeMode = Appearance?.ThemeMode ?? settings.ThemeMode,
        Accent = Appearance?.Accent ?? settings.Accent,
        LanguageMode = Appearance?.LanguageMode ?? settings.LanguageMode
    };

    private void Validate()
    {
        if (SchemaVersion is not 1 and not CurrentSchemaVersion)
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.UnsupportedConfigurationVersion));
        }

        if (Ocr is null || CaptureRegion is null || FloatingButton is null)
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.IncompleteConfigurationFile));
        }

        // Version 1 files did not contain these profiles. They remain importable
        // and leave the corresponding local settings unchanged.
        if (SchemaVersion == 1)
        {
            return;
        }

        if (Translation is null || GlobalHotkey is null || Appearance is null)
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.IncompleteConfigurationFile));
        }

        if (string.IsNullOrWhiteSpace(Translation.SourceLanguage) ||
            string.IsNullOrWhiteSpace(Translation.TargetLanguage))
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.InvalidTranslationLanguages));
        }

        if (GlobalHotkey.Codes is null || GlobalHotkey.Codes.Any(code => code <= 0) ||
            GlobalHotkey.Codes.Distinct().Count() != GlobalHotkey.Codes.Length)
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.InvalidGlobalHotkey));
        }

        if (!Enum.IsDefined(Appearance.ThemeMode) ||
            !Enum.IsDefined(Appearance.Accent) ||
            !Enum.IsDefined(Appearance.LanguageMode))
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.InvalidAppearanceSettings));
        }

        if (!IsInRange(Ocr.RecognitionConfidence, 0f, 1f) ||
            !IsInRange(Ocr.ImageScale, 0.25f, 1f) ||
            !IsInRange(Ocr.GroupingPower, TranslationSettings.DefaultGroupingPower, 1f) ||
            !IsInRange(Ocr.FontScale, 1f, 3f) ||
            Ocr.JpegQuality is < TranslationSettings.MinimumOcrJpegQuality or > TranslationSettings.MaximumOcrJpegQuality)
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.InvalidOcrOrOverlaySettings));
        }

        if (!IsInRange(FloatingButton.Scale, 0.5f, 2f) ||
            !IsInRange(FloatingButton.HorizontalPosition, 0f, 1f) ||
            !IsInRange(FloatingButton.VerticalPosition, 0f, 1f))
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.InvalidFloatingButtonSettings));
        }

        if (CaptureRegion.IsEnabled &&
            (!IsInRange(CaptureRegion.Left, 0f, 0.95f) ||
             !IsInRange(CaptureRegion.Top, 0f, 0.95f) ||
             !IsInRange(CaptureRegion.Right, 0.05f, 1f) ||
             !IsInRange(CaptureRegion.Bottom, 0.05f, 1f) ||
             CaptureRegion.Right - CaptureRegion.Left < 0.05f ||
             CaptureRegion.Bottom - CaptureRegion.Top < 0.05f))
        {
            throw new InvalidDataException(AppStrings.Get(AppStrings.Keys.InvalidCaptureRegion));
        }
    }

    private static bool IsInRange(float value, float minimum, float maximum) =>
        float.IsFinite(value) && value >= minimum && value <= maximum;
}

internal sealed record OcrProfile(
    float RecognitionConfidence,
    float ImageScale,
    float GroupingPower,
    float FontScale,
    bool HideIdenticalTranslations)
{
    public bool UseJpeg { get; init; } = TranslationSettings.DefaultUseJpegForOcr;

    public int JpegQuality { get; init; } = TranslationSettings.DefaultOcrJpegQuality;
}

internal sealed record CaptureRegionProfile(
    bool IsEnabled,
    float Left,
    float Top,
    float Right,
    float Bottom);

internal sealed record FloatingButtonProfile(
    bool AlwaysVisible,
    float Scale,
    float HorizontalPosition,
    float VerticalPosition);

internal sealed record TranslationProfile(string SourceLanguage, string TargetLanguage);

internal sealed record GlobalHotkeyProfile(bool IsEnabled, int[] Codes);

internal sealed record AppearanceProfile(
    AppThemeMode ThemeMode,
    AppAccent Accent,
    AppLanguageMode LanguageMode);
