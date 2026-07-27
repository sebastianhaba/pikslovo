using Android.Content;
using Android.Graphics;
using Pikslovo.Core;
using Pikslovo.Services;
using System.Diagnostics;

namespace Pikslovo.Droid.Services;

internal sealed class TranslationCapturePipeline(
    AndroidServiceSettingsAdapter settingsAdapter,
    TranslationSessionCoordinator sessionCoordinator,
    TranslationOverlayCoordinator overlayCoordinator,
    Action<string> showMessage)
{
    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        var operationStopwatch = Stopwatch.StartNew();
        cancellationToken.ThrowIfCancellationRequested();
        if (!settingsAdapter.CanDrawOverlays())
        {
            showMessage(AppStrings.Keys.GrantOverlayPermission);
            return false;
        }

        var captureResult = await sessionCoordinator
            .AcquireBitmapAsync(overlayCoordinator.PrepareForCaptureAsync, cancellationToken)
            .ConfigureAwait(false);
        AppServices.Diagnostics.RecordCaptureAttempt(
            captureResult.Status switch
            {
                CaptureStatus.Success => CaptureAttemptStatus.Success,
                CaptureStatus.NoFreshFrame => CaptureAttemptStatus.NoFreshFrame,
                _ => CaptureAttemptStatus.Failed,
            },
            captureResult.Attempts,
            captureResult.ElapsedMilliseconds);

        if (captureResult.Status == CaptureStatus.NoFreshFrame)
        {
            Android.Util.Log.Warn(
                "Pikslovo",
                $"Screen capture timed out waiting for a fresh frame after {captureResult.Attempts} attempts and {captureResult.ElapsedMilliseconds} ms.");
            showMessage(AppStrings.Keys.ScreenFrameCaptureFailed);
            return false;
        }

        if (captureResult.Bitmap is null)
        {
            Android.Util.Log.Warn(
                "Pikslovo",
                $"Screen capture failed while reading the latest frame after {captureResult.Attempts} attempts and {captureResult.ElapsedMilliseconds} ms.");
            throw new InvalidOperationException(AppStrings.Get(AppStrings.Keys.ScreenFrameCaptureFailed));
        }

        using var bitmap = captureResult.Bitmap;
        var appSettings = settingsAdapter.Load();
        var processingAccent = global::Pikslovo.App.GetAccentColor(appSettings.Accent);
        overlayCoordinator.ShowProcessingFrame(Color.Rgb(processingAccent.R, processingAccent.G, processingAccent.B));
        overlayCoordinator.ShowTriggerAfterCapture();
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new MemoryStream();
        var settings = appSettings.Translation;
        var cropBounds = appSettings.CaptureRegion.ToPixelRect(bitmap.Width, bitmap.Height);
        using var croppedBitmap = appSettings.CaptureRegion.IsEnabled
            ? Bitmap.CreateBitmap(bitmap, cropBounds.Left, cropBounds.Top, cropBounds.Width, cropBounds.Height)
            : null;
        var bitmapForOcr = croppedBitmap ?? bitmap;
        using var scaledBitmap = CreateScaledOcrBitmap(bitmapForOcr, settings.OcrImageScale);
        var bitmapForVision = scaledBitmap ?? bitmapForOcr;
        var encodingStopwatch = Stopwatch.StartNew();
        var imageFormat = settings.UseJpegForOcr ? Bitmap.CompressFormat.Jpeg! : Bitmap.CompressFormat.Png!;
        var imageQuality = settings.UseJpegForOcr ? settings.OcrJpegQuality : 100;
        if (!bitmapForVision.Compress(imageFormat, imageQuality, stream))
        {
            throw new InvalidOperationException(AppStrings.Get(AppStrings.Keys.CouldNotEncodeOcrImage));
        }

        var imageBytes = stream.ToArray();
        var encodingMilliseconds = encodingStopwatch.ElapsedMilliseconds;
        var captureAndImageEncodingMilliseconds = operationStopwatch.ElapsedMilliseconds;
        var imageFormatName = settings.UseJpegForOcr ? $"JPEG {imageQuality}%" : "PNG";
        Android.Util.Log.Debug(
            "Pikslovo",
            $"Capture + encode: {captureAndImageEncodingMilliseconds} ms; {imageFormatName} encode: {encodingMilliseconds} ms; {bitmapForVision.Width}x{bitmapForVision.Height}; image={imageBytes.Length / 1024d:0.0} KiB");
        var execution = await AppServices.TranslationOrchestrator
            .TranslateWithTimingsAsync(imageBytes, settings, cancellationToken)
            .ConfigureAwait(false);
        var result = execution.Result;
        if (result is null)
        {
            AppServices.Diagnostics.RecordTranslation(
                captureAndImageEncodingMilliseconds,
                encodingMilliseconds,
                imageBytes.Length,
                execution.CloudVisionOcrMilliseconds,
                execution.CloudTranslationMilliseconds,
                null,
                operationStopwatch.ElapsedMilliseconds);
            return false;
        }

        if (result.Regions.Count == 0)
        {
            AppServices.Diagnostics.RecordTranslation(
                captureAndImageEncodingMilliseconds,
                encodingMilliseconds,
                imageBytes.Length,
                execution.CloudVisionOcrMilliseconds,
                execution.CloudTranslationMilliseconds,
                null,
                operationStopwatch.ElapsedMilliseconds);
            showMessage(AppStrings.Keys.NoTextFoundOnScreen);
            return false;
        }

        if (scaledBitmap is not null)
        {
            result = ScaleRegions(
                result,
                bitmapForOcr.Width / (float)bitmapForVision.Width,
                bitmapForOcr.Height / (float)bitmapForVision.Height);
        }

        if (appSettings.CaptureRegion.IsEnabled)
        {
            result = OffsetRegions(result, cropBounds.Left, cropBounds.Top);
        }

        var accent = global::Pikslovo.App.GetAccentColor(appSettings.Accent);
        var overlayRenderStopwatch = Stopwatch.StartNew();
        var overlay = AndroidOverlayRenderer.Render(
            bitmap,
            result,
            settings.FontScale,
            Color.Rgb(accent.R, accent.G, accent.B));
        var overlayRenderMilliseconds = overlayRenderStopwatch.ElapsedMilliseconds;
        AppServices.Diagnostics.RecordTranslation(
            captureAndImageEncodingMilliseconds,
            encodingMilliseconds,
            imageBytes.Length,
            execution.CloudVisionOcrMilliseconds,
            execution.CloudTranslationMilliseconds,
            overlayRenderMilliseconds,
            operationStopwatch.ElapsedMilliseconds);
        Android.Util.Log.Debug(
            "Pikslovo",
            $"Cloud Vision OCR: {execution.CloudVisionOcrMilliseconds} ms; Cloud Translation: {execution.CloudTranslationMilliseconds} ms; overlay render: {overlayRenderMilliseconds} ms; total={operationStopwatch.ElapsedMilliseconds} ms");
        cancellationToken.ThrowIfCancellationRequested();
        overlayCoordinator.ShowResult(overlay, cancellationToken, () => sessionCoordinator.IsActive);
        return true;
    }

    private static TranslationResult OffsetRegions(TranslationResult result, int offsetX, int offsetY) =>
        new(result.Regions.Select(region => new TranslatedRegion(
            region.SourceText,
            region.TranslatedText,
            new PixelRect(
                region.Bounds.Left + offsetX,
                region.Bounds.Top + offsetY,
                region.Bounds.Right + offsetX,
                region.Bounds.Bottom + offsetY))).ToArray());

    private static Bitmap? CreateScaledOcrBitmap(Bitmap bitmap, float scale)
    {
        if (scale >= 1f)
        {
            return null;
        }

        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        return Bitmap.CreateScaledBitmap(bitmap, width, height, filter: false);
    }

    private static TranslationResult ScaleRegions(TranslationResult result, float scaleX, float scaleY) =>
        new(result.Regions.Select(region => new TranslatedRegion(
            region.SourceText,
            region.TranslatedText,
            new PixelRect(
                (int)Math.Round(region.Bounds.Left * scaleX),
                (int)Math.Round(region.Bounds.Top * scaleY),
                Math.Max((int)Math.Round(region.Bounds.Left * scaleX) + 1, (int)Math.Round(region.Bounds.Right * scaleX)),
                Math.Max((int)Math.Round(region.Bounds.Top * scaleY) + 1, (int)Math.Round(region.Bounds.Bottom * scaleY))))).ToArray());
}
