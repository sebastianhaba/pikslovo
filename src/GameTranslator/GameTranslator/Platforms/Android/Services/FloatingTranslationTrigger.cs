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

internal enum FloatingTranslationTriggerState
{
    Ready,
    Processing,
    ResultVisible,
}

internal sealed partial class FloatingTranslationTrigger
{
    private readonly Context _context;
    private readonly IWindowManager _windowManager;
    private readonly Handler _mainHandler = new(Looper.MainLooper!);
    private ImageButton? _button;
    private WindowManagerLayoutParams? _layout;
    private BrightnessObserver? _brightnessObserver;
    private int _stateRevision;
    private bool _isAttached;
    private bool _buttonShouldBeVisible = true;

    public FloatingTranslationTrigger(Context context)
    {
        _context = context;
        _windowManager = context
            .GetSystemService(Context.WindowService)!
            .JavaCast<IWindowManager>();
    }

    public bool IsAttached => _isAttached;

    public void Show(Action onClick, Action onLongClick, bool buttonVisible = true)
    {
        ShowCore(onClick, onLongClick, buttonVisible);
    }

    public void ShowPreview()
    {
        ShowCore(static () => { }, static () => { }, buttonVisible: true);
    }

    public void SetButtonVisibility(bool visible)
    {
        _buttonShouldBeVisible = visible;
        _mainHandler.Post(ApplyButtonVisibility);
    }

    private void ShowCore(Action onClick, Action onLongClick, bool buttonVisible)
    {
        Dismiss();
        _buttonShouldBeVisible = buttonVisible;
        var settings = AndroidSettingsStore.Load(_context).FloatingButton;
        var size = GetButtonSize(settings.Scale);
        _button = new ImageButton(_context)
        {
            ContentDescription = "Tłumacz ekran",
        };
        var iconPadding = ToPixels(12f * settings.Scale);
        _button.SetPadding(iconPadding, iconPadding, iconPadding, iconPadding);
        _button.SetScaleType(ImageView.ScaleType.FitCenter);
        _button.Background = CreateBackground();
        _button.Click += (_, _) => onClick();
        _button.LongClick += (_, _) => onLongClick();

        _layout = new WindowManagerLayoutParams(
            size,
            size,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable,
            Format.Rgba8888)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
        };
        ApplyPosition(settings, size);
        UpdateBrightness();
        _windowManager.AddView(_button, _layout);
        _isAttached = true;
        ApplyButtonVisibility();
        ApplyState(FloatingTranslationTriggerState.Ready, Interlocked.Increment(ref _stateRevision));

        _brightnessObserver = new BrightnessObserver(this, new Handler(Looper.MainLooper!));
        var brightnessUri = Settings.System.GetUriFor(Settings.System.ScreenBrightness);
        if (brightnessUri is not null)
        {
            _context.ContentResolver?.RegisterContentObserver(brightnessUri, false, _brightnessObserver);
        }
    }

    public void SetState(FloatingTranslationTriggerState state)
    {
        var revision = Interlocked.Increment(ref _stateRevision);
        _mainHandler.Post(() => ApplyState(state, revision));
    }

    public void RefreshConfiguration()
    {
        _mainHandler.Post(() =>
        {
            if (_button is not null && _layout is not null)
            {
                var settings = AndroidSettingsStore.Load(_context).FloatingButton;
                var size = GetButtonSize(settings.Scale);
                _button.Background = CreateBackground();
                var iconPadding = ToPixels(12f * settings.Scale);
                _button.SetPadding(iconPadding, iconPadding, iconPadding, iconPadding);
                _layout.Width = size;
                _layout.Height = size;
                ApplyPosition(settings, size);
                if (_isAttached)
                {
                    _windowManager.UpdateViewLayout(_button, _layout);
                }
            }
        });
    }

    public void BringToFront()
    {
        if (_button is null || _layout is null || !_isAttached)
        {
            return;
        }

        _windowManager.RemoveViewImmediate(_button);
        _windowManager.AddView(_button, _layout);
        ApplyButtonVisibility();
    }

    public Task HideForCaptureAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mainHandler.Post(() =>
        {
            if (_button is not null)
            {
                _button.Visibility = ViewStates.Invisible;
            }

            completion.SetResult();
        });
        return completion.Task;
    }

    public void ShowAfterCapture()
    {
        _mainHandler.Post(() =>
        {
            if (_button is not null)
            {
                ApplyButtonVisibility();
            }
        });
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
        var accent = global::GameTranslator.App.GetAccentColor(AndroidSettingsStore.Load(_context).Accent);
        var background = new GradientDrawable();
        background.SetShape(ShapeType.Oval);
        background.SetColor(Color.Rgb(accent.R, accent.G, accent.B));
        return background;
    }

    private void ApplyButtonVisibility()
    {
        if (_button is null || _layout is null)
        {
            return;
        }

        _button.Visibility = ViewStates.Visible;
        _button.Alpha = _buttonShouldBeVisible ? 1f : 0f;
        _button.Clickable = _buttonShouldBeVisible;
        _layout.Flags = _buttonShouldBeVisible
            ? WindowManagerFlags.NotFocusable
            : WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchable;
        if (_isAttached)
        {
            _windowManager.UpdateViewLayout(_button, _layout);
        }
    }

    private void ApplyPosition(FloatingButtonSettings settings, int size)
    {
        if (_layout is null)
        {
            return;
        }

        var bounds = _windowManager.CurrentWindowMetrics?.Bounds;
        var availableWidth = Math.Max(0, (bounds?.Width() ?? size) - size);
        var availableHeight = Math.Max(0, (bounds?.Height() ?? size) - size);
        _layout.X = (int)(availableWidth * settings.HorizontalPosition + 0.5f);
        _layout.Y = (int)(availableHeight * settings.VerticalPosition + 0.5f);
    }

    private int GetButtonSize(float scale) => ToPixels(56f * scale);

    private int ToPixels(float dp) => (int)(dp * _context.Resources!.DisplayMetrics!.Density + 0.5f);

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

    private void ApplyState(FloatingTranslationTriggerState state, int revision)
    {
        if (_button is null || revision != Volatile.Read(ref _stateRevision))
        {
            return;
        }

        _button.Enabled = state != FloatingTranslationTriggerState.Processing;
        _button.SetImageResource(state switch
        {
            FloatingTranslationTriggerState.Processing => Resource.Drawable.ic_hourglass_empty,
            FloatingTranslationTriggerState.ResultVisible => Resource.Drawable.ic_arrow_back,
            _ => Resource.Drawable.ic_translate,
        });
        _button.ContentDescription = state switch
        {
            FloatingTranslationTriggerState.Processing => "Tłumaczenie w toku",
            FloatingTranslationTriggerState.ResultVisible => "Wróć do gry",
            _ => "Tłumacz ekran",
        };
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
