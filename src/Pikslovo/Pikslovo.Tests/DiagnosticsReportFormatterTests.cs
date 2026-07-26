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
        var diagnostics = new TranslationDiagnosticsSnapshot(12, 34, 56, 78, 90, 123);

        var report = DiagnosticsReportFormatter.Format(metadata, diagnostics);

        report.Should().Contain("Cloud Translation: 78 ms");
        report.Should().Contain("API key validation: 123 ms");
        report.Should().Contain("API key, screen content, OCR result and translated text");
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
            new TranslationDiagnosticsSnapshot(null, null, null, null, null, null));

        report.Should().Contain("Total translation: no data");
        report.Should().Contain("API key validation: no data");
    }
}
