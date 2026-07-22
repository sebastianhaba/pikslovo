namespace GameTranslator.Core;

public sealed class TranslationOrchestrator
{
    private readonly IOcrProvider _ocrProvider;
    private readonly ITranslationProvider _translationProvider;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public TranslationOrchestrator(IOcrProvider ocrProvider, ITranslationProvider translationProvider)
    {
        _ocrProvider = ocrProvider;
        _translationProvider = translationProvider;
    }

    public async Task<TranslationResult?> TranslateAsync(
        ReadOnlyMemory<byte> imageBytes,
        TranslationSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.IsValid)
        {
            throw new TranslationException("Uzupełnij klucz API oraz oba języki przed uruchomieniem tłumaczenia.");
        }

        if (!await _operationLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            var document = await _ocrProvider
                .RecognizeAsync(imageBytes, settings, cancellationToken)
                .ConfigureAwait(false);

            if (document.Regions.Count == 0)
            {
                return new TranslationResult([]);
            }

            var sourceTexts = document.Regions.Select(region => region.Text).ToArray();
            var translatedTexts = await _translationProvider
                .TranslateAsync(sourceTexts, settings, cancellationToken)
                .ConfigureAwait(false);

            if (translatedTexts.Count != document.Regions.Count)
            {
                throw new TranslationException("Google Translation zwróciło niepełną odpowiedź.");
            }

            var regions = document.Regions
                .Select((region, index) => new TranslatedRegion(region.Text, translatedTexts[index], region.Bounds))
                .ToArray();

            return new TranslationResult(regions);
        }
        finally
        {
            _operationLock.Release();
        }
    }
}
