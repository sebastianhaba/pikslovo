# ADR 0002: Jawna aktywna sesja przechwytywania

## Status

Zaakceptowane.

## Kontekst

Hotkey i broadcast sa odbierane, gdy gra jest na pierwszym planie. Android 12+
ogranicza uruchamianie pracy w tle, a MediaProjection wymaga uprzedniej zgody
uzytkownika i foreground service typu `mediaProjection`.

## Decyzja

Uzytkownik jawnie wlacza sesje. Aplikacja uzyskuje MediaProjection, uruchamia
widoczna usluge pierwszoplanowa i utrzymuje VirtualDisplay do pojedynczych
klatek. Trigger tylko zleca przechwycenie w aktywnej sesji. Zatrzymanie sesji
zwalnia MediaProjection i uniewaznia wszystkie triggery.

## Konsekwencje

- Brak ponownego dialogu MediaProjection przy kazdym tlumaczeniu w tej samej
  sesji.
- Trwale powiadomienie jest wymagane i stanowi uczciwa sygnalizacje dzialania.
- Broadcast poza sesja jest bezpiecznie ignorowany.
