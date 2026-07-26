using Pikslovo.Core;

#if __ANDROID__
using System.Security.Cryptography;
#endif

namespace Pikslovo.Services;

public static class AppServices
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static TranslationDiagnostics Diagnostics { get; } = new();

    public static TranslationOrchestrator TranslationOrchestrator { get; } = new(
        new GoogleVisionOcrProvider(HttpClient),
        new GoogleTranslationProvider(HttpClient));

    public static GoogleCloudApiKeyValidator GoogleCloudApiKeyValidator { get; } = new(HttpClient);

    private static HttpClient CreateHttpClient()
    {
#if __ANDROID__
        // Use Android's HTTPS implementation. It handles the device's network and
        // certificate configuration more reliably than the managed socket handler.
        var httpClient = new HttpClient(new global::Xamarin.Android.Net.AndroidMessageHandler());
        var context = global::Android.App.Application.Context;
        var packageName = context?.PackageName;
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return httpClient;
        }

        var packageManager = context?.PackageManager;
        if (packageManager is null)
        {
            return httpClient;
        }

        var packageInfo = OperatingSystem.IsAndroidVersionAtLeast(33)
            ? packageManager.GetPackageInfo(
                packageName,
                global::Android.Content.PM.PackageManager.PackageInfoFlags.Of(
                    (long)global::Android.Content.PM.PackageInfoFlags.SigningCertificates))
            : packageManager.GetPackageInfo(
                packageName,
                global::Android.Content.PM.PackageInfoFlags.SigningCertificates);
        var signatures = packageInfo?.SigningInfo?.GetApkContentsSigners();
        if (signatures is not { Length: > 0 })
        {
            return httpClient;
        }

        var certificate = signatures[0]?.ToByteArray();
        if (certificate is not { Length: > 0 })
        {
            return httpClient;
        }

        httpClient.DefaultRequestHeaders.Add("X-Android-Package", packageName);
        httpClient.DefaultRequestHeaders.Add("X-Android-Cert", Convert.ToHexString(SHA1.HashData(certificate)));
#else
        var httpClient = new HttpClient(new SocketsHttpHandler());
#endif
        return httpClient;
    }
}
