# Pikslovo - Specyfikacja MVP

## Cel

Pikslovo jest aplikacja Android dla graczy, ktora tlumaczy tekst widoczny
w innej aplikacji lub grze. Uzytkownik uruchamia aktywna sesje, wywoluje
tlumaczenie przyciskiem plywajacym, globalnym hotkeyem albo broadcastem, a
aplikacja wyswietla zatrzymany zrzut ekranu z nalozonym tlumaczeniem.

Pierwsza wersja uzywa wylacznie Google Cloud Vision API do OCR i Cloud
Translation Basic v2 do tlumaczenia. Konfiguracja oraz ekran ustawien beda
zbudowane w Uno Platform i C#; integracje systemowe Androida pozostaja
platform-specific za interfejsami C#.

## Zakres MVP

W zakresie:

- Android 12 (API 31) i nowsze.
- Wizard pierwszego uruchomienia.
- Wlasny klucz Google Cloud API podawany przez uzytkownika.
- Wybor jezyka zrodlowego i docelowego.
- Aktywna sesja MediaProjection z widocznym powiadomieniem.
- Opcjonalny, zdefiniowany przez użytkownika obszar przechwycenia dialogu.
- Wywolanie tlumaczenia: przycisk plywajacy, globalny hotkey oraz publiczny
  broadcast.
- OCR przez Vision `DOCUMENT_TEXT_DETECTION`, aby uzyskac strukture tekstu i
  wartosci pewnosci dla regionow OCR.
- Tlumaczenie przez Translation Basic v2.
- Pelnoekranowa nakladka ze zrzutem, czerwona ramka i nieprzezroczystymi
  czarnymi polami tlumaczen w polozeniach tekstu rozpoznanego przez OCR.
- Zamkniecie nakladki przez ponowne wywolanie w trybie przelaczanym, przycisk
  zamkniecia albo ukonczenie sesji.
- Dane robocze tylko w pamieci RAM.

Poza zakresem:

- Automatyczne, ciagle tlumaczenie zmian dialogu.
- Historia, eksport i zapis zrzutow.
- OCR i tlumaczenie offline.
- Konta aplikacji, backend posredniczacy i wspoldzielone pozycje API.
- Dystrybucja przez Google Play.

## Przeplyw uzytkownika

1. Wizard wyjasnia, ze zrzut ekranu i tekst OCR beda wysylane do Google Cloud.
2. Uzytkownik wpisuje wlasny klucz API, wybiera jezyk zrodlowy i docelowy oraz
   moze otworzyc instrukcje wlaczenia Cloud Vision API i Cloud Translation API.
3. Uzytkownik przyznaje dostep do wyswietlania nad innymi aplikacjami. Dla
   globalnego hotkeya przyznaje osobno dostep do uslugi dostepnosci.
4. Klikniecie `Wlacz tlumacza` uruchamia systemowy dialog MediaProjection,
   a po jego akceptacji aktywna sesje i trwale powiadomienie.
5. Trigger zleca pojedyncze tlumaczenie. Aplikacja pobiera biezaca klatke,
   wysyla ja do Vision, tlumaczy wykryte jednostki tekstu i buduje bitmapowa
   nakladke.
6. Nakladka zatrzymuje widok na zrzucie ekranu, rysuje czerwona ramke oraz
   czarne prostokaty z bialym tlumaczeniem w miejscach oryginalnego tekstu.
   Gra nie jest pauzowana przez aplikacje.
7. Uzytkownik ukrywa nakladke i wraca do gry. Bufory obrazu, OCR oraz
   tlumaczenia sa zwalniane.

## Wywolania

### Przycisk plywajacy

W aktywnej sesji widoczny jest niewielki przycisk systemowy. Jego klikniecie
uruchamia lub ukrywa wynik, zalezne od aktualnego stanu.

### Globalny hotkey

Uzytkownik wybiera klawisz lub kombinacje wysylana przez jego urzadzenie.
AccessibilityService dziala w trybie przelaczania: nacisniecie pokazuje wynik,
a kolejne go ukrywa.

Nie kazdy fizyczny przycisk urzadzenia jest dostepny dla uslug dostepnosci;
MVP obsluguje tylko zdarzenia przekazywane przez Android jako key events.

### Publiczny broadcast

Zewnetrzny menedzer hotkeyow moze wyslac broadcast:

```text
action: app.pikslovo.action.CAPTURE_AND_TRANSLATE
package: app.pikslovo
```

Odbiornik jest wyeksportowany, bez tokenu, i ignoruje zadanie, jezeli aktywna
sesja nie dziala albo nakladka jest juz w trakcie przetwarzania. Publiczne API
jest wygodne dla wlasnego urzadzenia, ale dowolna zainstalowana aplikacja moze
je wywolac; dokumentacja musi to wyraznie zaznaczac.

## Wymagania niefunkcjonalne

- Klucz API jest przechowywany lokalnie jako zaszyfrowana wartosc w
  `SharedPreferences`; klucz szyfrujacy jest generowany i trzymany w Android
  Keystore. Sekret nigdy nie trafia do logow ani analityki.
- Backup danych aplikacji jest wylaczony. Konfiguracja zalezy od materialu
  klucza w Android Keystore, ktory nie jest traktowany jako przenaszalny stan
  aplikacji przy restore lub migracji urzadzenia.
- Klucz nalezy ograniczyc w Google Cloud do Cloud Vision API i Cloud Translation
  API oraz, gdy obsluga danego API na to pozwala, do podpisanego pakietu Android.
- Zrzut i wynik OCR sa trzymane w pamieci tylko do zamkniecia nakladki lub
  wystapienia bledu. Nie zapisujemy ich do galerii, cache ani bazy.
- Zadanie jest pojedyncze: drugi trigger podczas OCR, tlumaczenia lub
  renderowania nie tworzy rownoleglej pracy.
- Komunikaty bledow musza odroznic: brak polaczenia, odmowe MediaProjection,
  brak klucza, blad/autoryzacje Google API, pusty wynik OCR i wycofane
  uprawnienie nakladki.

## Kryteria akceptacji

1. Na Androidzie 12+ uzytkownik konfiguruje klucz i jezyki w wizardzie.
2. Po uruchomieniu sesji system pokazuje standardowa zgode na udostepnianie
   ekranu i widoczne jest powiadomienie aktywnej sesji.
3. Kazdy z trzech triggerow w aktywnej sesji wywoluje pojedyncza operacje.
4. Dla poprawnej odpowiedzi Google wynik zawiera zrzut, czerwona ramke oraz
   biale tlumaczenia na czarnych polach zgodnych z pozycjami OCR.
5. Ponowne wywolanie w trybie przelaczanym, zamkniecie lub zatrzymanie sesji
   usuwa nakladke i wszystkie dane robocze z pamieci.
6. Bez aktywnej sesji broadcast nie przechwytuje ekranu i nie wywoluje Google API.
