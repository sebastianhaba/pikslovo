namespace Pikslovo.Core;

public sealed record TranslationSettings(
    string ApiKey,
    string SourceLanguage,
    string TargetLanguage,
    float RecognitionConfidence = 0.6f,
    float GroupingPower = TextRegionGrouper.DefaultGroupingPower,
    float FontScale = 1f,
    bool HideIdenticalTranslations = false,
    float OcrImageScale = 1f,
    bool UseJpegForOcr = true,
    int OcrJpegQuality = 85)
{
    public const float DefaultRecognitionConfidence = 0.6f;
    public const float DefaultGroupingPower = TextRegionGrouper.DefaultGroupingPower;
    public const float DefaultFontScale = 1f;
    public const float DefaultOcrImageScale = 1f;
    public const bool DefaultUseJpegForOcr = true;
    public const int DefaultOcrJpegQuality = 85;
    public const int MinimumOcrJpegQuality = 50;
    public const int MaximumOcrJpegQuality = 100;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(SourceLanguage) &&
        !string.IsNullOrWhiteSpace(TargetLanguage) &&
        float.IsFinite(RecognitionConfidence) &&
        RecognitionConfidence is >= 0f and <= 1f &&
        float.IsFinite(GroupingPower) &&
        GroupingPower is >= DefaultGroupingPower and <= 1f &&
        float.IsFinite(FontScale) &&
        FontScale is >= 1f and <= 3f &&
        float.IsFinite(OcrImageScale) &&
        OcrImageScale is >= 0.25f and <= 1f &&
        OcrJpegQuality is >= MinimumOcrJpegQuality and <= MaximumOcrJpegQuality;
}

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(1, Right - Left);
    public int Height => Math.Max(1, Bottom - Top);
}

public sealed record TextRegion(string Text, PixelRect Bounds);

public sealed record OcrDocument(IReadOnlyList<TextRegion> Regions);

public sealed record TranslatedRegion(string SourceText, string TranslatedText, PixelRect Bounds);

public sealed record TranslationResult(IReadOnlyList<TranslatedRegion> Regions);

public sealed class TranslationException : Exception
{
    public TranslationException(string message) : base(message)
    {
    }

    public TranslationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
