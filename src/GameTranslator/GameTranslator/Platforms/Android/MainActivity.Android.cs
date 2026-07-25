using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using GameTranslator.Droid.Services;

namespace GameTranslator.Droid;

[Activity(
    MainLauncher = true,
    ConfigurationChanges = global::Uno.UI.ActivityHelper.AllConfigChanges,
    WindowSoftInputMode = SoftInput.AdjustNothing | SoftInput.StateHidden
)]
public class MainActivity : Microsoft.UI.Xaml.ApplicationActivity
{
    public static MainActivity? CurrentActivity { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        global::AndroidX.Core.SplashScreen.SplashScreen.InstallSplashScreen(this);

        base.OnCreate(savedInstanceState);
        CurrentActivity = this;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == AndroidTranslationHost.ProjectionRequestCode)
        {
            AndroidTranslationHost.HandleProjectionResult(this, resultCode, data);
            return;
        }

        AndroidTranslationHost.HandleSettingsFileResult(requestCode, resultCode, data);
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(CurrentActivity, this))
        {
            CurrentActivity = null;
        }

        base.OnDestroy();
    }
}
