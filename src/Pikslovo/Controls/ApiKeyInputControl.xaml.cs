using Microsoft.UI.Xaml.Input;

namespace Pikslovo.Controls;

public sealed partial class ApiKeyInputControl : UserControl
{
    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(
            nameof(Password),
            typeof(string),
            typeof(ApiKeyInputControl),
            new PropertyMetadata(string.Empty, OnPasswordPropertyChanged));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(ApiKeyInputControl),
            new PropertyMetadata(string.Empty, OnPlaceholderTextPropertyChanged));

    private bool _isPasswordVisible;
    private bool _isSynchronizingPassword;

    public ApiKeyInputControl()
    {
        InitializeComponent();
    }

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public event RoutedEventHandler? PasswordChanged;
    public event KeyEventHandler? PasswordSubmitted;

    public void FocusPrimaryInput(FocusState focusState = FocusState.Programmatic) =>
        PasswordInput.Focus(focusState);

    private static void OnPasswordPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ApiKeyInputControl control || control._isSynchronizingPassword)
        {
            return;
        }

        var newValue = args.NewValue as string ?? string.Empty;
        if (control.PasswordInput.Password == newValue)
        {
            return;
        }

        control._isSynchronizingPassword = true;
        try
        {
            control.PasswordInput.Password = newValue;
        }
        finally
        {
            control._isSynchronizingPassword = false;
        }
    }

    private static void OnPlaceholderTextPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ApiKeyInputControl control)
        {
            control.PasswordInput.PlaceholderText = args.NewValue as string ?? string.Empty;
        }
    }

    private void RevealButton_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        PasswordInput.PasswordRevealMode = _isPasswordVisible ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
        HideSlash.Visibility = _isPasswordVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isSynchronizingPassword)
        {
            _isSynchronizingPassword = true;
            try
            {
                SetValue(PasswordProperty, PasswordInput.Password);
            }
            finally
            {
                _isSynchronizingPassword = false;
            }
        }

        PasswordChanged?.Invoke(this, e);
    }

    private void PasswordInput_KeyDown(object sender, KeyRoutedEventArgs e) =>
        PasswordSubmitted?.Invoke(this, e);
}
