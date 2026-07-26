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

public static class DiagnosticsReportFormatter
{
    public static string Format(DiagnosticsReportMetadata metadata, TranslationDiagnosticsSnapshot diagnostics)
    {
        var report = new StringBuilder();
        report.AppendLine("Pikslovo - raport diagnostyczny");
        report.AppendLine($"Wygenerowano (UTC): {metadata.GeneratedAtUtc:O}");
        report.AppendLine($"Aplikacja: {metadata.ApplicationVersion}");
        report.AppendLine($"Urządzenie: {metadata.Device}");
        report.AppendLine($"Android: {metadata.AndroidVersion}");
        report.AppendLine($"Aktywna sesja tłumacza: {FormatBoolean(metadata.IsSessionActive)}");
        report.AppendLine($"Uprawnienie nakładki: {FormatBoolean(metadata.IsOverlayPermissionGranted)}");
        report.AppendLine($"Uprawnienie powiadomień: {FormatBoolean(metadata.IsNotificationPermissionGranted)}");
        report.AppendLine();
        report.AppendLine("Ostatnie pomiary:");
        AppendDuration(report, "Przechwycenie + kodowanie", diagnostics.CaptureAndImageEncodingMilliseconds);
        AppendDuration(report, "Kodowanie obrazu OCR", diagnostics.OcrImageEncodingMilliseconds);
        AppendDuration(report, "Cloud Vision OCR", diagnostics.CloudVisionOcrMilliseconds);
        AppendDuration(report, "Cloud Translation", diagnostics.CloudTranslationMilliseconds);
        AppendDuration(report, "Całość tłumaczenia", diagnostics.TranslationTotalMilliseconds);
        AppendDuration(report, "Sprawdzenie klucza API", diagnostics.ApiKeyValidationMilliseconds);
        report.AppendLine();
        report.AppendLine("Wykluczone dane: klucz API, treść ekranu, wynik OCR i tekst tłumaczenia.");
        return report.ToString();
    }

    private static void AppendDuration(StringBuilder report, string label, long? milliseconds) =>
        report.Append(label)
            .Append(": ")
            .Append(milliseconds is { } value
                ? string.Format(CultureInfo.InvariantCulture, "{0} ms", value)
                : "brak danych")
            .AppendLine();

    private static string FormatBoolean(bool value) => value ? "tak" : "nie";
}
