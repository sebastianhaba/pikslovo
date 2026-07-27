using Pikslovo.Core;

namespace Pikslovo.Tests;

public sealed class DiagnosticsReportFormatterTests
{
    [SetUp]
    public void SetUp() => AppStrings.SetLanguageMode(AppLanguageMode.English);

    [TearDown]
    public void TearDown() => AppStrings.SetLanguageMode(AppLanguageMode.System);

    [Test]
    public void Format_includes_diagnostic_measurements_without_sensitive_translation_data()
    {
        var metadata = new DiagnosticsReportMetadata(
            new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero),
            "1.0 (1)",
            "Example Pixel (example)",
            "16 (SDK 36)",
            true,
            true,
            false);
        var diagnostics = new TranslationDiagnosticsSnapshot(12, 34, 2048, 56, 78, 91, 90, 123, CaptureAttemptStatus.NoFreshFrame, 4, 225);
        var ocrSettings = new DiagnosticsReportOcrSettings(0.75f, 0.5f, 0.85f, 1.25f, true, true, 80);

        var report = DiagnosticsReportFormatter.Format(metadata, diagnostics, ocrSettings);

        report.Should().Contain("Cloud Translation: 78 ms");
        report.Should().Contain("OCR image size: 2.0 KiB");
        report.Should().Contain("Overlay render: 91 ms");
        report.Should().Contain("API key validation: 123 ms");
        report.Should().Contain("Last capture attempt: status=no fresh frame, attempts=4, elapsed=225 ms");
        report.Should().Contain("User OCR settings:");
        report.Should().Contain("OCR confidence: 0.75");
        report.Should().Contain("OCR image scale: 0.5x");
        report.Should().Contain("Dialog grouping strength: 0.85");
        report.Should().Contain("Font scale: 1.25x");
        report.Should().Contain("Hide identical translations: yes");
        report.Should().Contain("OCR image encoding: JPEG 80%");
        report.Should().NotContain("AIza");
    }

    [Test]
    public void Format_marks_missing_measurements_as_unavailable()
    {
        var metadata = new DiagnosticsReportMetadata(
            DateTimeOffset.UnixEpoch,
            "1.0 (1)",
            "Example Pixel (example)",
            "16 (SDK 36)",
            false,
            false,
            false);

        var report = DiagnosticsReportFormatter.Format(
            metadata,
            new TranslationDiagnosticsSnapshot(null, null, null, null, null, null, null, null, null, null, null));

        report.Should().Contain("Total translation: no data");
        report.Should().Contain("API key validation: no data");
        report.Should().Contain("Last capture attempt: status=no data, attempts=no data, elapsed=no data");
    }
}
