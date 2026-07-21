namespace GameTranslator.Core;

public interface IOcrProvider
{
    Task<OcrDocument> RecognizeAsync(ReadOnlyMemory<byte> imageBytes, string apiKey, CancellationToken cancellationToken);
}

public interface ITranslationProvider
{
    Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> sourceTexts,
        TranslationSettings settings,
        CancellationToken cancellationToken);
}
