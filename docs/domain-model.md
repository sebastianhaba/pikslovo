# Model domeny

## Pojecia

| Pojecie | Znaczenie |
| --- | --- |
| `Configuration` | Klucz API, jezyk zrodlowy, jezyk docelowy, prog pewnosci OCR, skala czcionki, ukrywanie identycznych tlumaczen i ustawienia triggerow. |
| `TranslationSession` | Jawnie wlaczony cykl zycia MediaProjection, uslugi pierwszoplanowej i triggerow. |
| `Trigger` | Zdarzenie rozpoczynajace lub konczace pokazanie wyniku: przycisk, hotkey albo broadcast. |
| `CaptureFrame` | Tymczasowa bitmapa pojedynczej klatki z VirtualDisplay. |
| `OcrDocument` | Strukturalna odpowiedz Vision API: tekst oraz wielokaty/ramki jednostek tekstu. |
| `TextRegion` | Jednostka do tlumaczenia i narysowania: tekst OCR oraz jego obszar w pikselach klatki. |
| `TranslatedRegion` | `TextRegion` wzbogacony o przetlumaczony tekst. |
| `OverlayFrame` | Zrzut plus wyrenderowane pola tlumaczen i czerwona ramka. |

## Agregaty i wlasciciele stanu

- `Configuration` jest trwala konfiguracja urzadzenia. Jej sekret jest
  przechowywany jako zaszyfrowana wartosc w `SharedPreferences`, a klucz
  szyfrujacy pochodzi z Android Keystore; pozostale ustawienia sa trzymane
  zwykle. Zaleznie od platformowego Keystore konfiguracja nie jest
  przenoszalna przez systemowy restore, dlatego backup danych aplikacji jest
  wylaczony. Prog pewnosci OCR ma zakres od `0` do `1` i domyslnie wynosi
  `0.6`; regiony ponizej progu nie sa wysylane do tlumaczenia. Skala czcionki
  nakladki ma zakres od `1.0` do `3.0` i domyslnie wynosi `1.0`. Opcja
  ukrywania identycznych tlumaczen jest domyslnie wylaczona; po wlaczeniu nie
  rysujemy regionu, gdy tekst OCR i tlumaczenie sa rowne po usunieciu bialych
  znakow na brzegach i bez rozrozniania wielkosci liter.
- `TranslationSession` jest jedynym wlascicielem MediaProjection, uslugi
  pierwszoplanowej, uchwytu VirtualDisplay, nakladki i anulowania biezacej
  operacji.
- `TranslationJob` jest efemeryczny i ma wlasny `CaptureFrame`, `OcrDocument`
  oraz `TranslatedRegion`. Konczy sie zwolnieniem wszystkich danych.

## Stany sesji

```text
Unconfigured -> Ready -> RequestingProjection -> Active -> Stopping -> Ready
                         |                         |
                         v                         v
                      Ready                     Error -> Active/Ready
```

`Active` utrzymuje usluge pierwszoplanowa oraz zasoby projekcji. Tylko w tym
stanie system przyjmuje trigger i tworzy `TranslationJob`.

## Stany zadania

```text
Idle -> Capturing -> Recognizing -> Translating -> Rendering -> Showing
  ^                                                      |
  +-------------------------- Dismissed/Failed ----------+
```

Drugie zdarzenie triggera podczas `Capturing`, `Recognizing`, `Translating`
lub `Rendering` jest ignorowane. W `Showing` semantyka zalezy od triggera:
tryb przelaczany ukrywa wynik, a zwolnienie klawisza w trybie podgladu go
ukrywa.

## Granice portow

```text
TranslationOrchestrator
  -> IScreenCapture
  -> IOcrProvider
  -> ITranslationProvider
  -> IOverlayRenderer
  -> IOverlayPresenter

AndroidSessionHost
  -> IMediaProjectionGateway
  -> IForegroundServiceHost
  -> IOverlayPermissionGateway
  -> IGlobalHotkeyGateway
  -> IExternalTriggerGateway
```

`TranslationOrchestrator` i modele powyzsze pozostaja czystym C# testowanym
bez Androida. Implementacje MediaProjection, WindowManager, AccessibilityService
i BroadcastReceiver naleza do projektu Android. Widoki wizardu oraz ustawien
naleza do Uno Platform.
