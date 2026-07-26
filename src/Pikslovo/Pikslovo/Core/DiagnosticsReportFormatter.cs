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
        report.AppendLine(AppStrings.Get(AppStrings.Keys.DiagnosticsReportTitle));
        report.AppendLine(AppStrings.Format(AppStrings.Keys.DiagnosticsGeneratedAtUtc, metadata.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
        report.AppendLine(AppStrings.Format(AppStrings.Keys.DiagnosticsApplication, metadata.ApplicationVersion));
        report.AppendLine(AppStrings.Format(AppStrings.Keys.DiagnosticsDevice, metadata.Device));
        report.AppendLine($"Android: {metadata.AndroidVersion}");
        report.AppendLine(AppStrings.Format(AppStrings.Keys.DiagnosticsActiveSession, FormatBoolean(metadata.IsSessionActive)));
        report.AppendLine(AppStrings.Format(AppStrings.Keys.DiagnosticsOverlayPermission, FormatBoolean(metadata.IsOverlayPermissionGranted)));
        report.AppendLine(AppStrings.Format(AppStrings.Keys.DiagnosticsNotificationPermission, FormatBoolean(metadata.IsNotificationPermissionGranted)));
        report.AppendLine();
        report.AppendLine(AppStrings.Get(AppStrings.Keys.DiagnosticsLatestMeasurements));
        AppendDuration(report, AppStrings.Get(AppStrings.Keys.CapturePlusEncoding), diagnostics.CaptureAndImageEncodingMilliseconds);
        AppendDuration(report, AppStrings.Get(AppStrings.Keys.OcrImageEncoding), diagnostics.OcrImageEncodingMilliseconds);
        AppendDuration(report, "Cloud Vision OCR", diagnostics.CloudVisionOcrMilliseconds);
        AppendDuration(report, "Cloud Translation", diagnostics.CloudTranslationMilliseconds);
        AppendDuration(report, AppStrings.Get(AppStrings.Keys.TotalTranslation), diagnostics.TranslationTotalMilliseconds);
        AppendDuration(report, AppStrings.Get(AppStrings.Keys.ApiKeyValidation), diagnostics.ApiKeyValidationMilliseconds);
        if (ocrSettings is not null)
        {
            report.AppendLine();
            report.AppendLine(AppStrings.Get(AppStrings.Keys.UserOcrSettings));
            report.AppendLine(AppStrings.Format(AppStrings.Keys.OcrConfidence, FormatFloat(ocrSettings.RecognitionConfidence)));
            report.AppendLine(AppStrings.Format(AppStrings.Keys.OcrImageScale, FormatScale(ocrSettings.OcrImageScale)));
            report.AppendLine(AppStrings.Format(AppStrings.Keys.DialogGroupingStrength, FormatFloat(ocrSettings.GroupingPower)));
            report.AppendLine(AppStrings.Format(AppStrings.Keys.FontScale, FormatScale(ocrSettings.FontScale)));
            report.AppendLine(AppStrings.Format(AppStrings.Keys.HideIdenticalTranslations, FormatBoolean(ocrSettings.HideIdenticalTranslations)));
            report.AppendLine(AppStrings.Format(AppStrings.Keys.OcrImageEncodingFormat, FormatImageEncoding(ocrSettings)));
        }

        return report.ToString();
    }

    private static void AppendDuration(StringBuilder report, string label, long? milliseconds) =>
        report.Append(label)
            .Append(": ")
            .Append(milliseconds is { } value
                ? string.Format(CultureInfo.InvariantCulture, "{0} ms", value)
                : AppStrings.Get(AppStrings.Keys.NoData))
            .AppendLine();

    private static string FormatFloat(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatScale(float value) => $"{value.ToString("0.##", CultureInfo.InvariantCulture)}x";

    private static string FormatImageEncoding(DiagnosticsReportOcrSettings settings) =>
        settings.UseJpegForOcr
            ? string.Format(CultureInfo.InvariantCulture, "JPEG {0}%", settings.OcrJpegQuality)
            : "PNG";

    private static string FormatBoolean(bool value) => AppStrings.GetBooleanLabel(value);
}
