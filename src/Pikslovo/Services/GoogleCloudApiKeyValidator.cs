using System.Net;
using System.Text.Json.Serialization;

namespace Pikslovo.Services;

public sealed class GoogleCloudApiKeyValidator(HttpClient httpClient)
{
    private const string TranslationEndpoint = "https://translation.googleapis.com/language/translate/v2";
    private const string VisionEndpoint = "https://vision.googleapis.com/v1/images:annotate";
    private const string TestImage = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgAQAAAABbAUdZAAAAIGNIUk0AAHomAACAhAAA+gAAAIDoAAB1MAAA6mAAADqYAAAXcJy6UTwAAAACYktHRAAB3YoTpAAAAAd0SU1FB+oHGAwSI48PyeEAAAAldEVYdGRhdGU6Y3JlYXRlADIwMjYtMDctMjRUMTI6MTg6MzUrMDA6MDDH44g0AAAAJXRFWHRkYXRlOm1vZGlmeQAyMDI2LTA3LTI0VDEyOjE4OjM1KzAwOjAwtr4wiAAAACh0RVh0ZGF0ZTp0aW1lc3RhbXAAMjAyNi0wNy0yNFQxMjoxODozNSswMDowMOGrEVcAAAARSURBVAjXY/j///9/hsFLAADa6X+B2fzqbQAAAABJRU5ErkJggg==";

    public async Task ValidateAsync(string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        using var translationResponse = await GoogleCloudJsonRequest
            .PostAsync(
                httpClient,
                $"{TranslationEndpoint}?key={Uri.EscapeDataString(apiKey)}",
                new TranslationRequest(["test"], "en", "pl", "text"),
                cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(translationResponse, "Cloud Translation API");

        using var visionResponse = await GoogleCloudJsonRequest
            .PostAsync(
                httpClient,
                $"{VisionEndpoint}?key={Uri.EscapeDataString(apiKey)}",
                new VisionRequest([new VisionImageRequest(new VisionImage(TestImage), [new VisionFeature("DOCUMENT_TEXT_DETECTION")])]),
                cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(visionResponse, "Cloud Vision API");
    }

    private static void EnsureSuccess(HttpResponseMessage response, string serviceName)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new GoogleCloudApiKeyValidationException(serviceName, response.StatusCode);
        }
    }

    private sealed record TranslationRequest(
        [property: JsonPropertyName("q")] IReadOnlyList<string> Texts,
        [property: JsonPropertyName("source")] string SourceLanguage,
        [property: JsonPropertyName("target")] string TargetLanguage,
        [property: JsonPropertyName("format")] string Format);

    private sealed record VisionRequest([property: JsonPropertyName("requests")] VisionImageRequest[] Requests);

    private sealed record VisionImageRequest(
        [property: JsonPropertyName("image")] VisionImage Image,
        [property: JsonPropertyName("features")] VisionFeature[] Features);

    private sealed record VisionImage([property: JsonPropertyName("content")] string Content);

    private sealed record VisionFeature([property: JsonPropertyName("type")] string Type);
}

public sealed class GoogleCloudApiKeyValidationException(string serviceName, HttpStatusCode statusCode) : Exception
{
    public string ServiceName { get; } = serviceName;

    public HttpStatusCode StatusCode { get; } = statusCode;
}
