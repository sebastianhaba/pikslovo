using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace GameTranslator.Droid.Services;

internal sealed class HotkeyCaptureDialog : Dialog
{
    private const int CaptureDelayMilliseconds = 2000;
    private readonly TaskCompletionSource<int[]?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly SortedSet<int> _heldCodes = [];
    private TextView? _hotkeyValue;
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
        return labels.Length == 0 ? "Nie ustawiono" : string.Join(" + ", labels);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetTitle("Ustaw skrót");
        SetCanceledOnTouchOutside(true);

        var padding = ToPixels(24);
        var content = new LinearLayout(Context)
        {
            Orientation = Android.Widget.Orientation.Vertical,
        };
        content.SetPadding(padding, 0, padding, padding);

        var instruction = new TextView(Context)
        {
            Text = "Przytrzymaj wybrany klawisz albo kombinację przez 2 sekundy.",
        };
        content.AddView(instruction);

        _hotkeyValue = new TextView(Context)
        {
            Text = "Czekam na klawisz...",
            Gravity = GravityFlags.CenterHorizontal,
        };
        _hotkeyValue.SetPadding(0, ToPixels(24), 0, ToPixels(24));
        content.AddView(_hotkeyValue);

        var cancel = new Android.Widget.Button(Context) { Text = "Anuluj" };
        cancel.Click += (_, _) => Dismiss();
        content.AddView(cancel);
        SetContentView(content);
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
            UpdateHotkeyValue();
            ScheduleCapture();
        }
        else if (e.Action == KeyEventActions.Up)
        {
            _heldCodes.Remove(keyCode);
            UpdateHotkeyValue();
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

    private void UpdateHotkeyValue()
    {
        if (_hotkeyValue is not null)
        {
            _hotkeyValue.Text = _heldCodes.Count == 0 ? "Czekam na klawisz..." : Format(_heldCodes);
        }
    }

    private int ToPixels(int dp) => (int)(dp * Context.Resources!.DisplayMetrics!.Density + 0.5f);

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
}
