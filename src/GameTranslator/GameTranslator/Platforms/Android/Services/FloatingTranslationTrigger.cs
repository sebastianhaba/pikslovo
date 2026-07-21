using Android.Content;
using Android.Database;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Java.Interop;

namespace GameTranslator.Droid.Services;

internal sealed class FloatingTranslationTrigger
{
    private readonly Context _context;
    private readonly IWindowManager _windowManager;
    private TextView? _button;
    private WindowManagerLayoutParams? _layout;
    private BrightnessObserver? _brightnessObserver;
    private bool _isAttached;

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

        _layout = new WindowManagerLayoutParams(
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
        UpdateBrightness();
        _windowManager.AddView(_button, _layout);
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
        if (_button is null)
        {
            return;
        }

        if (_brightnessObserver is not null)
        {
            _context.ContentResolver?.UnregisterContentObserver(_brightnessObserver);
            _brightnessObserver.Dispose();
            _brightnessObserver = null;
        }

        _windowManager.RemoveViewImmediate(_button);
        _isAttached = false;
        _button.Dispose();
        _button = null;
        _layout = null;
    }

    private Drawable CreateBackground()
    {
        var background = new GradientDrawable();
        background.SetShape(ShapeType.Oval);
        background.SetColor(Color.Rgb(191, 43, 43));
        return background;
    }

    private int ToPixels(int dp) => (int)(dp * _context.Resources!.DisplayMetrics!.Density + 0.5f);

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

        if (_button is not null && _isAttached)
        {
            _windowManager.UpdateViewLayout(_button, _layout);
        }
    }

    private sealed class BrightnessObserver(FloatingTranslationTrigger owner, Handler handler) : ContentObserver(handler)
    {
        public override void OnChange(bool selfChange)
        {
            base.OnChange(selfChange);
            owner.UpdateBrightness();
        }
    }
}
