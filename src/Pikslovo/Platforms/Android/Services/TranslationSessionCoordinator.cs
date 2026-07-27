using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Display;
using Android.Media;
using Android.Media.Projection;
using Android.OS;
using Android.Views;
using Java.Interop;

namespace Pikslovo.Droid.Services;

internal sealed class TranslationSessionCoordinator(Context context)
{
    private const int CaptureSurfaceRefreshDelayMilliseconds = 75;
    private const int CaptureRetryDelayMilliseconds = 50;
    private const int CaptureRetryTimeoutMilliseconds = 400;

    private readonly object _stateLock = new();
    private MediaProjection? _mediaProjection;
    private VirtualDisplay? _virtualDisplay;
    private ImageReader? _imageReader;
    private CancellationTokenSource? _sessionCancellation;
    private bool _isProcessing;

    public bool IsActive { get; private set; }

    public bool IsProcessing
    {
        get
        {
            lock (_stateLock)
            {
                return _isProcessing;
            }
        }
    }

    public bool Start(Result resultCode, Intent resultData, Action onProjectionStopped)
    {
        if (IsActive || _mediaProjection is not null)
        {
            return false;
        }

        var manager = (MediaProjectionManager?)context.GetSystemService(Context.MediaProjectionService);
        _mediaProjection = manager?.GetMediaProjection((int)resultCode, resultData);
        if (_mediaProjection is null)
        {
            return false;
        }

        _mediaProjection.RegisterCallback(new ProjectionCallback(onProjectionStopped), new Handler(Looper.MainLooper!));
        CreateCaptureSurface();
        _sessionCancellation = new CancellationTokenSource();
        IsActive = true;
        return true;
    }

    public bool TryBeginProcessing(out CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (!IsActive || _isProcessing)
            {
                cancellationToken = CancellationToken.None;
                return false;
            }

            _isProcessing = true;
            cancellationToken = _sessionCancellation?.Token ?? CancellationToken.None;
            return true;
        }
    }

    public void EndProcessing()
    {
        lock (_stateLock)
        {
            _isProcessing = false;
        }
    }

    public async Task<CaptureResult> AcquireBitmapAsync(Func<Task> prepareCaptureUiAsync, CancellationToken cancellationToken)
    {
        await prepareCaptureUiAsync().ConfigureAwait(false);
        await Task.Delay(CaptureSurfaceRefreshDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var attempts = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            var image = _imageReader?.AcquireLatestImage();
            if (image is not null)
            {
                return ExtractBitmap(image, attempts, stopwatch.ElapsedMilliseconds);
            }

            if (stopwatch.ElapsedMilliseconds >= CaptureRetryTimeoutMilliseconds)
            {
                return CaptureResult.NoFreshFrame(attempts, stopwatch.ElapsedMilliseconds);
            }

            await Task.Delay(CaptureRetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? sessionCancellation;
        lock (_stateLock)
        {
            IsActive = false;
            _isProcessing = false;
            sessionCancellation = _sessionCancellation;
            _sessionCancellation = null;
        }

        sessionCancellation?.Cancel();
        _virtualDisplay?.Release();
        _virtualDisplay?.Dispose();
        _virtualDisplay = null;
        _imageReader?.Close();
        _imageReader?.Dispose();
        _imageReader = null;
        try
        {
            _mediaProjection?.Stop();
        }
        finally
        {
            _mediaProjection?.Dispose();
            _mediaProjection = null;
        }
    }

    private void CreateCaptureSurface()
    {
        var windowManager = context.GetSystemService(Context.WindowService)?.JavaCast<IWindowManager>();
        var bounds = windowManager?.CurrentWindowMetrics?.Bounds;
        var width = bounds?.Width() ?? 0;
        var height = bounds?.Height() ?? 0;
        var density = context.Resources?.Configuration?.DensityDpi ?? 0;
        if (width <= 0 || height <= 0 || density <= 0 || _mediaProjection is null)
        {
            throw new InvalidOperationException(AppStrings.Get(AppStrings.Keys.CannotReadScreenSize));
        }

        _imageReader = ImageReader.NewInstance(width, height, (ImageFormatType)1, 2);
        _virtualDisplay = _mediaProjection.CreateVirtualDisplay(
            "PikslovoCapture",
            width,
            height,
            density,
            (DisplayFlags)(int)VirtualDisplayFlags.AutoMirror,
            _imageReader.Surface,
            null,
            null);
    }

    private static CaptureResult ExtractBitmap(Android.Media.Image image, int attempts, long elapsedMilliseconds)
    {
        try
        {
            var planes = image.GetPlanes();
            if (planes is null)
            {
                return CaptureResult.Failed(attempts, elapsedMilliseconds);
            }

            var plane = planes.FirstOrDefault();
            if (plane is null)
            {
                return CaptureResult.Failed(attempts, elapsedMilliseconds);
            }

            var rowPadding = plane.RowStride - (plane.PixelStride * image.Width);
            using var paddedBitmap = Bitmap.CreateBitmap(
                image.Width + (rowPadding / plane.PixelStride),
                image.Height,
                Bitmap.Config.Argb8888!);
            var buffer = plane.Buffer;
            if (buffer is null)
            {
                return CaptureResult.Failed(attempts, elapsedMilliseconds);
            }

            paddedBitmap.CopyPixelsFromBuffer(buffer);
            return CaptureResult.Success(Bitmap.CreateBitmap(paddedBitmap, 0, 0, image.Width, image.Height), attempts, elapsedMilliseconds);
        }
        finally
        {
            image.Close();
            image.Dispose();
        }
    }

    private sealed class ProjectionCallback(Action onProjectionStopped) : MediaProjection.Callback
    {
        public override void OnStop()
        {
            onProjectionStopped();
        }
    }
}

internal enum CaptureStatus
{
    Success,
    NoFreshFrame,
    Failed
}

internal sealed record CaptureResult(CaptureStatus Status, Bitmap? Bitmap, int Attempts, long ElapsedMilliseconds)
{
    public static CaptureResult Success(Bitmap bitmap, int attempts, long elapsedMilliseconds) =>
        new(CaptureStatus.Success, bitmap, attempts, elapsedMilliseconds);

    public static CaptureResult NoFreshFrame(int attempts, long elapsedMilliseconds) =>
        new(CaptureStatus.NoFreshFrame, null, attempts, elapsedMilliseconds);

    public static CaptureResult Failed(int attempts, long elapsedMilliseconds) =>
        new(CaptureStatus.Failed, null, attempts, elapsedMilliseconds);
}
