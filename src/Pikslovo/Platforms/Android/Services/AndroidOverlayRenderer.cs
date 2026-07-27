using Android.Graphics;
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Pikslovo.Core;
using Java.Interop;

namespace Pikslovo.Droid.Services;

internal sealed partial class AndroidOverlayPresenter
{
    private readonly Context _context;
    private readonly IWindowManager _windowManager;
    private DismissableOverlayImageView? _imageView;
    private ProcessingFrameView? _processingFrameView;
    private Bitmap? _bitmap;
    private WindowManagerLayoutParams? _layout;
    private WindowManagerLayoutParams? _processingFrameLayout;
    private BrightnessObserver? _brightnessObserver;
    private bool _isAttached;

    public AndroidOverlayPresenter(Context context)
    {
        _context = context;
        _windowManager = context
            .GetSystemService(Context.WindowService)!
            .JavaCast<IWindowManager>();
    }

    public bool IsShowing => _imageView is not null;

    public void ShowProcessingFrame(Color borderColor)
    {
        DismissProcessingFrame();

        var bounds = _windowManager.CurrentWindowMetrics?.Bounds;
        var width = bounds?.Width() ?? 0;
        var height = bounds?.Height() ?? 0;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _processingFrameView = new ProcessingFrameView(_context, borderColor);
        _processingFrameLayout = new WindowManagerLayoutParams(
            width,
            height,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchable | WindowManagerFlags.LayoutInScreen | WindowManagerFlags.LayoutNoLimits,
            Format.Rgba8888)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
        };
        _windowManager.AddView(_processingFrameView, _processingFrameLayout);
    }

    public void Show(Bitmap bitmap, Action onDismiss)
    {
        Dismiss();
        _bitmap = bitmap;
        _imageView = new DismissableOverlayImageView(_context, onDismiss);
        _imageView.SetImageBitmap(_bitmap);
        _imageView.SetScaleType(ImageView.ScaleType.Center);

        _layout = new WindowManagerLayoutParams(
            _bitmap.Width,
            _bitmap.Height,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.LayoutInScreen | WindowManagerFlags.LayoutNoLimits,
            Format.Rgba8888)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
        };
        UpdateBrightness();
        _windowManager.AddView(_imageView, _layout);
        _isAttached = true;

        _brightnessObserver = new BrightnessObserver(this, new Handler(Looper.MainLooper!));
        var brightnessUri = Settings.System.GetUriFor(Settings.System.ScreenBrightness);
        if (brightnessUri is not null)
        {
            _context.ContentResolver?.RegisterContentObserver(brightnessUri, false, _brightnessObserver);
        }
    }

    public void Dismiss()
    {
        DismissProcessingFrame();

        if (_imageView is null)
        {
            return;
        }

        if (_brightnessObserver is not null)
        {
            _context.ContentResolver?.UnregisterContentObserver(_brightnessObserver);
            _brightnessObserver.Dispose();
            _brightnessObserver = null;
        }

        _windowManager.RemoveViewImmediate(_imageView);
        _isAttached = false;
        _imageView.SetImageBitmap(null);
        _imageView.Dispose();
        _imageView = null;
        _layout = null;
        _bitmap?.Recycle();
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private void DismissProcessingFrame()
    {
        if (_processingFrameView is null)
        {
            return;
        }

        _windowManager.RemoveViewImmediate(_processingFrameView);
        _processingFrameView.Dispose();
        _processingFrameView = null;
        _processingFrameLayout = null;
    }

    private void UpdateBrightness()
    {
        if (_layout is null)
        {
            return;
        }

        var brightness = Settings.System.GetInt(
            _context.ContentResolver,
            Settings.System.ScreenBrightness,
            128);
        _layout.ScreenBrightness = Math.Clamp(brightness / 255f, 0f, 1f);

        if (_imageView is not null && _isAttached)
        {
            _windowManager.UpdateViewLayout(_imageView, _layout);
        }
    }

    private sealed class BrightnessObserver(AndroidOverlayPresenter owner, Handler handler) : ContentObserver(handler)
    {
        public override void OnChange(bool selfChange)
        {
            base.OnChange(selfChange);
            owner.UpdateBrightness();
        }
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

    private sealed partial class ProcessingFrameView : View
    {
        private const long HighlightCycleMilliseconds = 1800;
        private const int HighlightSegments = 36;
        private readonly Paint _border;
        private readonly Paint _highlight;
        private readonly float _highlightLength;
        private readonly int _highlightRed;
        private readonly int _highlightGreen;
        private readonly int _highlightBlue;
        private readonly long _animationStartedAt;

        public ProcessingFrameView(Context context, Color borderColor) : base(context)
        {
            _border = new Paint { Color = borderColor, StrokeWidth = 6, AntiAlias = true };
            _border.SetStyle(Paint.Style.Stroke);

            var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            _highlight = new Paint { StrokeWidth = Math.Max(18f, 7f * density), AntiAlias = true };
            _highlight.SetStyle(Paint.Style.Stroke);
            _highlightLength = 180f * density;
            _highlightRed = Lighten(borderColor.R);
            _highlightGreen = Lighten(borderColor.G);
            _highlightBlue = Lighten(borderColor.B);
            _animationStartedAt = SystemClock.ElapsedRealtime();
        }

        protected override void OnDraw(Android.Graphics.Canvas? canvas)
        {
            if (canvas is null)
            {
                return;
            }

            base.OnDraw(canvas);
            canvas.DrawRect(3, 3, Width - 3, Height - 3, _border);

            var frameWidth = Width - 6f;
            var frameHeight = Height - 6f;
            var perimeter = 2f * (frameWidth + frameHeight);
            if (perimeter > 0)
            {
                var elapsed = SystemClock.ElapsedRealtime() - _animationStartedAt;
                var head = perimeter * (elapsed % HighlightCycleMilliseconds) / HighlightCycleMilliseconds;
                DrawHighlight(canvas, frameWidth, frameHeight, perimeter, head);
                PostInvalidateOnAnimation();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _border.Dispose();
                _highlight.Dispose();
            }

            base.Dispose(disposing);
        }

        private void DrawHighlight(Android.Graphics.Canvas canvas, float frameWidth, float frameHeight, float perimeter, float head)
        {
            var length = Math.Min(_highlightLength, perimeter / 2f);
            for (var segment = HighlightSegments; segment > 0; segment--)
            {
                var start = head - (length * segment / HighlightSegments);
                var end = head - (length * (segment - 1) / HighlightSegments);
                var brightness = (HighlightSegments - segment + 1f) / HighlightSegments;
                var alpha = (int)(65 + (190 * brightness * brightness));
                _highlight.Color = Color.Argb(alpha, _highlightRed, _highlightGreen, _highlightBlue);

                var startPoint = PointOnFrame(start, frameWidth, frameHeight, perimeter);
                var endPoint = PointOnFrame(end, frameWidth, frameHeight, perimeter);
                canvas.DrawLine(startPoint.X, startPoint.Y, endPoint.X, endPoint.Y, _highlight);
            }
        }

        private static PointF PointOnFrame(float distance, float frameWidth, float frameHeight, float perimeter)
        {
            var position = distance % perimeter;
            var right = 3 + frameWidth;
            var bottom = 3 + frameHeight;
            if (position < 0)
            {
                position += perimeter;
            }

            if (position <= frameWidth)
            {
                return new PointF(3 + position, 3);
            }

            position -= frameWidth;
            if (position <= frameHeight)
            {
                return new PointF(right, 3 + position);
            }

            position -= frameHeight;
            if (position <= frameWidth)
            {
                return new PointF(right - position, bottom);
            }

            position -= frameWidth;
            return new PointF(3, bottom - position);
        }

        private static int Lighten(int component) => component + ((255 - component) * 7 / 8);
    }
}

internal static class AndroidOverlayRenderer
{
    private const float MaximumTextSize = 48f;
    private const float PreferredMinimumTextSize = 16f;
    private const float AbsoluteMinimumTextSize = 8f;
    private const int RegionGap = 6;

    public static Bitmap Render(Bitmap source, TranslationResult result, float fontScale, Color borderColor)
    {
        var output = source.Copy(Bitmap.Config.Argb8888!, true)!;
        using var canvas = new Android.Graphics.Canvas(output);
        using var background = new Paint { Color = Color.Black, AntiAlias = true };
        using var text = new Paint { Color = Color.White, AntiAlias = true };
        using var border = new Paint { Color = borderColor, StrokeWidth = 6, AntiAlias = true };
        border.SetStyle(Paint.Style.Stroke);

        var layouts = PlanLayouts(result.Regions, text, fontScale, output.Width, output.Height);
        foreach (var layout in layouts)
        {
            DrawLayout(canvas, layout, text, background);
        }

        canvas.DrawRect(3, 3, output.Width - 3, output.Height - 3, border);
        return output;
    }

    private static PixelRect Clamp(PixelRect bounds, int width, int height) => new(
        Math.Clamp(bounds.Left, 0, width - 1),
        Math.Clamp(bounds.Top, 0, height - 1),
        Math.Clamp(Math.Max(bounds.Right, bounds.Left + 1), 1, width),
        Math.Clamp(Math.Max(bounds.Bottom, bounds.Top + 1), 1, height));

    private static IReadOnlyList<PlannedLayout> PlanLayouts(
        IReadOnlyList<TranslatedRegion> regions,
        Paint paint,
        float fontScale,
        int outputWidth,
        int outputHeight)
    {
        var clampedScale = float.IsFinite(fontScale)
            ? Math.Clamp(fontScale, 1f, 3f)
            : TranslationSettings.DefaultFontScale;
        var sourceBounds = regions
            .Select((region, index) => (Region: region, Index: index, Bounds: Clamp(region.Bounds, outputWidth, outputHeight)))
            .OrderBy(item => item.Bounds.Top)
            .ThenBy(item => item.Bounds.Left)
            .ToArray();
        var layouts = new List<PlannedLayout>(sourceBounds.Length);

        foreach (var item in sourceBounds)
        {
            var layout = PlanLayout(
                item.Index,
                item.Region.TranslatedText,
                item.Bounds,
                sourceBounds,
                layouts,
                paint,
                clampedScale,
                outputWidth,
                outputHeight);
            if (layout is not null)
            {
                layouts.Add(layout);
            }
        }

        return layouts;
    }

    private static PlannedLayout? PlanLayout(
        int regionIndex,
        string value,
        PixelRect bounds,
        IReadOnlyList<(TranslatedRegion Region, int Index, PixelRect Bounds)> allBounds,
        IReadOnlyList<PlannedLayout> plannedLayouts,
        Paint paint,
        float fontScale,
        int outputWidth,
        int outputHeight)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var preferredTextSize = GetBaseTextSize(value, bounds, paint);
        var scaledTextSize = ScaleTextSize(preferredTextSize, fontScale);
        var (leftLimit, rightLimit) = GetHorizontalLimits(regionIndex, bounds, allBounds, plannedLayouts, outputWidth);
        var bestCandidate = SelectBestCandidate(value, bounds, leftLimit, rightLimit, paint, scaledTextSize);

        if (bestCandidate is null)
        {
            return null;
        }

        var placedBounds = PlaceBounds(bestCandidate.Bounds, bounds.Top, plannedLayouts, outputWidth, outputHeight);
        return new PlannedLayout(
            placedBounds,
            bestCandidate.Lines,
            bestCandidate.TextSize,
            bestCandidate.HorizontalPadding,
            bestCandidate.VerticalPadding);
    }

    private static void DrawLayout(
        Android.Graphics.Canvas canvas,
        PlannedLayout layout,
        Paint paint,
        Paint background)
    {
        paint.TextSize = layout.TextSize;
        canvas.DrawRect(layout.Bounds.Left, layout.Bounds.Top, layout.Bounds.Right, layout.Bounds.Bottom, background);

        var baseline = layout.Bounds.Top + layout.VerticalPadding - paint.Ascent();
        var lineHeight = LineHeight(paint);
        foreach (var line in layout.Lines)
        {
            if (baseline + paint.Descent() > layout.Bounds.Bottom - layout.VerticalPadding)
            {
                break;
            }

            canvas.DrawText(line, layout.Bounds.Left + layout.HorizontalPadding, baseline, paint);
            baseline += lineHeight;
        }
    }

    private static float GetBaseTextSize(string value, PixelRect bounds, Paint paint)
    {
        var horizontalPadding = Math.Min(10f, Math.Max(2f, bounds.Width * 0.08f));
        var verticalPadding = Math.Min(10f, Math.Max(1f, bounds.Height * 0.10f));
        var usableWidth = Math.Max(1f, bounds.Width - (horizontalPadding * 2));
        var usableHeight = Math.Max(1f, bounds.Height - (verticalPadding * 2));
        var visibleCharacterCount = Math.Max(1, value.Count(character => !char.IsWhiteSpace(character)));
        var areaBasedTextSize = MathF.Sqrt((usableWidth * usableHeight / visibleCharacterCount) * 0.7f);
        paint.TextSize = Math.Clamp(areaBasedTextSize, PreferredMinimumTextSize, MaximumTextSize);

        var lines = Wrap(value, paint, (int)usableWidth);
        while (RequiredHeight(lines, paint) > usableHeight && paint.TextSize > AbsoluteMinimumTextSize)
        {
            paint.TextSize = Math.Max(AbsoluteMinimumTextSize, paint.TextSize - 1f);
            lines = Wrap(value, paint, (int)usableWidth);
        }

        return Math.Max(AbsoluteMinimumTextSize, paint.TextSize);
    }

    private static float ScaleTextSize(float baseTextSize, float fontScale)
    {
        var scale = float.IsFinite(fontScale)
            ? Math.Clamp(fontScale, 1f, 3f)
            : TranslationSettings.DefaultFontScale;
        return Math.Clamp(baseTextSize * scale, AbsoluteMinimumTextSize, MaximumTextSize);
    }

    private static CandidateLayout? SelectBestCandidate(
        string value,
        PixelRect originalBounds,
        int leftLimit,
        int rightLimit,
        Paint paint,
        float scaledTextSize)
    {
        var maxWidth = Math.Max(originalBounds.Width, rightLimit - leftLimit);
        CandidateLayout? best = null;
        for (var width = originalBounds.Width; width <= maxWidth; width += width < maxWidth ? 8 : 1)
        {
            var candidate = EvaluateCandidate(
                value,
                originalBounds,
                leftLimit,
                rightLimit,
                width,
                paint,
                scaledTextSize);
            best = ChooseBetter(best, candidate);
            if (best is not null && best.BrokenWordCount == 0 && best.Lines.Count == 1)
            {
                break;
            }
        }

        if (best is null && maxWidth != originalBounds.Width)
        {
            best = EvaluateCandidate(
                value,
                originalBounds,
                leftLimit,
                rightLimit,
                maxWidth,
                paint,
                scaledTextSize);
        }

        return best;
    }

    private static CandidateLayout EvaluateCandidate(
        string value,
        PixelRect originalBounds,
        int leftLimit,
        int rightLimit,
        int width,
        Paint paint,
        float scaledTextSize)
    {
        var horizontalPadding = Math.Min(10f, Math.Max(2f, width * 0.08f));
        var verticalPadding = Math.Min(10f, Math.Max(1f, originalBounds.Height * 0.10f));
        var usableWidth = Math.Max(1, (int)MathF.Floor(width - (horizontalPadding * 2f)));
        paint.TextSize = scaledTextSize;

        var wrapped = WrapDetailed(value, paint, usableWidth);
        var requiredTotalHeight = Math.Max(
            originalBounds.Height,
            (int)MathF.Ceiling(RequiredHeight(wrapped.Lines, paint) + (verticalPadding * 2f)));
        var bounds = ExpandHorizontally(originalBounds, width, leftLimit, rightLimit);
        bounds = new PixelRect(
            bounds.Left,
            originalBounds.Top,
            bounds.Right,
            Math.Max(originalBounds.Bottom, originalBounds.Top + requiredTotalHeight));

        return new CandidateLayout(
            bounds,
            wrapped.Lines,
            paint.TextSize,
            horizontalPadding,
            verticalPadding,
            wrapped.BrokenWordCount);
    }

    private static CandidateLayout? ChooseBetter(CandidateLayout? current, CandidateLayout candidate)
    {
        if (current is null)
        {
            return candidate;
        }

        if (candidate.BrokenWordCount != current.BrokenWordCount)
        {
            return candidate.BrokenWordCount < current.BrokenWordCount ? candidate : current;
        }

        if (candidate.Lines.Count != current.Lines.Count)
        {
            return candidate.Lines.Count < current.Lines.Count ? candidate : current;
        }

        if (Math.Abs(candidate.TextSize - current.TextSize) > 0.01f)
        {
            return candidate.TextSize > current.TextSize ? candidate : current;
        }

        if (candidate.Bounds.Width != current.Bounds.Width)
        {
            return candidate.Bounds.Width < current.Bounds.Width ? candidate : current;
        }

        return candidate.Bounds.Height < current.Bounds.Height ? candidate : current;
    }

    private static PixelRect ExpandHorizontally(PixelRect originalBounds, int width, int leftLimit, int rightLimit)
    {
        var extraWidth = Math.Max(0, width - originalBounds.Width);
        var leftCapacity = Math.Max(0, originalBounds.Left - leftLimit);
        var rightCapacity = Math.Max(0, rightLimit - originalBounds.Right);
        var leftExpansion = Math.Min(leftCapacity, extraWidth / 2);
        var rightExpansion = Math.Min(rightCapacity, extraWidth - leftExpansion);
        var remaining = extraWidth - leftExpansion - rightExpansion;
        if (remaining > 0)
        {
            var additionalLeft = Math.Min(leftCapacity - leftExpansion, remaining);
            leftExpansion += additionalLeft;
            remaining -= additionalLeft;
        }

        if (remaining > 0)
        {
            rightExpansion += Math.Min(rightCapacity - rightExpansion, remaining);
        }

        var left = originalBounds.Left - leftExpansion;
        var right = originalBounds.Right + rightExpansion;
        return new PixelRect(left, originalBounds.Top, Math.Max(left + 1, right), originalBounds.Bottom);
    }

    private static (int LeftLimit, int RightLimit) GetHorizontalLimits(
        int regionIndex,
        PixelRect bounds,
        IReadOnlyList<(TranslatedRegion Region, int Index, PixelRect Bounds)> allBounds,
        IReadOnlyList<PlannedLayout> plannedLayouts,
        int outputWidth)
    {
        var verticalBandPadding = Math.Max(12, bounds.Height / 2);
        var leftLimit = 0;
        var rightLimit = outputWidth;

        foreach (var other in allBounds)
        {
            if (other.Index == regionIndex)
            {
                continue;
            }

            if (!VerticalRangesOverlap(
                    bounds.Top - verticalBandPadding,
                    bounds.Bottom + verticalBandPadding,
                    other.Bounds.Top,
                    other.Bounds.Bottom))
            {
                continue;
            }

            if (other.Bounds.Right <= bounds.Left)
            {
                leftLimit = Math.Max(leftLimit, other.Bounds.Right + RegionGap);
            }
            else if (other.Bounds.Left >= bounds.Right)
            {
                rightLimit = Math.Min(rightLimit, other.Bounds.Left - RegionGap);
            }
        }

        foreach (var layout in plannedLayouts)
        {
            if (!VerticalRangesOverlap(
                    bounds.Top - verticalBandPadding,
                    bounds.Bottom + verticalBandPadding,
                    layout.Bounds.Top,
                    layout.Bounds.Bottom))
            {
                continue;
            }

            if (layout.Bounds.Right <= bounds.Left)
            {
                leftLimit = Math.Max(leftLimit, layout.Bounds.Right + RegionGap);
            }
            else if (layout.Bounds.Left >= bounds.Right)
            {
                rightLimit = Math.Min(rightLimit, layout.Bounds.Left - RegionGap);
            }
        }

        if (rightLimit - leftLimit < bounds.Width)
        {
            leftLimit = Math.Max(0, Math.Min(leftLimit, bounds.Left));
            rightLimit = Math.Min(outputWidth, Math.Max(rightLimit, bounds.Right));
        }

        return (leftLimit, Math.Max(leftLimit + 1, rightLimit));
    }

    private static PixelRect PlaceBounds(
        PixelRect candidate,
        int preferredTop,
        IReadOnlyList<PlannedLayout> plannedLayouts,
        int outputWidth,
        int outputHeight)
    {
        var height = candidate.Height;
        var horizontalOverlaps = plannedLayouts
            .Where(layout => HorizontalRangesOverlap(candidate.Left, candidate.Right, layout.Bounds.Left, layout.Bounds.Right))
            .Select(layout => layout.Bounds)
            .ToArray();
        if (horizontalOverlaps.Length == 0)
        {
            return Clamp(candidate with { Bottom = Math.Min(outputHeight, candidate.Bottom) }, outputWidth, outputHeight);
        }

        var downTop = preferredTop;
        foreach (var obstacle in horizontalOverlaps.OrderBy(bounds => bounds.Top))
        {
            if (VerticalRangesOverlap(downTop, downTop + height, obstacle.Top, obstacle.Bottom))
            {
                downTop = obstacle.Bottom + RegionGap;
            }
        }

        var upTop = preferredTop;
        foreach (var obstacle in horizontalOverlaps.OrderByDescending(bounds => bounds.Bottom))
        {
            if (VerticalRangesOverlap(upTop, upTop + height, obstacle.Top, obstacle.Bottom))
            {
                upTop = obstacle.Top - RegionGap - height;
            }
        }

        var canPlaceUp = upTop >= 0;
        var canPlaceDown = downTop + height <= outputHeight;
        var chosenTop = preferredTop;

        if (canPlaceUp && canPlaceDown)
        {
            chosenTop = Math.Abs(preferredTop - upTop) <= Math.Abs(downTop - preferredTop) ? upTop : downTop;
        }
        else if (canPlaceUp)
        {
            chosenTop = upTop;
        }
        else if (canPlaceDown)
        {
            chosenTop = downTop;
        }

        return Clamp(
            new PixelRect(candidate.Left, chosenTop, candidate.Right, Math.Min(outputHeight, chosenTop + height)),
            outputWidth,
            outputHeight);
    }

    private static bool HorizontalRangesOverlap(int firstLeft, int firstRight, int secondLeft, int secondRight) =>
        firstLeft < secondRight && secondLeft < firstRight;

    private static bool VerticalRangesOverlap(int firstTop, int firstBottom, int secondTop, int secondBottom) =>
        firstTop < secondBottom && secondTop < firstBottom;

    private static float RequiredHeight(IReadOnlyCollection<string> lines, Paint paint) =>
        lines.Count * LineHeight(paint);

    private static float LineHeight(Paint paint) =>
        (paint.Descent() - paint.Ascent()) * 1.15f;

    private static List<string> Wrap(string value, Paint paint, int maxWidth)
        => WrapDetailed(value, paint, maxWidth).Lines.ToList();

    private static WrapResult WrapDetailed(string value, Paint paint, int maxWidth)
    {
        var lines = new List<string>();
        var brokenWordCount = 0;
        foreach (var paragraph in value.Replace("\r\n", "\n").Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (paint.MeasureText(candidate) <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                    current = string.Empty;
                }

                var segments = SplitLongWord(word, paint, maxWidth);
                if (segments.Count > 1)
                {
                    brokenWordCount++;
                }

                for (var index = 0; index < segments.Count - 1; index++)
                {
                    lines.Add(segments[index]);
                }

                current = segments[^1];
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }
        }

        return new WrapResult(lines.Count == 0 ? [string.Empty] : lines, brokenWordCount);
    }

    private static List<string> SplitLongWord(string word, Paint paint, int maxWidth)
    {
        if (paint.MeasureText(word) <= maxWidth)
        {
            return [word];
        }

        var segments = new List<string>();
        var current = string.Empty;
        foreach (var character in word)
        {
            var candidate = current + character;
            if (!string.IsNullOrEmpty(current) && paint.MeasureText(candidate) > maxWidth)
            {
                segments.Add(current);
                current = character.ToString();
            }
            else
            {
                current = candidate;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            segments.Add(current);
        }

        return segments;
    }

    private sealed record PlannedLayout(
        PixelRect Bounds,
        IReadOnlyList<string> Lines,
        float TextSize,
        float HorizontalPadding,
        float VerticalPadding);

    private sealed record CandidateLayout(
        PixelRect Bounds,
        IReadOnlyList<string> Lines,
        float TextSize,
        float HorizontalPadding,
        float VerticalPadding,
        int BrokenWordCount);

    private sealed record WrapResult(IReadOnlyList<string> Lines, int BrokenWordCount);
}
