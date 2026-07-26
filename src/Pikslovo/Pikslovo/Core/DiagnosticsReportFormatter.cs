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
        report.AppendLine();
        report.AppendLine(AppStrings.Get("Wykluczone dane: klucz API, treść ekranu, wynik OCR i tekst tłumaczenia."));
        return report.ToString();
    }

    private static void AppendDuration(StringBuilder report, string label, long? milliseconds) =>
        report.Append(label)
            .Append(": ")
            .Append(milliseconds is { } value
                ? string.Format(CultureInfo.InvariantCulture, "{0} ms", value)
                : AppStrings.Get("brak danych"))
            .AppendLine();

    private static string FormatBoolean(bool value) => AppStrings.Get(value ? "tak" : "nie");
}
