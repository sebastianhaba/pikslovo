using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Java.Interop;

namespace Pikslovo.Droid.Services;

internal sealed partial class CaptureRegionSelectorOverlay
{
    private readonly Context _context;
    private readonly IWindowManager _windowManager;
    private RegionSelectionView? _selectionView;
    private View? _actionBar;
    private View? _instruction;

    public CaptureRegionSelectorOverlay(Context context)
    {
        _context = context;
        _windowManager = context.GetSystemService(Context.WindowService)!.JavaCast<IWindowManager>();
    }

    public bool IsShowing => _selectionView is not null;

    public void Show(CaptureRegionSettings initialRegion, Action<CaptureRegionSettings> onConfirm, Action onCancel)
    {
        Dismiss();

        var accent = global::Pikslovo.App.GetAccentColor(AndroidSettingsStore.Load(_context).Accent);
        var selection = new RegionSelectionView(_context, initialRegion.Normalize(), Color.Rgb(accent.R, accent.G, accent.B));
        var fullScreenLayout = new WindowManagerLayoutParams(
            WindowManagerLayoutParams.MatchParent,
            WindowManagerLayoutParams.MatchParent,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.LayoutInScreen | WindowManagerFlags.LayoutNoLimits,
            Format.Translucent)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
        };
        _windowManager.AddView(selection, fullScreenLayout);
        _selectionView = selection;

        var density = _context.Resources?.DisplayMetrics?.Density ?? 1f;
        var bar = CreateActionBar(
            density,
            () =>
            {
                Dismiss();
                onCancel();
            },
            () =>
            {
                var region = selection.Region;
                Dismiss();
                onConfirm(region);
            });
        var barLayout = new WindowManagerLayoutParams(
            WindowManagerLayoutParams.WrapContent,
            WindowManagerLayoutParams.WrapContent,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.LayoutInScreen,
            Format.Translucent)
        {
            Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal,
            Y = (int)(24f * density + 0.5f),
        };
        _windowManager.AddView(bar, barLayout);
        _actionBar = bar;

