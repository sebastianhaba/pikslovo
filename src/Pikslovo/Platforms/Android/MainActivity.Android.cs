using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
using Pikslovo.Droid.Services;

namespace Pikslovo.Droid;

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
        HandleIntent(Intent);
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

    public override void OnRequestPermissionsResult(int requestCode, string[]? permissions, Permission[]? grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        AndroidTranslationHost.HandlePermissionRequestResult(requestCode, this);
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        if (AppStrings.LanguageMode != global::Pikslovo.Core.AppLanguageMode.System)
        {
            return;
        }

        AndroidTranslationHost.RefreshFloatingTriggerConfiguration(this);
        (global::Microsoft.UI.Xaml.Application.Current as App)?.ReloadMainPage();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        HandleIntent(intent);
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(CurrentActivity, this))
        {
            CurrentActivity = null;
        }

        base.OnDestroy();
    }

    private void HandleIntent(Intent? intent)
    {
        if (intent is null)
        {
            return;
        }

        if (intent.GetBooleanExtra(AndroidTranslationHost.OpenSettingsImportExtra, false))
        {
            intent.RemoveExtra(AndroidTranslationHost.OpenSettingsImportExtra);
            AndroidTranslationHost.OpenSettingsImportFile(this);
        }
    }
}
