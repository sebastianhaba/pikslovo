using Android.Graphics;
using Android.Content;
using Android.Views;
using Android.Widget;
using GameTranslator.Core;
using Java.Interop;

namespace GameTranslator.Droid.Services;

internal sealed partial class AndroidOverlayPresenter
{
    private readonly Context _context;
    private readonly IWindowManager _windowManager;
    private DismissableOverlayImageView? _imageView;
    private Bitmap? _bitmap;

    public AndroidOverlayPresenter(Context context)
    {
        _context = context;
        _windowManager = context
            .GetSystemService(Context.WindowService)!
            .JavaCast<IWindowManager>();
    }

    public bool IsShowing => _imageView is not null;

    public void Show(Bitmap bitmap, Action onDismiss)
    {
        Dismiss();
        _bitmap = bitmap;
        _imageView = new DismissableOverlayImageView(_context, onDismiss);
        _imageView.SetImageBitmap(_bitmap);
        _imageView.SetScaleType(ImageView.ScaleType.Center);

        var layout = new WindowManagerLayoutParams(
            _bitmap.Width,
            _bitmap.Height,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.LayoutInScreen | WindowManagerFlags.LayoutNoLimits,
            Format.Rgba8888)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
        };
        _windowManager.AddView(_imageView, layout);
    }

    public void Dismiss()
    {
        if (_imageView is null)
        {
            return;
        }

        _windowManager.RemoveViewImmediate(_imageView);
        _imageView.SetImageBitmap(null);
        _imageView.Dispose();
        _imageView = null;
        _bitmap?.Recycle();
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private sealed partial class DismissableOverlayImageView : ImageView
    {
        private readonly Action _onDismiss;

        public DismissableOverlayImageView(Context context, Action onDismiss) : base(context)
        {
            _onDismiss = onDismiss;
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e?.Action == MotionEventActions.Up)
            {
                _onDismiss();
            }

            return true;
        }
    }
}

internal static class AndroidOverlayRenderer
{
    private const int Padding = 10;

    public static Bitmap Render(Bitmap source, TranslationResult result)
    {
        var output = source.Copy(Bitmap.Config.Argb8888!, true)!;
        using var canvas = new Android.Graphics.Canvas(output);
        using var background = new Paint { Color = Color.Black, AntiAlias = true };
        using var text = new Paint { Color = Color.White, AntiAlias = true };
        using var border = new Paint { Color = Color.Rgb(220, 38, 38), StrokeWidth = 6, AntiAlias = true };
        border.SetStyle(Paint.Style.Stroke);

        foreach (var region in result.Regions)
        {
            var bounds = Clamp(region.Bounds, output.Width, output.Height);
            canvas.DrawRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, background);
            DrawWrappedText(canvas, region.TranslatedText, bounds, text);
        }

        canvas.DrawRect(3, 3, output.Width - 3, output.Height - 3, border);
        return output;
    }

    private static PixelRect Clamp(PixelRect bounds, int width, int height) => new(
        Math.Clamp(bounds.Left, 0, width - 1),
        Math.Clamp(bounds.Top, 0, height - 1),
        Math.Clamp(Math.Max(bounds.Right, bounds.Left + 1), 1, width),
        Math.Clamp(Math.Max(bounds.Bottom, bounds.Top + 1), 1, height));

    private static void DrawWrappedText(Android.Graphics.Canvas canvas, string value, PixelRect bounds, Paint paint)
    {
        var usableWidth = Math.Max(1, bounds.Width - (Padding * 2));
        var usableHeight = Math.Max(1, bounds.Height - (Padding * 2));
        paint.TextSize = Math.Clamp(usableHeight * 0.6f, 14f, 42f);

        var lines = Wrap(value, paint, usableWidth);
        while (lines.Count * paint.TextSize * 1.25f > usableHeight && paint.TextSize > 10f)
        {
            paint.TextSize -= 2f;
            lines = Wrap(value, paint, usableWidth);
        }

        var baseline = bounds.Top + Padding - paint.Ascent();
        foreach (var line in lines)
        {
            if (baseline + paint.Descent() > bounds.Bottom - Padding)
            {
                break;
            }

            canvas.DrawText(line, bounds.Left + Padding, baseline, paint);
            baseline += paint.TextSize * 1.25f;
        }
    }

    private static List<string> Wrap(string value, Paint paint, int maxWidth)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (!string.IsNullOrEmpty(current) && paint.MeasureText(candidate) > maxWidth)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        return lines.Count == 0 ? [string.Empty] : lines;
    }
}
