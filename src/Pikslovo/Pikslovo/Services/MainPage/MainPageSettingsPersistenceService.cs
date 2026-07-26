using System.Threading.Tasks;

#if __ANDROID__
using Pikslovo.Droid.Services;
using global::Android.Content;
#endif

namespace Pikslovo;

internal sealed class MainPageSettingsPersistenceService
{
    public void Load(MainPageViewModel viewModel)
    {
#if __ANDROID__
        viewModel.Apply(AndroidSettingsStore.Load(global::Android.App.Application.Context!));
#else
        viewModel.LoadDefaults();
#endif
    }

    public bool Save(MainPageViewModel viewModel)
    {
#if __ANDROID__
        var context = global::Android.App.Application.Context!;
        var existingSettings = AndroidSettingsStore.Load(context);
        AndroidSettingsStore.Save(context, viewModel.ToAndroidSettings(existingSettings.CaptureRegion));
#endif
        return true;
    }

    public bool HasCompletedOnboarding()
    {
#if __ANDROID__
        return AndroidSettingsStore.HasCompletedOnboarding(global::Android.App.Application.Context!);
#else
        return true;
#endif
    }

    public void CompleteOnboarding()
    {
#if __ANDROID__
        AndroidSettingsStore.CompleteOnboarding(global::Android.App.Application.Context!);
#endif
    }

#if __ANDROID__
    public async Task ExportAsync(global::Android.App.Result resultCode, Intent? data)
    {
        if (resultCode != global::Android.App.Result.Ok || data?.Data is not { } uri)
        {
            return;
        }

        var context = global::Android.App.Application.Context!;
        using var stream = context.ContentResolver?.OpenOutputStream(uri)
            ?? throw new InvalidOperationException(AppStrings.Get("Nie można zapisać wybranego pliku."));
        await SettingsProfile.WriteAsync(
            stream,
            SettingsProfile.FromSettings(AndroidSettingsStore.Load(context)),
            CancellationToken.None);
    }

    public async Task<SettingsProfile?> ImportAsync(global::Android.App.Result resultCode, Intent? data)
    {
        if (resultCode != global::Android.App.Result.Ok || data?.Data is not { } uri)
        {
            return null;
        }

        var context = global::Android.App.Application.Context!;
        using var stream = context.ContentResolver?.OpenInputStream(uri)
            ?? throw new InvalidOperationException(AppStrings.Get("Nie można odczytać wybranego pliku."));
        return await SettingsProfile.ReadAsync(stream, CancellationToken.None);
    }

    public void ApplyProfile(MainPageViewModel viewModel, SettingsProfile profile)
    {
        var context = global::Android.App.Application.Context!;
        AndroidSettingsStore.Save(context, profile.ApplyTo(AndroidSettingsStore.Load(context)));
        viewModel.Apply(AndroidSettingsStore.Load(context));
    }
#endif
}
