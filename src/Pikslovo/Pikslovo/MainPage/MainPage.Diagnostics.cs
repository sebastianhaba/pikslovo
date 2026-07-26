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
        _viewModel.UpdateDiagnostics(_diagnosticsService.Snapshot);
    }
}
