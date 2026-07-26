using Pikslovo.Core;
using Pikslovo.Services;

#if __ANDROID__
using Pikslovo.Droid.Services;
using global::Android.App;
#endif

namespace Pikslovo;

internal sealed class MainPageDiagnosticsService
{
    public TranslationDiagnosticsSnapshot Snapshot => AppServices.Diagnostics.Snapshot;

#if __ANDROID__
    public Task ExportAsync(Activity activity, CancellationToken cancellationToken) =>
        DiagnosticsReportWriter.ExportAndShareAsync(
            activity,
            AppServices.Diagnostics.Snapshot,
            cancellationToken);
#endif
}
