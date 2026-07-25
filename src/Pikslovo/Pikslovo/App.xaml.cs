using System;
using Pikslovo.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Uno.Resizetizer;

#if __ANDROID__
using Pikslovo.Droid.Services;
#endif

namespace Pikslovo;

public partial class App : Application
{
    private AppThemeMode _themeMode = AppThemeMode.System;
    private AppAccent _accent = AppAccent.Lavender;
    private readonly ResourceDictionary _accentResources = new();
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
        _accentResources.ThemeDictionaries["Light"] = new ResourceDictionary();
        _accentResources.ThemeDictionaries["Dark"] = new ResourceDictionary();
        _accentResources.ThemeDictionaries["Default"] = new ResourceDictionary();
        Resources.MergedDictionaries.Add(_accentResources);
    }

    protected Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();

        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;

            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        ApplyTheme(rootFrame, LoadThemeMode());
        SetAccent(LoadAccent());

        if (rootFrame.Content == null)
        {
            // When the navigation stack isn't restored navigate to the first page,
            // configuring the new page by passing required information as a navigation
            // parameter
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }

        MainWindow.SetWindowIcon();
        // Ensure the current window is active
        MainWindow.Activate();
    }

    public void SetThemeMode(AppThemeMode mode)
    {
        _themeMode = mode;
        if (MainWindow?.Content is FrameworkElement root)
        {
            ApplyTheme(root, mode);
        }
    }

    public void SetAccent(AppAccent accent)
    {
        _accent = accent;
        var color = GetAccentColor(accent);
        var onPrimary = accent == AppAccent.Lavender
            ? global::Windows.UI.Color.FromArgb(255, 42, 0, 159)
            : global::Windows.UI.Color.FromArgb(255, 28, 27, 31);

        foreach (var dictionary in _accentResources.ThemeDictionaries.Values.OfType<ResourceDictionary>())
        {
            ApplyAccentResources(dictionary, color, onPrimary);
        }
    }

    private AppThemeMode LoadThemeMode()
    {
#if __ANDROID__
        try
        {
            return AndroidSettingsStore.Load(global::Android.App.Application.Context!).ThemeMode;
        }
        catch
        {
            return AppThemeMode.System;
        }
#else
        return AppThemeMode.System;
#endif
    }

    private AppAccent LoadAccent()
    {
#if __ANDROID__
        try
        {
            return AndroidSettingsStore.Load(global::Android.App.Application.Context!).Accent;
        }
        catch
        {
            return AppAccent.Lavender;
        }
#else
        return AppAccent.Lavender;
#endif
    }

    public static global::Windows.UI.Color GetAccentColor(AppAccent accent) => accent switch
    {
        AppAccent.Coral => global::Windows.UI.Color.FromArgb(255, 240, 138, 109),
        AppAccent.Amber => global::Windows.UI.Color.FromArgb(255, 228, 178, 74),
        AppAccent.Lime => global::Windows.UI.Color.FromArgb(255, 170, 207, 91),
        AppAccent.Mint => global::Windows.UI.Color.FromArgb(255, 103, 211, 154),
        AppAccent.Teal => global::Windows.UI.Color.FromArgb(255, 77, 208, 194),
        AppAccent.Aqua => global::Windows.UI.Color.FromArgb(255, 0, 188, 212),
        AppAccent.Sky => global::Windows.UI.Color.FromArgb(255, 107, 182, 232),
        AppAccent.Steel => global::Windows.UI.Color.FromArgb(255, 138, 164, 182),
        AppAccent.Orchid => global::Windows.UI.Color.FromArgb(255, 209, 132, 216),
        AppAccent.Rose => global::Windows.UI.Color.FromArgb(255, 236, 122, 158),
        _ => global::Windows.UI.Color.FromArgb(255, 199, 191, 255)
    };

    private static void ApplyAccentResources(
        ResourceDictionary dictionary,
        global::Windows.UI.Color accent,
        global::Windows.UI.Color onAccent)
    {
        dictionary["PrimaryColor"] = accent;
        dictionary["PrimaryInverseColor"] = accent;
        dictionary["PrimaryContainerColor"] = accent;
        dictionary["SurfaceTintColor"] = accent;
        dictionary["OnPrimaryColor"] = onAccent;
        dictionary["OnPrimaryContainerColor"] = onAccent;

        SetBrush(dictionary, "PrimaryBrush", accent, 255);
        SetBrush(dictionary, "PrimaryHoverBrush", accent, 20);
        SetBrush(dictionary, "PrimaryFocusedBrush", accent, 31);
        SetBrush(dictionary, "PrimaryPressedBrush", accent, 31);
        SetBrush(dictionary, "PrimaryMediumBrush", accent, 163);
        SetBrush(dictionary, "PrimaryLowBrush", accent, 82);
        SetBrush(dictionary, "PrimaryContainerBrush", accent, 255);
        SetBrush(dictionary, "OnPrimaryBrush", onAccent, 255);
        SetBrush(dictionary, "OnPrimaryContainerBrush", onAccent, 255);
        SetBrush(dictionary, "SurfaceTintBrush", accent, 255);
    }

    private static void SetBrush(
        ResourceDictionary dictionary,
        string key,
        global::Windows.UI.Color color,
        byte alpha)
    {
        var value = global::Windows.UI.Color.FromArgb(alpha, color.R, color.G, color.B);
        if (dictionary[key] is SolidColorBrush brush)
        {
            brush.Color = value;
        }
        else
        {
            dictionary[key] = new SolidColorBrush(value);
        }
    }

    private void ApplyTheme(FrameworkElement root, AppThemeMode mode)
    {
        _themeMode = mode;
        root.RequestedTheme = mode switch
        {
            AppThemeMode.Light => ElementTheme.Light,
            AppThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails
    /// </summary>
    /// <param name="sender">The Frame which failed navigation</param>
    /// <param name="e">Details about the navigation failure</param>
    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    /// <summary>
    /// Configures global Uno Platform logging
    /// </summary>
    public static void InitializeLogging()
    {
#if DEBUG
        // Logging is disabled by default for release builds, as it incurs a significant
        // initialization cost from Microsoft.Extensions.Logging setup. If startup performance
        // is a concern for your application, keep this disabled. If you're running on the web or
        // desktop targets, you can use URL or command line parameters to enable it.
        //
        // For more performance documentation: https://platform.uno/docs/articles/Uno-UI-Performance.html

        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());

            // Log to the Visual Studio Debug console
            builder.AddConsole();
#else
            builder.AddConsole();
#endif

            // Exclude logs below this level
            builder.SetMinimumLevel(LogLevel.Information);

            // Default filters for Uno Platform namespaces
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);

            // Generic Xaml events
            // builder.AddFilter("Microsoft.UI.Xaml", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.VisualStateGroup", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.StateTriggerBase", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.UIElement", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.FrameworkElement", LogLevel.Trace );

            // Layouter specific messages
            // builder.AddFilter("Microsoft.UI.Xaml.Controls", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Layouter", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Panel", LogLevel.Debug );

            // builder.AddFilter("Windows.Storage", LogLevel.Debug );

            // Binding related messages
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );

            // Binder memory references tracking
            // builder.AddFilter("Uno.UI.DataBinding.BinderReferenceHolder", LogLevel.Debug );

            // DevServer and HotReload related
            // builder.AddFilter("Uno.UI.RemoteControl", LogLevel.Information);

            // Debug JS interop
            // builder.AddFilter("Uno.Foundation.WebAssemblyRuntime", LogLevel.Debug );
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
