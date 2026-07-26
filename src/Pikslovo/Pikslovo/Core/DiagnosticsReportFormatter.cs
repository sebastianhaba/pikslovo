using System.Globalization;
using System.Text;

namespace Pikslovo.Core;

public sealed record DiagnosticsReportMetadata(
    DateTimeOffset GeneratedAtUtc,
    string ApplicationVersion,
    string Device,
    string AndroidVersion,
    bool IsSessionActive,
    bool IsOverlayPermissionGranted,
    bool IsNotificationPermissionGranted);

public sealed record DiagnosticsReportOcrSettings(
    float RecognitionConfidence,
    float OcrImageScale,
    float GroupingPower,
    float FontScale,
    bool HideIdenticalTranslations,
    bool UseJpegForOcr,
    int OcrJpegQuality);

public static class DiagnosticsReportFormatter
{
    public static string Format(
        DiagnosticsReportMetadata metadata,
        TranslationDiagnosticsSnapshot diagnostics,
        DiagnosticsReportOcrSettings? ocrSettings = null)
    {
        var report = new StringBuilder();
        report.AppendLine(AppStrings.Get("Pikslovo - raport diagnostyczny"));
        report.AppendLine(AppStrings.Format("Wygenerowano (UTC): {0}", metadata.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
        report.AppendLine(AppStrings.Format("Aplikacja: {0}", metadata.ApplicationVersion));
        report.AppendLine(AppStrings.Format("Urządzenie: {0}", metadata.Device));
        report.AppendLine($"Android: {metadata.AndroidVersion}");
        report.AppendLine(AppStrings.Format("Aktywna sesja tłumacza: {0}", FormatBoolean(metadata.IsSessionActive)));
        report.AppendLine(AppStrings.Format("Uprawnienie nakładki: {0}", FormatBoolean(metadata.IsOverlayPermissionGranted)));
        report.AppendLine(AppStrings.Format("Uprawnienie powiadomień: {0}", FormatBoolean(metadata.IsNotificationPermissionGranted)));
        report.AppendLine();
        report.AppendLine(AppStrings.Get("Ostatnie pomiary:"));
        AppendDuration(report, AppStrings.Get("Przechwycenie + kodowanie"), diagnostics.CaptureAndImageEncodingMilliseconds);
        AppendDuration(report, AppStrings.Get("Kodowanie obrazu OCR"), diagnostics.OcrImageEncodingMilliseconds);
        AppendDuration(report, "Cloud Vision OCR", diagnostics.CloudVisionOcrMilliseconds);
        AppendDuration(report, "Cloud Translation", diagnostics.CloudTranslationMilliseconds);
        AppendDuration(report, AppStrings.Get("Całość tłumaczenia"), diagnostics.TranslationTotalMilliseconds);
        AppendDuration(report, AppStrings.Get("Sprawdzenie klucza API"), diagnostics.ApiKeyValidationMilliseconds);
        if (ocrSettings is not null)
        {
            report.AppendLine();
            report.AppendLine(AppStrings.Get("Ustawienia OCR użytkownika:"));
            report.AppendLine(AppStrings.Format("Pewność OCR: {0}", FormatFloat(ocrSettings.RecognitionConfidence)));
            report.AppendLine(AppStrings.Format("Skala obrazu OCR: {0}", FormatScale(ocrSettings.OcrImageScale)));
            report.AppendLine(AppStrings.Format("Siła łączenia dialogów: {0}", FormatFloat(ocrSettings.GroupingPower)));
            report.AppendLine(AppStrings.Format("Skalowanie czcionki: {0}", FormatScale(ocrSettings.FontScale)));
            report.AppendLine(AppStrings.Format("Ukrywaj identyczne tłumaczenia: {0}", FormatBoolean(ocrSettings.HideIdenticalTranslations)));
            report.AppendLine(AppStrings.Format("Kodowanie obrazu OCR: {0}", FormatImageEncoding(ocrSettings)));
        }

        return report.ToString();
    }

    private static void AppendDuration(StringBuilder report, string label, long? milliseconds) =>
        report.Append(label)
            .Append(": ")
            .Append(milliseconds is { } value
                ? string.Format(CultureInfo.InvariantCulture, "{0} ms", value)
                : AppStrings.Get("brak danych"))
            .AppendLine();

    private static string FormatFloat(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatScale(float value) => $"{value.ToString("0.##", CultureInfo.InvariantCulture)}x";

    private static string FormatImageEncoding(DiagnosticsReportOcrSettings settings) =>
        settings.UseJpegForOcr
            ? string.Format(CultureInfo.InvariantCulture, "JPEG {0}%", settings.OcrJpegQuality)
            : "PNG";

    private static string FormatBoolean(bool value) => AppStrings.Get(value ? "tak" : "nie");
}
