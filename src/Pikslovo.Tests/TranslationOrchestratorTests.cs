using Pikslovo.Core;
using Pikslovo.Services;
using System.Net;
using System.Text;

namespace Pikslovo.Tests;

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
    public async Task TranslateAsync_groups_nearby_lines_before_translation()
    {
        var ocr = new StubOcrProvider(
            new OcrDocument(
            [
                new TextRegion("The door is", new PixelRect(100, 300, 280, 330)),
                new TextRegion("locked.", new PixelRect(102, 334, 230, 364)),
                new TextRegion("Options", new PixelRect(20, 80, 140, 110)),
            ]));
        var translator = new StubTranslationProvider(["Opcje", "Drzwi są\nzamknięte."]);
        var orchestrator = new TranslationOrchestrator(ocr, translator);

        var result = await orchestrator.TranslateAsync(
            new byte[] { 1 },
            new TranslationSettings("key", "en", "pl", HideIdenticalTranslations: false),
            CancellationToken.None);

        translator.RequestedSourceTexts.Should().Equal("Options", "The door is\nlocked.");
        result!.Regions.Should().Equal(
            new TranslatedRegion("Options", "Opcje", new PixelRect(20, 80, 140, 110)),
            new TranslatedRegion("The door is\nlocked.", "Drzwi są\nzamknięte.", new PixelRect(100, 300, 280, 364)));
    }

    [Test]
    public async Task TranslateAsync_uses_configured_grouping_power()
    {
        var ocr = new StubOcrProvider(
            new OcrDocument(
            [
                new TextRegion("First line", new PixelRect(100, 100, 280, 120)),
                new TextRegion("Second line", new PixelRect(100, 145, 280, 165)),
            ]));
        var translator = new StubTranslationProvider(["Połączony dialog"]);
        var orchestrator = new TranslationOrchestrator(ocr, translator);

        var result = await orchestrator.TranslateAsync(
            new byte[] { 1 },
            new TranslationSettings("key", "en", "pl", GroupingPower: 1f),
            CancellationToken.None);

        translator.RequestedSourceTexts.Should().Equal("First line\nSecond line");
        result!.Regions.Should().ContainSingle()
            .Which.Bounds.Should().Be(new PixelRect(100, 100, 280, 165));
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
            new TranslationSettings("key", "en", "pl", HideIdenticalTranslations: false),
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

    [TestCase(0.25f, true)]
    [TestCase(1f, true)]
    [TestCase(0.24f, false)]
    [TestCase(1.01f, false)]
    public void TranslationSettings_validates_grouping_power_range(float groupingPower, bool isValid)
    {
        var settings = new TranslationSettings("key", "en", "pl", GroupingPower: groupingPower);

        settings.IsValid.Should().Be(isValid);
    }

    [TestCase(0.25f, true)]
    [TestCase(1f, true)]
    [TestCase(0.24f, false)]
    [TestCase(1.01f, false)]
    public void TranslationSettings_validates_ocr_image_scale_range(float ocrImageScale, bool isValid)
    {
        var settings = new TranslationSettings("key", "en", "pl", OcrImageScale: ocrImageScale);

        settings.IsValid.Should().Be(isValid);
    }

    [TestCase(50, true)]
    [TestCase(85, true)]
    [TestCase(100, true)]
    [TestCase(49, false)]
    [TestCase(101, false)]
    public void TranslationSettings_validates_ocr_jpeg_quality_range(int jpegQuality, bool isValid)
    {
        var settings = new TranslationSettings("key", "en", "pl", OcrJpegQuality: jpegQuality);

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
        handler.RequestContentLength.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GoogleVisionOcrProvider_preserves_symbol_breaks_and_default_word_spacing()
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
                                "confidence": 1,
                                "boundingBox": { "vertices": [{ "x": 1, "y": 2 }, { "x": 100, "y": 40 }] },
                                "words": [
                                  { "symbols": [{ "text": "H" }, { "text": "i", "property": { "detectedBreak": { "type": "SPACE" } } }] },
                                  { "symbols": [{ "text": "t" }, { "text": "h" }, { "text": "e" }, { "text": "r" }, { "text": "e", "property": { "detectedBreak": { "type": "LINE_BREAK" } } }] },
                                  { "symbols": [{ "text": "G" }, { "text": "o" }] },
                                  { "symbols": [{ "text": "!" }] }
                                ]
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
            .Which.Text.Should().Be("Hi there\nGo!");
    }

    [Test]
    public async Task GoogleCloudApiKeyValidator_checks_translation_and_vision_access()
    {
        var handler = new SequenceHttpMessageHandler(HttpStatusCode.OK, HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var validator = new GoogleCloudApiKeyValidator(httpClient);

        await validator.ValidateAsync("test key", CancellationToken.None);

        handler.RequestUris.Should().Equal(
            "https://translation.googleapis.com/language/translate/v2?key=test key",
            "https://vision.googleapis.com/v1/images:annotate?key=test key");
        handler.RequestBodies[1].Should().Contain("DOCUMENT_TEXT_DETECTION");
    }

    [Test]
    public async Task GoogleCloudApiKeyValidator_identifies_the_service_that_rejected_the_key()
    {
        var handler = new SequenceHttpMessageHandler(HttpStatusCode.OK, HttpStatusCode.Forbidden);
        using var httpClient = new HttpClient(handler);
        var validator = new GoogleCloudApiKeyValidator(httpClient);

        var action = () => validator.ValidateAsync("key", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<GoogleCloudApiKeyValidationException>();
        exception.Which.ServiceName.Should().Be("Cloud Vision API");
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

        public long? RequestContentLength { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestContentLength = request.Content!.Headers.ContentLength;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class SequenceHttpMessageHandler(params HttpStatusCode[] statusCodes) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statusCodes = new(statusCodes);

        public List<string> RequestUris { get; } = [];

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!.ToString());
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(_statusCodes.Dequeue())
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }
    }

}
