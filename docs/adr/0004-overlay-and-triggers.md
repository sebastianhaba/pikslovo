# ADR 0004: Bitmapowa nakladka i trzy triggery

## Status

Zaakceptowane.

## Kontekst

Wynik ma przypominac Decky Translator: zrzut jest widoczny nad gra, a gracz
musi od razu wiedziec, ze oglada zatrzymany obraz. Uzytkownik potrzebuje
przycisku, wlasnego hotkeya i integracji z zewnetrznym menedzerem hotkeyow.

## Decyzja

Renderujemy wynik jako nieruchoma bitmapowa nakladke systemowa. W obszarach
`TextRegion` rysujemy nieprzezroczyste czarne pola i bialy tekst
`TranslatedRegion`; dookola calego obrazu rysujemy czerwona ramke. MVP nie
probkuje tla gry ani nie rekonstruuje grafiki pod tekstem.

Udostepniamy trzy triggery: przycisk plywajacy, AccessibilityService w trybie
przelaczania oraz wyeksportowany publiczny BroadcastReceiver o akcji
`app.pikslovo.action.CAPTURE_AND_TRANSLATE`.

## Konsekwencje

- Czarne pola zapewniaja czytelnosc i sa prostsze niz rekonstrukcja tla.
- Niektore kontrolery lub przyciski systemowe nie wygeneruja zdarzen, ktore
  moze filtrowac AccessibilityService.
- Kazda aplikacja na urzadzeniu moze wyslac publiczny broadcast, dlatego jest
  honorowany tylko w aktywnej sesji i zadania sa serializowane.
