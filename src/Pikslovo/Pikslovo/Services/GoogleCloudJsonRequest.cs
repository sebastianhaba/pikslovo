using System.Net.Http.Headers;
using System.Text.Json;

namespace Pikslovo.Services;

internal static class GoogleCloudJsonRequest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<HttpResponseMessage> PostAsync<TRequest>(
        HttpClient httpClient,
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8",
        };

        return await httpClient.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
    }
}
