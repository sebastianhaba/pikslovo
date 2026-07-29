# Pikslovo

Pikslovo is an Android app for translating text visible inside games and other apps.

It is meant for quick, on-demand translation of what is currently on screen, especially dialogue boxes, menus, and interface text.

## What it does

Pikslovo lets you:

- translate the currently visible text from another app or game,
- show the translation on top of the captured screen,
- trigger translation with a floating button,
- trigger translation with a global hotkey,
- choose whether to translate the whole screen or only a selected region,
- set source and target languages,
- save your settings on the device,
- import and export settings,
- use your own Google Cloud API key.

## How it works

The app captures the current screen, recognizes the visible text, translates it, and shows the result as an overlay.

The workflow is simple:

1. Start a translation session.
2. Open the game or app you want to translate.
3. Trigger translation.
4. Read the translated overlay.

## Main features

- Whole-screen or selected-region translation
- Floating trigger button
- Global hotkey support
- Source and target language selection
- On-device settings storage
- Settings import and export
- Overlay-based result display

## Current scope

The current version focuses on single, manually triggered translations of the current screen content.

It does not aim to be a constant live translator running on every screen update.

## Requirements

- The app runs on Android only.
- The user provides their own Google Cloud API key.
- The app is currently intended for Android 12 and newer.

## Tested devices and systems

Manual testing was performed on the following devices and operating systems:

- ANBERNIC RG 477M running GammaOS
- Samsung Galaxy S21 FE running Android 16

## Credits and inspiration

The original idea for Pikslovo came from using [Decky-Translator](https://github.com/cat-in-a-box/Decky-Translator) on a Steam Deck. Its screenshot-based OCR, translation, and temporary translated-screen overlay inspired this app's translation workflow.

The visual direction of Pikslovo is inspired by [PlayTranslate](https://github.com/dominostars/playtranslate), an Android-focused screen translation app.

## Built with Uno Platform

Pikslovo is built with [Uno Platform](https://platform.uno/), which lets us develop the Android app in C# and XAML.

## How AI was used

This app was not created from a single prompt. It is the result of several weeks of manual testing, iterative fixes, refactoring, and many prompts. Codex and OpenGo were used as part of an experiment in using AI tools to build a complete Android application that looks polished while using XAML and C#.
