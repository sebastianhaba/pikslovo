using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GameTranslator.Core;

namespace GameTranslator.Services;

public sealed class GoogleVisionOcrProvider(HttpClient httpClient) : IOcrProvider
{
    private const string Endpoint = "https://vision.googleapis.com/v1/images:annotate";

    public async Task<OcrDocument> RecognizeAsync(
        ReadOnlyMemory<byte> imageBytes,
        TranslationSettings settings,
        CancellationToken cancellationToken)
    {
        var request = new VisionRequest(
            [new VisionImageRequest(
                new VisionImage(Convert.ToBase64String(imageBytes.Span)),
                [new VisionFeature("DOCUMENT_TEXT_DETECTION")])]);

        using var response = await httpClient
            .PostAsJsonAsync($"{Endpoint}?key={Uri.EscapeDataString(settings.ApiKey)}", request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new TranslationException($"Cloud Vision API odrzucilo zrzut ({(int)response.StatusCode} {response.StatusCode}): {details}");
        }

        var payload = await response.Content
            .ReadFromJsonAsync<VisionResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var annotation = payload?.Responses?.FirstOrDefault();

        if (annotation?.Error is not null)
        {
            throw new TranslationException($"Cloud Vision API: {annotation.Error.Message}");
        }

        var regions = annotation?.FullTextAnnotation?.Pages?
            .SelectMany(page => page.Blocks ?? [])
            .SelectMany(block => block.Paragraphs ?? [])
            .Where(paragraph => paragraph.Confidence >= settings.RecognitionConfidence)
            .Select(ToRegion)
            .Where(region => region is not null)
            .Cast<TextRegion>()
            .ToArray() ?? [];

        return new OcrDocument(regions);
    }

    private static TextRegion? ToRegion(VisionParagraph paragraph)
    {
        var words = paragraph.Words ?? [];
        var text = string.Concat(words.Select(word =>
            string.Concat(word.Symbols?.Select(symbol => symbol.Text) ?? []) +
            (word.Property?.DetectedBreak?.Type is "SPACE" or "EOL_SURE_SPACE" or "LINE_BREAK" ? " " : string.Empty)))
            .Trim();

        var vertices = paragraph.BoundingBox?.Vertices ?? [];
        if (string.IsNullOrWhiteSpace(text) || vertices.Length == 0)
        {
            return null;
        }

        var left = vertices.Min(vertex => vertex.X ?? 0);
        var top = vertices.Min(vertex => vertex.Y ?? 0);
        var right = vertices.Max(vertex => vertex.X ?? 0);
        var bottom = vertices.Max(vertex => vertex.Y ?? 0);

        return new TextRegion(text, new PixelRect(left, top, right, bottom));
    }

    private sealed record VisionRequest([property: JsonPropertyName("requests")] VisionImageRequest[] Requests);

    private sealed record VisionImageRequest(
        [property: JsonPropertyName("image")] VisionImage Image,
        [property: JsonPropertyName("features")] VisionFeature[] Features);

    private sealed record VisionImage([property: JsonPropertyName("content")] string Content);

    private sealed record VisionFeature([property: JsonPropertyName("type")] string Type);

    private sealed class VisionResponse
    {
        [JsonPropertyName("responses")]
        public VisionAnnotation[]? Responses { get; init; }
    }

    private sealed class VisionAnnotation
    {
        [JsonPropertyName("fullTextAnnotation")]
        public VisionFullTextAnnotation? FullTextAnnotation { get; init; }

        [JsonPropertyName("error")]
        public VisionError? Error { get; init; }
    }

    private sealed class VisionFullTextAnnotation
    {
        [JsonPropertyName("pages")]
        public VisionPage[]? Pages { get; init; }
    }

    private sealed class VisionPage
    {
        [JsonPropertyName("blocks")]
        public VisionBlock[]? Blocks { get; init; }
    }

    private sealed class VisionBlock
    {
        [JsonPropertyName("paragraphs")]
        public VisionParagraph[]? Paragraphs { get; init; }
    }

    private sealed class VisionParagraph
    {
        [JsonPropertyName("confidence")]
        public float Confidence { get; init; }

        [JsonPropertyName("boundingBox")]
        public VisionBoundingBox? BoundingBox { get; init; }

        [JsonPropertyName("words")]
        public VisionWord[]? Words { get; init; }
    }

    private sealed class VisionBoundingBox
    {
        [JsonPropertyName("vertices")]
        public VisionVertex[]? Vertices { get; init; }
    }

    private sealed class VisionVertex
    {
        [JsonPropertyName("x")]
        public int? X { get; init; }

        [JsonPropertyName("y")]
        public int? Y { get; init; }
    }

    private sealed class VisionWord
    {
        [JsonPropertyName("symbols")]
        public VisionSymbol[]? Symbols { get; init; }

        [JsonPropertyName("property")]
        public VisionTextProperty? Property { get; init; }
    }

    private sealed class VisionSymbol
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }

    private sealed class VisionTextProperty
    {
        [JsonPropertyName("detectedBreak")]
        public VisionBreak? DetectedBreak { get; init; }
    }

    private sealed class VisionBreak
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }
    }

    private sealed class VisionError
    {
        [JsonPropertyName("message")]
        public string Message { get; init; } = "Nieznany blad OCR.";
    }
}
