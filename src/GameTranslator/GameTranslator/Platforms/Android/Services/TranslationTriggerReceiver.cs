using Android.App;
using Android.Content;

namespace GameTranslator.Droid.Services;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([TranslationForegroundService.CaptureAndTranslateAction])]
public sealed class TranslationTriggerReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action != TranslationForegroundService.CaptureAndTranslateAction ||
            !TranslationForegroundService.IsSessionActive)
        {
            return;
        }

        var serviceIntent = new Intent(context, typeof(TranslationForegroundService));
        serviceIntent.SetAction(TranslationForegroundService.CaptureAndTranslateAction);
        context.StartService(serviceIntent);
    }
}
