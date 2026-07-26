using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace Pikslovo.Droid.Services;

internal sealed class HotkeyCaptureDialog : Dialog
{
    private const int CaptureDelayMilliseconds = 2000;
    private static string CaptureInstruction => AppStrings.Get(AppStrings.Keys.HoldKeysForTwoSeconds);
    private readonly TaskCompletionSource<int[]?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly SortedSet<int> _heldCodes = [];
    private TextView? _instruction;
    private bool _captureScheduled;

    private HotkeyCaptureDialog(Context context)
        : base(context)
    {
    }

    public static Task<int[]?> ShowAsync(Activity activity)
    {
        var dialog = new HotkeyCaptureDialog(activity);
        dialog.Show();
        return dialog._completion.Task;
    }

    public static string Format(IEnumerable<int> hotkeyCodes)
    {
        var labels = hotkeyCodes
            .Where(code => code > 0)
            .Distinct()
            .OrderBy(code => code)
            .Select(GetKeyLabel)
            .ToArray();
        return labels.Length == 0 ? AppStrings.Get(AppStrings.Keys.NotSet) : string.Join(" + ", labels);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetCanceledOnTouchOutside(true);

        var colors = DialogColors.Create(Context);
        Window?.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        Window?.SetDimAmount(0.6f);

        var padding = ToPixels(28);
        var content = new LinearLayout(Context)
        {
            Orientation = Android.Widget.Orientation.Vertical,
            Background = CreateRoundedBackground(colors.Surface, 28),
        };
        content.SetGravity(GravityFlags.CenterHorizontal);
        content.SetPadding(padding, ToPixels(28), padding, padding);

        var title = CreateText(AppStrings.Get(AppStrings.Keys.SetShortcut), 18, colors.Text, bold: true);
        content.AddView(title);

        _instruction = CreateText(CaptureInstruction, 18, colors.Accent, bold: true);
        _instruction.SetPadding(0, ToPixels(30), 0, ToPixels(30));
        content.AddView(_instruction);

        var cancel = new Android.Widget.Button(Context)
        {
            Text = AppStrings.Get(AppStrings.Keys.Cancel),
            TextSize = 18,
            Background = CreateRoundedBackground(colors.Button, 12),
        };
        cancel.SetAllCaps(false);
        cancel.SetTextColor(colors.Text);
        cancel.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        cancel.Click += (_, _) => Dismiss();
        content.AddView(cancel, CreateLayoutParams(height: 56));
        SetContentView(content);
    }

    protected override void OnStart()
    {
        base.OnStart();
        var screenWidth = Context.Resources!.DisplayMetrics!.WidthPixels;
        var textWidth = _instruction is null ? ToPixels(360) : (int)Math.Ceiling(_instruction.Paint!.MeasureText(_instruction.Text));
        var width = Math.Min(textWidth + ToPixels(56), screenWidth - ToPixels(48));
        Window?.SetLayout(width, ViewGroup.LayoutParams.WrapContent);
    }

    public override bool DispatchKeyEvent(KeyEvent e)
    {
        if (e.KeyCode == Keycode.Back)
        {
            Dismiss();
            return true;
        }

        var keyCode = (int)e.KeyCode;
        if (e.Action == KeyEventActions.Down && e.RepeatCount == 0)
        {
            _heldCodes.Add(keyCode);
            UpdateInstruction();
            ScheduleCapture();
        }
        else if (e.Action == KeyEventActions.Up)
        {
            _heldCodes.Remove(keyCode);
            UpdateInstruction();
        }

        return true;
    }

    protected override void OnStop()
    {
        _handler.RemoveCallbacksAndMessages(null);
        _completion.TrySetResult(null);
        base.OnStop();
    }

    private void ScheduleCapture()
    {
        if (_captureScheduled)
        {
            return;
        }

        _captureScheduled = true;
        _handler.PostDelayed(() =>
        {
            _captureScheduled = false;
            if (_heldCodes.Count == 0 || !IsShowing)
            {
                return;
            }

            _completion.TrySetResult(_heldCodes.ToArray());
            Dismiss();
        }, CaptureDelayMilliseconds);
    }

    private int ToPixels(int dp) => (int)(dp * Context.Resources!.DisplayMetrics!.Density + 0.5f);

    private void UpdateInstruction()
    {
        if (_instruction is not null)
        {
            _instruction.Text = _heldCodes.Count == 0 ? CaptureInstruction : Format(_heldCodes);
        }
    }

    private TextView CreateText(string text, float size, Color color, bool bold)
    {
        var view = new TextView(Context)
        {
            Text = text,
            TextSize = size,
            Gravity = GravityFlags.Center,
            TextAlignment = Android.Views.TextAlignment.Center,
        };
        view.SetTextColor(color);
        if (bold)
        {
            view.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        }

        return view;
    }

    private LinearLayout.LayoutParams CreateLayoutParams(int topMargin = 0, int height = ViewGroup.LayoutParams.WrapContent) =>
        new(ViewGroup.LayoutParams.MatchParent, height < 0 ? height : ToPixels(height))
        {
            TopMargin = ToPixels(topMargin),
        };

    private GradientDrawable CreateRoundedBackground(Color color, int cornerRadius)
    {
        var background = new GradientDrawable();
        background.SetColor(color);
        background.SetCornerRadius(ToPixels(cornerRadius));
        return background;
    }

    private static string GetKeyLabel(int keyCode) => keyCode switch
    {
        96 => "A",
        97 => "B",
        99 => "X",
        100 => "Y",
        102 => "LB",
        103 => "RB",
        104 => "LT",
        105 => "RT",
        106 => "L3",
        107 => "R3",
        108 => "Menu",
        109 => "View",
        110 => "Guide",
        _ => ((Keycode)keyCode).ToString()
    };

    private sealed record DialogColors(Color Surface, Color Button, Color Text, Color Accent)
    {
        public static DialogColors Create(Context context)
        {
            var settings = AndroidSettingsStore.Load(context);
            var isDark = settings.ThemeMode == global::Pikslovo.Core.AppThemeMode.Dark ||
                (settings.ThemeMode == global::Pikslovo.Core.AppThemeMode.System &&
                 (context.Resources!.Configuration!.UiMode & Android.Content.Res.UiMode.NightMask) == Android.Content.Res.UiMode.NightYes);
            var accent = global::Pikslovo.App.GetAccentColor(settings.Accent);
            return isDark
                ? new DialogColors(Color.Rgb(48, 45, 55), Color.Rgb(73, 69, 79), Color.Rgb(230, 225, 229), Color.Rgb(accent.R, accent.G, accent.B))
                : new DialogColors(Color.Rgb(247, 247, 249), Color.Rgb(228, 225, 230), Color.Rgb(28, 27, 31), Color.Rgb(accent.R, accent.G, accent.B));
        }
    }
}
