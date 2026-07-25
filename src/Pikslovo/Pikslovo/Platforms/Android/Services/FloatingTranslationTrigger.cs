using Android.Content;
using Android.Database;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Java.Interop;

namespace Pikslovo.Droid.Services;

internal enum FloatingTranslationTriggerState
{
    Ready,
    Processing,
    ResultVisible,
}

internal sealed partial class FloatingTranslationTrigger
{
    private const long MenuAnimationDurationMilliseconds = 220;
    private readonly Context _context;
    private readonly IWindowManager _windowManager;
    private readonly Handler _mainHandler = new(Looper.MainLooper!);
    private ImageButton? _button;
    private WindowManagerLayoutParams? _layout;
    private FloatingMenu? _menu;
    private BrightnessObserver? _brightnessObserver;
    private int _stateRevision;
    private int _menuRevision;
    private bool _isAttached;
    private bool _buttonShouldBeVisible = true;
    private bool _isMenuExpanded;
    private FloatingTranslationTriggerState _state = FloatingTranslationTriggerState.Ready;

    public FloatingTranslationTrigger(Context context)
    {
        _context = context;
        _windowManager = context
            .GetSystemService(Context.WindowService)!
            .JavaCast<IWindowManager>();
    }

    public bool IsAttached => _isAttached;

    public void Show(Action onClick, Action onEditRegion, Action onStopSession, bool buttonVisible = true)
    {
        ShowCore(onClick, onEditRegion, onStopSession, buttonVisible);
    }

    public void ShowPreview()
    {
        ShowCore(static () => { }, static () => { }, static () => { }, buttonVisible: true);
    }

    public void SetButtonVisibility(bool visible)
    {
        _buttonShouldBeVisible = visible;
        _mainHandler.Post(ApplyButtonVisibility);
    }

    private void ShowCore(Action onClick, Action onEditRegion, Action onStopSession, bool buttonVisible)
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
        _button.Click += (_, _) =>
        {
            var menuWasExpanded = _menu is not null;
            CollapseMenu(animated: true);
            if (menuWasExpanded)
            {
                _mainHandler.PostDelayed(onClick, MenuAnimationDurationMilliseconds);
                return;
            }

            onClick();
        };
        _button.LongClick += (_, _) => ToggleMenu(onEditRegion, onStopSession);

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
                CollapseMenu(animated: false);
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
                CollapseMenu(animated: false);
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
        CollapseMenu(animated: false);
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

