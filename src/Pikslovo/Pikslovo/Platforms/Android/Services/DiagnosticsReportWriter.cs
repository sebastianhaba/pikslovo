using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using Pikslovo.Core;
using System.Text;

namespace Pikslovo.Droid.Services;

internal static class DiagnosticsReportWriter
{
    private const string ReportsDirectoryName = "diagnostics";
    private const string ReportFilePrefix = "pikslovo-diagnostics-";

    public static async Task ExportAndShareAsync(
        Activity activity,
        TranslationDiagnosticsSnapshot diagnostics,
        CancellationToken cancellationToken)
    {
        var reportsDirectory = Path.Combine(activity.CacheDir!.AbsolutePath!, ReportsDirectoryName);
        Directory.CreateDirectory(reportsDirectory);
        foreach (var staleReport in Directory.EnumerateFiles(reportsDirectory, $"{ReportFilePrefix}*.txt"))
        {
            File.Delete(staleReport);
        }

        var reportPath = Path.Combine(reportsDirectory, $"{ReportFilePrefix}{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        await using (var stream = File.Create(reportPath))
        {
            await WriteAsync(stream, activity, diagnostics, cancellationToken);
        }

        var reportUri = FileProvider.GetUriForFile(
            activity,
            $"{activity.PackageName}.fileprovider",
            new Java.IO.File(reportPath));
        var intent = new Intent(Intent.ActionSend);
        intent.SetType("text/plain");
        intent.PutExtra(Intent.ExtraStream, reportUri);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        var chooser = Intent.CreateChooser(intent, AppStrings.Get("Udostępnij dziennik diagnostyczny"))
            ?? throw new InvalidOperationException(AppStrings.Get("Nie można otworzyć panelu udostępniania."));
        chooser.AddFlags(ActivityFlags.GrantReadUriPermission);
        activity.StartActivity(chooser);
    }

    public static async Task WriteAsync(
        Stream stream,
        Context context,
        TranslationDiagnosticsSnapshot diagnostics,
        CancellationToken cancellationToken)
    {
        var applicationVersion = typeof(DiagnosticsReportWriter).Assembly.GetName().Version?.ToString() ?? AppStrings.Get("nieznana");
        var notificationPermissionGranted = !OperatingSystem.IsAndroidVersionAtLeast(33) ||
            context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Permission.Granted;
        var metadata = new DiagnosticsReportMetadata(
            DateTimeOffset.UtcNow,
            applicationVersion,
            $"{Build.Manufacturer} {Build.Model} ({Build.Device})",
            $"{Build.VERSION.Release} (SDK {Build.VERSION.SdkInt})",
            TranslationForegroundService.IsSessionActive,
            Settings.CanDrawOverlays(context),
            notificationPermissionGranted);
        var settings = AndroidSettingsStore.Load(context);
        var ocrSettings = new DiagnosticsReportOcrSettings(
            settings.Translation.RecognitionConfidence,
            settings.Translation.OcrImageScale,
            settings.Translation.GroupingPower,
            settings.Translation.FontScale,
            settings.Translation.HideIdenticalTranslations,
            settings.Translation.UseJpegForOcr,
            settings.Translation.OcrJpegQuality);
        var contents = DiagnosticsReportFormatter.Format(metadata, diagnostics, ocrSettings);

        await stream.WriteAsync(Encoding.UTF8.GetBytes(contents), cancellationToken);
    }
}
