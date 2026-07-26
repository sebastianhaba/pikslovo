using System.Text.Json;
using Pikslovo.Core;

namespace Pikslovo.Droid.Services;

internal sealed record SettingsProfile(
    int SchemaVersion,
    OcrProfile Ocr,
    CaptureRegionProfile CaptureRegion,
    FloatingButtonProfile FloatingButton)
{
    public const int CurrentSchemaVersion = 1;

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
            settings.FloatingButton.VerticalPosition));

    public static SettingsProfile Defaults { get; } = new(
        CurrentSchemaVersion,
        new OcrProfile(
            TranslationSettings.DefaultRecognitionConfidence,
            TranslationSettings.DefaultOcrImageScale,
            TranslationSettings.DefaultGroupingPower,
            TranslationSettings.DefaultFontScale,
            false)
        {
            UseJpeg = TranslationSettings.DefaultUseJpegForOcr,
            JpegQuality = TranslationSettings.DefaultOcrJpegQuality,
        },
        new CaptureRegionProfile(false, 0f, 0f, 1f, 1f),
        new FloatingButtonProfile(
            true,
            FloatingButtonSettings.DefaultScale,
            FloatingButtonSettings.DefaultHorizontalPosition,
            FloatingButtonSettings.DefaultVerticalPosition));

    public static Task WriteAsync(Stream stream, SettingsProfile profile, CancellationToken cancellationToken) =>
        JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken).AsTask();

    public static async Task<SettingsProfile> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var profile = await JsonSerializer.DeserializeAsync<SettingsProfile>(stream, JsonOptions, cancellationToken);
        if (profile is null)
        {
            throw new InvalidDataException(AppStrings.Get("Plik nie zawiera konfiguracji."));
        }

        profile.Validate();
        return profile;
    }

    public AndroidAppSettings ApplyTo(AndroidAppSettings settings) => settings with
    {
        Translation = settings.Translation with
        {
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
            FloatingButton.VerticalPosition)
    };

    private void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(AppStrings.Get("Ten plik konfiguracji pochodzi z nieobsługiwanej wersji aplikacji."));
        }

        if (Ocr is null || CaptureRegion is null || FloatingButton is null)
        {
            throw new InvalidDataException(AppStrings.Get("Plik konfiguracji jest niekompletny."));
        }

        if (!IsInRange(Ocr.RecognitionConfidence, 0f, 1f) ||
            !IsInRange(Ocr.ImageScale, 0.25f, 1f) ||
            !IsInRange(Ocr.GroupingPower, TranslationSettings.DefaultGroupingPower, 1f) ||
            !IsInRange(Ocr.FontScale, 1f, 3f) ||
            Ocr.JpegQuality is < TranslationSettings.MinimumOcrJpegQuality or > TranslationSettings.MaximumOcrJpegQuality)
        {
            throw new InvalidDataException(AppStrings.Get("Plik zawiera nieprawidłowe ustawienia OCR lub nakładki."));
        }

        if (!IsInRange(FloatingButton.Scale, 0.5f, 2f) ||
            !IsInRange(FloatingButton.HorizontalPosition, 0f, 1f) ||
            !IsInRange(FloatingButton.VerticalPosition, 0f, 1f))
        {
            throw new InvalidDataException(AppStrings.Get("Plik zawiera nieprawidłowe ustawienia przycisku pływającego."));
        }

        if (CaptureRegion.IsEnabled &&
            (!IsInRange(CaptureRegion.Left, 0f, 0.95f) ||
             !IsInRange(CaptureRegion.Top, 0f, 0.95f) ||
             !IsInRange(CaptureRegion.Right, 0.05f, 1f) ||
             !IsInRange(CaptureRegion.Bottom, 0.05f, 1f) ||
             CaptureRegion.Right - CaptureRegion.Left < 0.05f ||
             CaptureRegion.Bottom - CaptureRegion.Top < 0.05f))
        {
            throw new InvalidDataException(AppStrings.Get("Plik zawiera nieprawidłowy obszar przechwytywania."));
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
