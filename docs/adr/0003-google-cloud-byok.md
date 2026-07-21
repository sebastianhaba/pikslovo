# ADR 0003: Wlasny klucz Google Cloud i REST API

## Status

Zaakceptowane.

## Kontekst

MVP ma dzialac podobnie do Decky Translator: uzytkownik sam podaje klucz,
bez backendu aplikacji. Potrzebne sa OCR oraz tlumaczenie.

## Decyzja

Wizard przyjmuje pojedynczy klucz API. Aplikacja uzywa Cloud Vision
`images:annotate` z `TEXT_DETECTION` oraz Cloud Translation Basic v2
`language/translate/v2`. Klucz jest szyfrowany lokalnie; ekran konfiguracji
instruuje uzytkownika, aby wlaczyl obie uslugi i ograniczyl klucz.

## Konsekwencje

- Uzytkownik ponosi koszty i zarzadza limitem w swoim projekcie Google Cloud.
- Nie przechowujemy sekretow po stronie projektu ani nie potrzebujemy konta.
- API key w aplikacji nie jest absolutnym sekretem; klucz musi byc prywatny,
  ograniczony do wymaganych API i nie moze trafic do zrodla ani logow.
- V2 jest proste dla MVP, ale interfejs `ITranslationProvider` pozwoli pozniej
  dodac Translation v3 lub innego dostawce.
