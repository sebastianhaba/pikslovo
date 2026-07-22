using GameTranslator.Core;
using GameTranslator.Services;
using System.Net;
using System.Text;

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

    [Test]
    public async Task TranslateAsync_hides_identical_translations_when_enabled()
    {
        var ocr = new StubOcrProvider(
            new OcrDocument(
            [
                new TextRegion("START", new PixelRect(10, 20, 120, 55)),
                new TextRegion("Options", new PixelRect(20, 90, 140, 125)),
            ]));
        var translator = new StubTranslationProvider([" start ", "Opcje"]);
        var orchestrator = new TranslationOrchestrator(ocr, translator);

        var result = await orchestrator.TranslateAsync(
            new byte[] { 1 },
            new TranslationSettings("key", "en", "pl", HideIdenticalTranslations: true),
            CancellationToken.None);

        result!.Regions.Should().Equal(
            new TranslatedRegion("Options", "Opcje", new PixelRect(20, 90, 140, 125)));
    }

    [Test]
    public async Task TranslateAsync_keeps_identical_translations_when_disabled()
    {
        var ocr = new StubOcrProvider(new OcrDocument([new TextRegion("Start", new PixelRect(10, 20, 120, 55))]));
        var translator = new StubTranslationProvider(["Start"]);
        var orchestrator = new TranslationOrchestrator(ocr, translator);

        var result = await orchestrator.TranslateAsync(
            new byte[] { 1 },
            new TranslationSettings("key", "en", "pl"),
            CancellationToken.None);

        result!.Regions.Should().ContainSingle()
            .Which.Should().Be(new TranslatedRegion("Start", "Start", new PixelRect(10, 20, 120, 55)));
    }

    [Test]
    public async Task TranslateAsync_passes_recognition_confidence_to_ocr_provider()
    {
        var ocr = new StubOcrProvider(new OcrDocument([]));
        var orchestrator = new TranslationOrchestrator(ocr, new StubTranslationProvider([]));
        var settings = new TranslationSettings("key", "en", "pl", 0.25f);

        await orchestrator.TranslateAsync(new byte[] { 1 }, settings, CancellationToken.None);

        ocr.RequestedSettings.Should().Be(settings);
    }

    [TestCase(1f, true)]
    [TestCase(3f, true)]
    [TestCase(0.9f, false)]
    [TestCase(3.1f, false)]
    public void TranslationSettings_validates_font_scale_range(float fontScale, bool isValid)
    {
        var settings = new TranslationSettings("key", "en", "pl", FontScale: fontScale);

        settings.IsValid.Should().Be(isValid);
    }

    [Test]
    public async Task GoogleVisionOcrProvider_filters_paragraphs_below_recognition_confidence()
    {
        const string responseJson = """
            {
              "responses": [
                {
                  "fullTextAnnotation": {
                    "pages": [
                      {
                        "blocks": [
                          {
                            "paragraphs": [
                              {
                                "confidence": 0.6,
                                "boundingBox": { "vertices": [{ "x": 1, "y": 2 }, { "x": 30, "y": 20 }] },
                                "words": [{ "symbols": [{ "text": "Keep" }] }]
                              },
                              {
                                "confidence": 0.59,
                                "boundingBox": { "vertices": [{ "x": 40, "y": 2 }, { "x": 70, "y": 20 }] },
                                "words": [{ "symbols": [{ "text": "Drop" }] }]
                              }
                            ]
                          }
                        ]
                      }
                    ]
                  }
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(responseJson);
        using var httpClient = new HttpClient(handler);
        var provider = new GoogleVisionOcrProvider(httpClient);

        var document = await provider.RecognizeAsync(
            new byte[] { 1 },
            new TranslationSettings("key", "en", "pl"),
            CancellationToken.None);

        document.Regions.Should().ContainSingle()
            .Which.Should().Be(new TextRegion("Keep", new PixelRect(1, 2, 30, 20)));
        handler.RequestBody.Should().Contain("DOCUMENT_TEXT_DETECTION");
    }

    private sealed class StubOcrProvider(OcrDocument document) : IOcrProvider
    {
        public TranslationSettings? RequestedSettings { get; private set; }

        public Task<OcrDocument> RecognizeAsync(
            ReadOnlyMemory<byte> imageBytes,
            TranslationSettings settings,
            CancellationToken cancellationToken)
        {
            RequestedSettings = settings;
            return Task.FromResult(document);
        }
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

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
