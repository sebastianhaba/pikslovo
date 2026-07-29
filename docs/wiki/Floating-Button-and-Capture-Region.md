# Floating button and capture region guide

The floating button is Pikslovo's on-screen trigger. It appears over other
apps only while a translation session is active, so it never appears merely
because Pikslovo is installed or open. The session also needs Android's
permission to display over other apps.

A normal tap starts a screen capture and translation. While a translation is
being prepared, the button is disabled and shows a progress icon. When the
translated-screen overlay is visible, tap the button again to close the overlay
and return to the game.

## When the floating button is visible

Open **Settings** > **Floating button** to control the button's visibility,
size, and position.

The button is visible during every active session by default. The **Always show
button** switch is on by default.

You can hide the button during an active session only when a working **Global
hotkey** is enabled. To do that:

1. Set up the global hotkey in **Settings** > **Global hotkey** and enable the
   Pikslovo Accessibility Service in Android settings. See
   [Enable Accessibility for Global Hotkey](Enable-Accessibility-for-Global-Hotkey.md)
   for the full setup instructions.
2. Choose a shortcut and turn on **Global hotkey**.
3. Open **Settings** > **Floating button** and turn off **Always show button**.

With **Always show button** off and the global hotkey enabled, the button is
hidden and cannot be tapped. The hotkey remains available to trigger a
translation and to close a visible result. This prevents the app from leaving
you without an in-game trigger.

If the global hotkey is disabled, turning off **Always show button** does not
hide the button: Pikslovo keeps it visible so that the active session still has
an on-screen trigger. Turning **Always show button** back on makes it visible
regardless of the hotkey setting.

## Floating button settings

### Always show button

**Default:** on

Controls whether the button remains visible during an active session even when
a global hotkey is enabled.

- Keep it on when you want both touch and hardware-key controls.
- Turn it off only after confirming that your global hotkey works in the game
  you use. This provides an unobstructed game screen.

### Button size

**Default:** `1.0x`
**Range:** `0.5x` to `2.0x`, in `0.1x` steps

Changes the size of the main floating button and its menu actions.

- Use a smaller value if it covers important game UI.
- Use a larger value if the button is difficult to tap.

### Horizontal position

**Default:** `0.97`

Moves the button from left (`0.00`) to right (`1.00`) within the usable screen
width. The default places it close to the right edge.

### Vertical position

**Default:** `0.20`

Moves the button from top (`0.00`) to bottom (`1.00`) within the usable screen
height. The default places it near the top.

The two position settings use proportions rather than fixed pixels, so the
placement adapts to screen size and orientation. Adjust them to avoid gameplay
controls, subtitles, or status indicators.

## Opening the floating-button menu

Long-press the floating button to open its extra-actions menu. Long-press it
again to close the menu. A normal tap while the menu is open closes the menu
and then performs the usual capture/translation action.

The menu cannot be opened while Pikslovo is already processing a translation.
It opens above or below the button depending on the available space.

The menu contains these actions:

- **Edit capture region** opens the region selector described below.
- **Import settings** opens Pikslovo and Android's file picker so that you can
  choose a previously exported Pikslovo settings JSON file. Importing settings
  replaces the supported saved settings; it does not import a Google Cloud API
  key.
- **Back to settings** returns to the main Pikslovo settings screen.
- **Stop translator** ends the active translation session, removes the
  floating button and overlays, and stops screen capture. Start the translator
  again from Pikslovo when you need it.

## Capture region

By default, Pikslovo sends the full captured screen to OCR. A capture region
lets you select only the part of the screen where dialogue or other text
normally appears.

Using a region can help when a game has a stable dialogue panel, subtitles, or
a text box in one location. It has three main benefits:

- Cloud Vision receives less irrelevant UI, artwork, and decorative text, so
  there are fewer unwanted OCR results.
- A smaller image may encode and upload faster and use less data.
- The translation overlay is less likely to cover unrelated on-screen labels.

The original full-screen screenshot remains behind the overlay. The selected
area is cropped only for OCR, and Pikslovo places the recognised and translated
text back at the correct location on the full-screen overlay.

### Set or change the region

1. Start a translation session.
2. Long-press the visible floating button.
3. Select **Edit capture region**.
4. Drag a corner handle to resize the outlined rectangle. Drag inside the
   rectangle to move the whole region.
5. Tap the checkmark to save the selected region.

The selection is saved and used by future translations. The smallest allowed
region is five percent of the screen width and height, which prevents an empty
or unusably small crop.

While selecting a region, the current translation overlay and floating button
are temporarily hidden so they do not interfere with the selection. Tap the
cross to cancel and keep the previously saved region.

### Return to full-screen OCR

In the region selector, tap the full-screen icon between the cancel cross and
the save checkmark. This resets the selection to the entire screen. Save with
the checkmark to disable the custom region.

Use full-screen OCR when text can appear in different places, such as menus,
tooltips, item descriptions, or changing dialogue layouts.