    private void ExpandMenu(Action onEditRegion, Action onStopSession)
    {
        if (_button is null || _layout is null || !_isAttached || !_buttonShouldBeVisible ||
            _state == FloatingTranslationTriggerState.Processing || _isMenuExpanded)
        {
            return;
        }

        var settings = AndroidSettingsStore.Load(_context).FloatingButton;
        var mainSize = GetButtonSize(settings.Scale);
        var actionSize = GetMenuActionSize(settings.Scale);
        var spacing = ToPixels(12f);
        var containerPadding = ToPixels(8f);
        var actionGap = ToPixels(8f);
        var menuWidth = actionSize + (containerPadding * 2);
        var menuHeight = (actionSize * 2) + actionGap + (containerPadding * 2);
        var direction = GetVerticalMenuDirection(mainSize, menuHeight, spacing);
        var menuX = GetCenteredMenuX(mainSize, menuWidth);
        var menuY = direction > 0
            ? _layout.Y + mainSize + spacing
            : _layout.Y - menuHeight - spacing;
        var outerGap = containerPadding;
        var containerWidth = menuWidth;
        var containerHeight = mainSize + spacing + menuHeight + outerGap;
        var containerX = menuX;
        var containerY = direction > 0
            ? _layout.Y - outerGap
            : menuY;

        _isMenuExpanded = true;
        var container = new View(_context)
        {
            Alpha = 0f,
            ScaleX = 0.92f,
            ScaleY = 0.35f,
            PivotX = containerWidth / 2f,
            PivotY = direction > 0 ? 0f : containerHeight,
            Background = CreateMenuContainerBackground(containerWidth / 2),
        };
        var menu = new LinearLayout(_context)
        {
            Orientation = Android.Widget.Orientation.Vertical,
            Alpha = 0f,
            ScaleX = 0.92f,
            ScaleY = 0.35f,
            TranslationY = -direction * (menuHeight + spacing),
            PivotX = menuWidth / 2f,
            PivotY = direction > 0 ? 0f : menuHeight,
        };
        menu.SetGravity(GravityFlags.Center);
        menu.SetPadding(containerPadding, containerPadding, containerPadding, containerPadding);
        menu.AddView(
            CreateMenuActionButton(
                Resource.Drawable.ic_edit,
                "Edytuj obszar przechwytywania",
                actionSize,
                CreateBackground(),
                () =>
                {
                    CollapseMenu(animated: true);
                    onEditRegion();
                }),
            new LinearLayout.LayoutParams(actionSize, actionSize)
            {
                BottomMargin = actionGap,
            });
        menu.AddView(
            CreateMenuActionButton(
                Resource.Drawable.ic_stop,
                "Zatrzymaj tłumacza",
                actionSize,
                CreateBackground(Color.Rgb(183, 28, 28)),
                () =>
                {
                    CollapseMenu(animated: true);
                    onStopSession();
                }),
            new LinearLayout.LayoutParams(actionSize, actionSize));

        var menuLayout = new WindowManagerLayoutParams(
            menuWidth,
            menuHeight,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable,
            Format.Rgba8888)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
            X = menuX,
            Y = menuY,
        };
        var containerLayout = new WindowManagerLayoutParams(
            containerWidth,
            containerHeight,
            WindowManagerTypes.ApplicationOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchable,
            Format.Rgba8888)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
            X = containerX,
            Y = containerY,
        };
        _windowManager.AddView(container, containerLayout);
        _windowManager.AddView(menu, menuLayout);
        _menu = new FloatingMenu(menu, container, direction, menuHeight + spacing);
        BringToFront();
        var containerAnimation = container.Animate();
        containerAnimation?.Alpha(1f)
            .ScaleX(1f)
            .ScaleY(1f)
            .SetDuration(MenuAnimationDurationMilliseconds)
            .Start();
        var openingAnimation = menu.Animate();
        openingAnimation?.Alpha(1f)
            .ScaleX(1f)
            .ScaleY(1f)
            .TranslationY(0f)
            .SetDuration(MenuAnimationDurationMilliseconds)
            .Start();
    }

    private void ToggleMenu(Action onEditRegion, Action onStopSession)
    {
        if (_isMenuExpanded)
        {
            CollapseMenu(animated: true);
            return;
        }

        ExpandMenu(onEditRegion, onStopSession);
    }

    private ImageButton CreateMenuActionButton(
        int iconResource,
        string contentDescription,
        int size,
        Drawable background,
        Action onClick)
    {
        var button = new ImageButton(_context)
        {
            ContentDescription = contentDescription,
        };
        var iconPadding = ToPixels(10f);
        button.SetPadding(iconPadding, iconPadding, iconPadding, iconPadding);
        button.SetScaleType(ImageView.ScaleType.FitCenter);
        button.SetImageResource(iconResource);
        button.Background = background;
        button.Click += (_, _) => onClick();
        return button;
    }

    private int GetVerticalMenuDirection(int mainSize, int menuHeight, int spacing)
    {
        if (_layout is null)
        {
            return 1;
        }

        var bounds = _windowManager.CurrentWindowMetrics?.Bounds;
        var screenHeight = bounds?.Height() ?? 0;
        var requiredSpace = menuHeight + spacing;
        var freeSpaceBelow = screenHeight - (_layout.Y + mainSize);
        if (freeSpaceBelow >= requiredSpace)
        {
            return 1;
        }

        var freeSpaceAbove = _layout.Y;
        if (freeSpaceAbove >= requiredSpace)
        {
            return -1;
        }

        return freeSpaceBelow >= freeSpaceAbove ? 1 : -1;
    }

    private int GetCenteredMenuX(int mainSize, int menuWidth)
    {
        if (_layout is null)
        {
            return 0;
        }

        var screenWidth = _windowManager.CurrentWindowMetrics?.Bounds?.Width() ?? menuWidth;
        return Math.Clamp(_layout.X + ((mainSize - menuWidth) / 2), 0, Math.Max(0, screenWidth - menuWidth));
    }

    private void CollapseMenu(bool animated)
    {
        if (_menu is null)
        {
            _isMenuExpanded = false;
            return;
        }

        _isMenuExpanded = false;
        var menu = _menu;
        _menu = null;
        var revision = Interlocked.Increment(ref _menuRevision);
        if (!animated)
        {
            RemoveMenu(menu.View);
            RemoveMenu(menu.Container);
            return;
        }

        menu.View.Enabled = false;
        for (var index = 0; index < menu.View.ChildCount; index++)
        {
            var child = menu.View.GetChildAt(index);
            if (child is not null)
            {
                child.Enabled = false;
            }
        }
        var closingAnimation = menu.View.Animate();
        closingAnimation?.Alpha(0f)
            .ScaleX(0.92f)
            .ScaleY(0.35f)
            .TranslationY(-menu.Direction * menu.Distance)
            .SetDuration(MenuAnimationDurationMilliseconds)
            .Start();
        var containerAnimation = menu.Container.Animate();
        containerAnimation?.Alpha(0f)
            .ScaleX(0.92f)
            .ScaleY(0.35f)
            .SetDuration(MenuAnimationDurationMilliseconds)
            .Start();

        _mainHandler.PostDelayed(() =>
        {
            if (revision == Volatile.Read(ref _menuRevision))
            {
                RemoveMenu(menu.View);
                RemoveMenu(menu.Container);
            }
        }, MenuAnimationDurationMilliseconds);
    }

    private void RemoveMenu(View menu)
    {
        try
        {
            _windowManager.RemoveViewImmediate(menu);
        }
        catch (Java.Lang.IllegalArgumentException)
        {
            // The view was already detached while the menu animation was ending.
        }

        menu.Dispose();
    }

    private Drawable CreateBackground(Color? color = null)
    {
        var background = new GradientDrawable();
        background.SetShape(ShapeType.Oval);
        if (color is { } backgroundColor)
        {
            background.SetColor(backgroundColor);
        }
        else
        {
            var accent = global::Pikslovo.App.GetAccentColor(AndroidSettingsStore.Load(_context).Accent);
            background.SetColor(Color.Rgb(accent.R, accent.G, accent.B));
        }
        return background;
    }

    private Drawable CreateMenuContainerBackground(int radius)
    {
        var background = new GradientDrawable();
        background.SetColor(Color.Argb(230, 35, 35, 35));
        background.SetCornerRadius(radius);
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
        if (!_buttonShouldBeVisible)
        {
            CollapseMenu(animated: false);
        }
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

    private int GetMenuActionSize(float scale) => ToPixels(48f * scale);

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

        _state = state;
        if (state == FloatingTranslationTriggerState.Processing)
        {
            CollapseMenu(animated: true);
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

    private sealed record FloatingMenu(LinearLayout View, View Container, int Direction, int Distance);
}
