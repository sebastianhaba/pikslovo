using GameTranslator.Core;

namespace GameTranslator.Tests;

public sealed class TranslationOrchestratorTests
{
    [Test]
    public async Task TranslateAsync_preserves_ocr_region_bounds_and_order()
    {
        var ocr = new StubOcrProvider(
            new OcrDocument(
            [
                new TextRegion("Start game", new PixelRect(10, 20, 120, 55)),
                new TextRegion("Options", new PixelRect(20, 90, 140, 125)),
            ]));
        var translator = new StubTranslationProvider(["Rozpocznij gre", "Opcje"]);
        var orchestrator = new TranslationOrchestrator(ocr, translator);

        var result = await orchestrator.TranslateAsync(
            new byte[] { 1, 2, 3 },
            new TranslationSettings("key", "en", "pl"),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Regions.Should().BeEquivalentTo(
        [
            new TranslatedRegion("Start game", "Rozpocznij gre", new PixelRect(10, 20, 120, 55)),
            new TranslatedRegion("Options", "Opcje", new PixelRect(20, 90, 140, 125)),
        ], options => options.WithStrictOrdering());
        translator.RequestedSourceTexts.Should().Equal("Start game", "Options");
    }

    [Test]
    public async Task TranslateAsync_rejects_missing_configuration_before_calling_providers()
    {
        var ocr = new StubOcrProvider(new OcrDocument([]));
        var translator = new StubTranslationProvider([]);
        var orchestrator = new TranslationOrchestrator(ocr, translator);

        var action = () => orchestrator.TranslateAsync(
            new byte[] { 1 },
            new TranslationSettings("", "en", "pl"),
            CancellationToken.None);

        await action.Should().ThrowAsync<TranslationException>();
    }

    private sealed class StubOcrProvider(OcrDocument document) : IOcrProvider
    {
        public Task<OcrDocument> RecognizeAsync(ReadOnlyMemory<byte> imageBytes, string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult(document);
    }

    private sealed class StubTranslationProvider(IReadOnlyList<string> translations) : ITranslationProvider
    {
        public IReadOnlyList<string> RequestedSourceTexts { get; private set; } = [];

        public Task<IReadOnlyList<string>> TranslateAsync(
            IReadOnlyList<string> sourceTexts,
            TranslationSettings settings,
            CancellationToken cancellationToken)
        {
            RequestedSourceTexts = sourceTexts;
            return Task.FromResult(translations);
        }
    }
}
