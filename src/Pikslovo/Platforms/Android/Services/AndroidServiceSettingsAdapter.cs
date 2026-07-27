using Android.Content;
using Android.Provider;

namespace Pikslovo.Droid.Services;

internal sealed class AndroidServiceSettingsAdapter(Context context)
{
    public AndroidAppSettings Load() => AndroidSettingsStore.Load(context);

    public void Save(AndroidAppSettings settings) => AndroidSettingsStore.Save(context, settings);

    public bool CanDrawOverlays() => Settings.CanDrawOverlays(context);
}
