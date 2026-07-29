Pikslovo uses an Android Accessibility Service to receive the hardware key
events used by the optional global hotkey. Enabling this service is required
only when you want to use the global hotkey; the floating button and broadcast
trigger do not need it.

## Enable it in Android Settings

1. Open **Pikslovo** and go to **Settings**.
2. Select **Accessibility settings**. Pikslovo opens the relevant Android
   Settings page.
3. Select **Pikslovo** from the list of downloaded or installed services.
4. Turn on **Use Pikslovo** and confirm Android's warning.
5. Return to Pikslovo, choose the hotkey, and enable **Global hotkey**.

The hotkey works only while a translation session is active. Android does not
forward every physical button as a key event, so some device-specific buttons
and controller buttons cannot be used as hotkeys.

## If Pikslovo is blocked as a restricted setting

On Android 13 and later, Android can block Accessibility Services in apps that
were installed from an APK, a browser download, or another non-store source.
The Pikslovo entry may be disabled, or Android may say that the setting is
currently unavailable for your security.

First try the standard Android procedure:

1. Open **Settings** > **Apps** > **Pikslovo**.
2. Open the overflow menu (the three dots in the top-right corner).
3. Select **Allow restricted settings** and confirm the warning.
4. Go back to Pikslovo's **Accessibility settings** and enable **Pikslovo**.

Google's instructions are available at [Allow restricted settings](https://support.google.com/android/answer/12623953).

Some Android variants, including some GammaOS builds, do not show **Allow
restricted settings**. Use the ADB procedure below only if the normal Android
option is absent.

## Advanced: unblock the setting with ADB

This procedure requires a computer with Android Platform Tools and USB
debugging enabled on the phone. It does not require root. It changes a
security-related Android setting for Pikslovo, so do this only for an APK that
you obtained from a source you trust.

1. On the phone, enable **Developer options** if they are not already visible:
   open **Settings** > **About phone** and tap **Build number** seven times.
2. Open **Settings** > **System** > **Developer options** and enable **USB
   debugging**.
3. Connect the phone to the computer by USB. Accept the **Allow USB debugging**
   fingerprint prompt on the phone.
4. In a terminal, confirm that ADB sees the device:

   ```sh
   adb devices
   ```

   Continue only when the device is listed as `device`, not `unauthorized`.
5. Allow Pikslovo to use the restricted Accessibility setting:

   ```sh
   adb shell cmd appops set app.pikslovo ACCESS_RESTRICTED_SETTINGS allow
   ```

6. Optional: verify the App Op value:

   ```sh
   adb shell cmd appops get app.pikslovo ACCESS_RESTRICTED_SETTINGS
   ```

   The result should show `allow` or `allowed`.
7. Open Pikslovo and repeat the normal steps in [Enable it in Android Settings](#enable-it-in-android-settings).
8. When finished, turn off **USB debugging** in Developer options if you do not
   otherwise need it.

The ADB command only removes Android's restricted-settings block. It does not
enable the service automatically, set a hotkey, start a translation session, or
grant root access. You must still explicitly enable the service in Android
Settings and select the hotkey in Pikslovo.

## Security note

Accessibility Services are powerful Android components. Enable Pikslovo only
if you trust the installed APK and its source. Pikslovo's service uses key
events for the configured hotkey and reacts only while a translation session is
active, but Android displays a broad warning because Accessibility Services can
potentially access on-screen content and interact with the device.

## Troubleshooting

- **`unauthorized` in `adb devices`:** unlock the phone and accept the USB
  debugging prompt, then run the command again.
- **`no devices/emulators found`:** check the USB cable and connection mode,
  then restart ADB with `adb kill-server` followed by `adb start-server`.
- **The setting is still disabled:** make sure the command uses the exact
  package name `app.pikslovo`, then close and reopen Android Settings.
- **The hotkey does nothing:** confirm that the Accessibility Service is on,
  that a hotkey is selected and enabled in Pikslovo, and that a translation
  session is running. Try a different hardware key because not all keys are
  delivered to Accessibility Services.
