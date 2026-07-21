# ADR 0001: C# z Uno Platform oraz adaptery Android

## Status

Zaakceptowane.

## Kontekst

Aplikacja ma byc wygodna w C#, a jednoczesnie wymaga Android MediaProjection,
WindowManager overlay, foreground service, BroadcastReceiver i opcjonalnie
AccessibilityService.

## Decyzja

Uzywamy Uno Platform do wizardu, ustawien i wspolnej logiki C#. Funkcje
systemowe sa za portami C# i maja implementacje Android-specific w tym samym
rozwiazaniu. Nie probujemy modelowac MediaProjection ani AccessibilityService
wylacznie przez wspolne API UI.

## Konsekwencje

- Wiekszosc logiki potoku OCR/tlumaczenie/renderowanie jest testowalna jako .NET.
- Konieczna jest znajomosc Android SDK dla kodu hosta.
- Przyszle platformy moga wspoldzielic UI i domeny, ale wymagaja wlasnych
  adapterow przechwytywania i nakladki.
