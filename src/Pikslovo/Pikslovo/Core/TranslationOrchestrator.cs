namespace Pikslovo.Core;

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
        var execution = await TranslateWithTimingsAsync(imageBytes, settings, cancellationToken)
            .ConfigureAwait(false);
        return execution.Result;
    }

    public async Task<TranslationExecution> TranslateWithTimingsAsync(
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
            return new TranslationExecution(null, 0, 0);
        }

        try
        {
            var ocrStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var document = await _ocrProvider
                .RecognizeAsync(imageBytes, settings, cancellationToken)
                .ConfigureAwait(false);
            var ocrMilliseconds = ocrStopwatch.ElapsedMilliseconds;

            var groupedRegions = new TextRegionGrouper(settings.GroupingPower).Group(document.Regions);
            if (groupedRegions.Count == 0)
            {
                return new TranslationExecution(new TranslationResult([]), ocrMilliseconds, 0);
            }

            var sourceTexts = groupedRegions.Select(region => region.Text).ToArray();
            var translationStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var translatedTexts = await _translationProvider
                .TranslateAsync(sourceTexts, settings, cancellationToken)
                .ConfigureAwait(false);
            var translationMilliseconds = translationStopwatch.ElapsedMilliseconds;

            if (translatedTexts.Count != groupedRegions.Count)
            {
                throw new TranslationException("Google Translation zwróciło niepełną odpowiedź.");
            }

            var regions = groupedRegions
                .Select((region, index) => new TranslatedRegion(region.Text, translatedTexts[index], region.Bounds))
                .Where(region => !settings.HideIdenticalTranslations ||
                    !string.Equals(
                        region.SourceText.Trim(),
                        region.TranslatedText.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return new TranslationExecution(new TranslationResult(regions), ocrMilliseconds, translationMilliseconds);
        }
        finally
        {
            _operationLock.Release();
        }
    }
}

public sealed record TranslationExecution(
    TranslationResult? Result,
    long CloudVisionOcrMilliseconds,
    long CloudTranslationMilliseconds);
