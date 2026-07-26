#if __ANDROID__
using Pikslovo.Droid;
#endif

namespace Pikslovo;

public sealed partial class MainPage
{
    private async Task ExportDiagnostics()
    {
#if __ANDROID__
        if (MainActivity.CurrentActivity is not { } activity)
        {
            ShowStatus(AppStrings.Keys.AndroidActivityNotReady);
            return;
        }

        try
        {
            await _diagnosticsService.ExportAsync(activity, CancellationToken.None);
        }
        catch (Exception exception)
        {
            ShowStatus(AppStrings.Format(AppStrings.Keys.ExportDiagnosticsFailed, exception.Message));
        }
#endif
    }

    private void UpdateDiagnostics()
    {
        _viewModel.UpdateDiagnostics(_diagnosticsService.Snapshot);
    }
}
