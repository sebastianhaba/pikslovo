using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Serialization;
using Pikslovo.Core;

namespace Pikslovo.Services;

public sealed class GoogleTranslationProvider(HttpClient httpClient) : ITranslationProvider
{
    private const string Endpoint = "https://translation.googleapis.com/language/translate/v2";

    public async Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> sourceTexts,
        TranslationSettings settings,
        CancellationToken cancellationToken)
    {
        if (sourceTexts.Count == 0)
        {
            return [];
        }

        var request = new TranslationRequest(sourceTexts, settings.SourceLanguage, settings.TargetLanguage, "text");
        using var response = await httpClient
            .PostAsJsonAsync($"{Endpoint}?key={Uri.EscapeDataString(settings.ApiKey)}", request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new TranslationException($"Cloud Translation API odrzuciło zadanie ({(int)response.StatusCode} {response.StatusCode}): {details}");
        }

        var payload = await response.Content
            .ReadFromJsonAsync<TranslationResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var translations = payload?.Data?.Translations?
            .Select(translation => WebUtility.HtmlDecode(translation.TranslatedText))
            .ToArray();

        if (translations is null || translations.Length != sourceTexts.Count)
        {
            throw new TranslationException("Cloud Translation API zwróciło niepełną odpowiedź.");
        }

        return translations;
    }

    private sealed record TranslationRequest(
        [property: JsonPropertyName("q")] IReadOnlyList<string> Texts,
        [property: JsonPropertyName("source")] string SourceLanguage,
        [property: JsonPropertyName("target")] string TargetLanguage,
        [property: JsonPropertyName("format")] string Format);

    private sealed class TranslationResponse
    {
        [JsonPropertyName("data")]
        public TranslationData? Data { get; init; }
    }

    private sealed class TranslationData
    {
        [JsonPropertyName("translations")]
        public TranslationValue[]? Translations { get; init; }
    }

    private sealed class TranslationValue
    {
        [JsonPropertyName("translatedText")]
        public string TranslatedText { get; init; } = string.Empty;
    }
}