        var instruction = new TextView(_context)
        {
            Text = AppStrings.Get("Przeciągnij narożniki obszaru dialogu"),
            Gravity = GravityFlags.Center,
            TextSize = 14f,
        };
        instruction.SetTextColor(Color.White);
        instruction.SetPadding((int)(16f * density + 0.5f), (int)(10f * density + 0.5f), (int)(16f * density + 0.5f), (int)(10f * density + 0.5f));
        instruction.Background = RoundedBackground(Color.Argb(220, 30, 30, 30), (int)(20f * density + 0.5f));
        var instructionLayout = new WindowManagerLayoutParams(
            WindowManagerLayoutParams.WrapContent,
            WindowManagerLayoutParams.WrapContent,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchable | WindowManagerFlags.LayoutInScreen,
            Format.Translucent)
        {
            Gravity = GravityFlags.Top | GravityFlags.CenterHorizontal,
            Y = (int)(24f * density + 0.5f),
        };
        _windowManager.AddView(instruction, instructionLayout);
        _instruction = instruction;
    }

    public void Dismiss()
    {
        Remove(_selectionView);
        Remove(_actionBar);
        Remove(_instruction);
        _selectionView = null;
        _actionBar = null;
        _instruction = null;
    }

    private LinearLayout CreateActionBar(float density, Action onCancel, Action onConfirm)
    {
        var size = (int)(52f * density + 0.5f);
        var bar = new LinearLayout(_context)
        {
            Orientation = Android.Widget.Orientation.Horizontal,
        };
        bar.SetGravity(GravityFlags.Center);
        bar.SetPadding((int)(12f * density + 0.5f), (int)(10f * density + 0.5f), (int)(12f * density + 0.5f), (int)(10f * density + 0.5f));
        bar.Background = RoundedBackground(Color.Argb(230, 35, 35, 35), (int)(24f * density + 0.5f));

        var cancel = CreateActionButton("×", Color.Rgb(70, 70, 70), Color.White, size, onCancel);
        var confirm = CreateActionButton("✓", AccentColor(), Color.Black, size, onConfirm);
        bar.AddView(cancel, new LinearLayout.LayoutParams(size, size) { RightMargin = (int)(16f * density + 0.5f) });
        bar.AddView(confirm, new LinearLayout.LayoutParams(size, size));
        return bar;
    }

    private TextView CreateActionButton(string text, Color background, Color foreground, int size, Action action)
    {
        var button = new TextView(_context)
        {
            Text = text,
            TextSize = 28f,
            Gravity = GravityFlags.Center,
            ContentDescription = AppStrings.Get(text == "✓" ? "Zapisz obszar" : "Anuluj wybór obszaru"),
            Background = RoundedBackground(background, size / 2),
        };
        button.SetTextColor(foreground);
        button.Click += (_, _) => action();
        return button;
    }

    private Color AccentColor()
    {
        var accent = global::Pikslovo.App.GetAccentColor(AndroidSettingsStore.Load(_context).Accent);
        return Color.Rgb(accent.R, accent.G, accent.B);
    }

    private static Drawable RoundedBackground(Color color, int radius)
    {
        var background = new GradientDrawable();
        background.SetColor(color);
        background.SetCornerRadius(radius);
        return background;
    }

    private void Remove(View? view)
    {
        if (view is null)
        {
            return;
        }

        _windowManager.RemoveViewImmediate(view);
        view.Dispose();
    }

    private sealed partial class RegionSelectionView : View
    {
        private const float MinimumRegionFraction = 0.05f;
        private readonly Paint _dim = new() { Color = Color.Argb(190, 0, 0, 0) };
        private readonly Paint _border;
        private readonly Paint _handle;
        private CaptureRegionSettings _region;
        private DragTarget _dragTarget;
        private float _startX;
        private float _startY;
        private CaptureRegionSettings _startRegion = CaptureRegionSettings.FullScreen;

        public RegionSelectionView(Context context, CaptureRegionSettings region, Color accent) : base(context)
        {
            _region = region;
            _border = new Paint(PaintFlags.AntiAlias) { Color = accent, StrokeWidth = 4f * (context.Resources?.DisplayMetrics?.Density ?? 1f) };
            _border.SetStyle(Paint.Style.Stroke);
            _handle = new Paint(PaintFlags.AntiAlias) { Color = accent };
        }

        public CaptureRegionSettings Region => _region.Normalize();

        protected override void OnDraw(Android.Graphics.Canvas? canvas)
        {
            if (canvas is null)
            {
                return;
            }

            var bounds = GetBounds();
            canvas.DrawRect(0, 0, Width, bounds.Top, _dim);
            canvas.DrawRect(0, bounds.Bottom, Width, Height, _dim);
            canvas.DrawRect(0, bounds.Top, bounds.Left, bounds.Bottom, _dim);
            canvas.DrawRect(bounds.Right, bounds.Top, Width, bounds.Bottom, _dim);
            canvas.DrawRect(bounds, _border);

            var radius = 11f * (Resources?.DisplayMetrics?.Density ?? 1f);
            canvas.DrawCircle(bounds.Left, bounds.Top, radius, _handle);
            canvas.DrawCircle(bounds.Right, bounds.Top, radius, _handle);
            canvas.DrawCircle(bounds.Left, bounds.Bottom, radius, _handle);
            canvas.DrawCircle(bounds.Right, bounds.Bottom, radius, _handle);
        }

        public override bool OnTouchEvent(MotionEvent? motionEvent)
        {
            if (motionEvent is null || Width <= 0 || Height <= 0)
            {
                return false;
            }

            var x = Math.Clamp(motionEvent.GetX() / Width, 0f, 1f);
            var y = Math.Clamp(motionEvent.GetY() / Height, 0f, 1f);
            switch (motionEvent.Action)
            {
                case MotionEventActions.Down:
                    _dragTarget = HitTest(x, y);
                    _startX = x;
                    _startY = y;
                    _startRegion = _region;
                    return true;
                case MotionEventActions.Move:
                    UpdateRegion(x, y);
                    return true;
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    UpdateRegion(x, y);
                    _dragTarget = DragTarget.None;
                    return true;
                default:
                    return true;
            }
        }

        private RectF GetBounds() => new(Width * _region.Left, Height * _region.Top, Width * _region.Right, Height * _region.Bottom);

        private DragTarget HitTest(float x, float y)
        {
            const float handleThreshold = 0.075f;
            if (Math.Abs(x - _region.Left) < handleThreshold && Math.Abs(y - _region.Top) < handleThreshold) return DragTarget.TopLeft;
            if (Math.Abs(x - _region.Right) < handleThreshold && Math.Abs(y - _region.Top) < handleThreshold) return DragTarget.TopRight;
            if (Math.Abs(x - _region.Left) < handleThreshold && Math.Abs(y - _region.Bottom) < handleThreshold) return DragTarget.BottomLeft;
            if (Math.Abs(x - _region.Right) < handleThreshold && Math.Abs(y - _region.Bottom) < handleThreshold) return DragTarget.BottomRight;
            return x >= _region.Left && x <= _region.Right && y >= _region.Top && y <= _region.Bottom
                ? DragTarget.Move
                : DragTarget.None;
        }

        private void UpdateRegion(float x, float y)
        {
            if (_dragTarget == DragTarget.None)
            {
                return;
            }

            var deltaX = x - _startX;
            var deltaY = y - _startY;
            var left = _startRegion.Left;
            var top = _startRegion.Top;
            var right = _startRegion.Right;
            var bottom = _startRegion.Bottom;
            switch (_dragTarget)
            {
                case DragTarget.TopLeft: left += deltaX; top += deltaY; break;
                case DragTarget.TopRight: right += deltaX; top += deltaY; break;
                case DragTarget.BottomLeft: left += deltaX; bottom += deltaY; break;
                case DragTarget.BottomRight: right += deltaX; bottom += deltaY; break;
                case DragTarget.Move:
                    var width = right - left;
                    var height = bottom - top;
                    left = Math.Clamp(left + deltaX, 0f, 1f - width);
                    top = Math.Clamp(top + deltaY, 0f, 1f - height);
                    right = left + width;
                    bottom = top + height;
                    break;
            }

            _region = new CaptureRegionSettings(
                true,
                Math.Clamp(left, 0f, right - MinimumRegionFraction),
                Math.Clamp(top, 0f, bottom - MinimumRegionFraction),
                Math.Clamp(right, left + MinimumRegionFraction, 1f),
                Math.Clamp(bottom, top + MinimumRegionFraction, 1f)).Normalize();
            Invalidate();
        }

        private enum DragTarget
        {
            None,
            Move,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
        }
    }
}
