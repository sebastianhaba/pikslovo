using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Java.Interop;

namespace GameTranslator.Droid.Services;

internal sealed class FloatingTranslationTrigger
{
    private readonly Context _context;
    private readonly IWindowManager _windowManager;
    private TextView? _button;

    public FloatingTranslationTrigger(Context context)
    {
        _context = context;
        _windowManager = context
            .GetSystemService(Context.WindowService)!
            .JavaCast<IWindowManager>();
    }

    public void Show(Action onClick)
    {
        Dismiss();
        var size = ToPixels(56);
        _button = new TextView(_context)
        {
            Text = "T",
            TextSize = 22,
            Gravity = GravityFlags.Center,
            ContentDescription = "Tlumacz ekran",
        };
        _button.SetTextColor(Color.White);
        _button.SetTypeface(Android.Graphics.Typeface.DefaultBold, TypefaceStyle.Bold);
        _button.Background = CreateBackground();
        _button.Click += (_, _) => onClick();

        var layout = new WindowManagerLayoutParams(
            size,
            size,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable,
            Format.Rgba8888)
        {
            Gravity = GravityFlags.Top | GravityFlags.End,
            X = ToPixels(16),
            Y = ToPixels(120),
        };
        _windowManager.AddView(_button, layout);
    }

    public void Dismiss()
    {
        if (_button is null)
        {
            return;
        }

        _windowManager.RemoveViewImmediate(_button);
        _button.Dispose();
        _button = null;
    }

    private Drawable CreateBackground()
    {
        var background = new GradientDrawable();
        background.SetShape(ShapeType.Oval);
        background.SetColor(Color.Rgb(191, 43, 43));
        return background;
    }

    private int ToPixels(int dp) => (int)(dp * _context.Resources!.DisplayMetrics!.Density + 0.5f);
}
