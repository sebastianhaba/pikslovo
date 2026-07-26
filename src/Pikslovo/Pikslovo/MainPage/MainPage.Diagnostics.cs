#if __ANDROID__
using Pikslovo.Droid;
#endif

namespace Pikslovo;

public sealed partial class MainPage
{
    private async void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is not { } activity)
        {
            ShowStatus("Aktywność Androida nie jest gotowa. Zamknij i otwórz aplikację ponownie.");
            return;
        }

        try
        {
            await _diagnosticsService.ExportAsync(activity, CancellationToken.None);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format("Nie można wyeksportować dziennika diagnostycznego: {0}", exception.Message));
        }
#endif
    }

    private void UpdateDiagnostics()
    {
        var diagnostics = _diagnosticsService.Snapshot;
        CaptureAndImageEncodingDurationValue.Text = FormatDuration(diagnostics.CaptureAndImageEncodingMilliseconds);
        OcrImageEncodingDurationValue.Text = FormatDuration(diagnostics.OcrImageEncodingMilliseconds);
        CloudVisionOcrDurationValue.Text = FormatDuration(diagnostics.CloudVisionOcrMilliseconds);
        CloudTranslationDurationValue.Text = FormatDuration(diagnostics.CloudTranslationMilliseconds);
        TranslationTotalDurationValue.Text = FormatDuration(diagnostics.TranslationTotalMilliseconds);
        ApiKeyValidationDurationValue.Text = FormatDuration(diagnostics.ApiKeyValidationMilliseconds);
    }
}
