namespace Pikslovo.Core;

public interface IOcrProvider
{
    Task<OcrDocument> RecognizeAsync(
        ReadOnlyMemory<byte> imageBytes,
        TranslationSettings settings,
        CancellationToken cancellationToken);
}

public interface ITranslationProvider
{
    Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> sourceTexts,
        TranslationSettings settings,
        CancellationToken cancellationToken);
}
