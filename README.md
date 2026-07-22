# Wdrożyciel II Wielki 21.38

WinForms/.NET Framework — instalator offline, narzędzia administracyjne, lokalne
skrypty PowerShell i obsługa dołączania komputerów do domeny.

## Najważniejsze zmiany

Wersja 21.38 dodaje Notepad++, Inkscape, GIMP 2.x, Kritę oraz 7-Zip. Naprawiono
przypadek Adobe Reader, w którym brak możliwości odczytania wersji z tekstowego
wyniku `winget show` kończył pobieranie. Teraz aplikacja niezależnie próbuje
`winget download`, następnie wariantów bez locale, scope i architektury oraz —
gdy skonfigurowano — bezpośredniego adresu producenta.

Instalacje są wykonywane z podniesionymi uprawnieniami. Dla MSI dopisywane jest
`ALLUSERS=1`, a po udanej instalacji aplikacja może utworzyć skrót na publicznym
pulpicie. Firefox jest instalowany z Maintenance Service, a po instalacji ustawiane
są polityki automatycznej i działającej w tle aktualizacji.

## Programy

- Mozilla Firefox, Google Chrome, Adobe Acrobat Reader, LibreOffice;
- VLC media player;
- Everything, Visual Studio Code, Notepad++, 7-Zip;
- Inkscape, GIMP 2.x, Krita.

Lista i argumenty instalatorów są w `apps.json`. Krita jest celowo przypięta do
podanego instalatora 5.3.2.1. Pozostałe pozycje korzystające z winget są pobierane
bez wskazywania numeru wersji, czyli z bieżącego manifestu katalogu.

## Skrypty PowerShell

Pliki `.ps1` wrzucaj do folderu `scripts`. Nazwa widoczna w aplikacji jest nazwą
pliku bez rozszerzenia. Na karcie **Skrypty i narzędzia** można je zaznaczać i
uruchamiać. Proces dziedziczy uprawnienia administratora aplikacji. Dołączono
bezpieczny przykład `Przyklad-informacje-o-systemie.ps1`.

## Narzędzia administracyjne

- zapis listy `winget list --accept-source-agreements --disable-interactivity` do `logs`;
- ręczne lub domyślne ID z `winget-remove-defaults.txt` i ciche odinstalowanie przez `winget uninstall`;
- maksymalny stan procesora 100% dla AC i DC we wszystkich widocznych planach;
- wyłączenie szybkiego uruchamiania (`HiberbootEnabled=0`);
- pobranie i interaktywne uruchomienie OfficeScrubber oraz McAfee MCPR;
- usuwanie podanych masek Appx dla wszystkich istniejących użytkowników oraz
  usuwanie pakietów provisioned dla nowych użytkowników.

## Pobieranie i wdrożenie offline

1. Na komputerze z internetem zaznacz programy i kliknij **POBIERZ aktualne wersje**.
2. Aplikacja zapisze instalatory w `repo`, sumy SHA-256 i dane w `manifest.json`.
3. Skopiuj cały katalog na nośnik lub udział sieciowy.
4. Na komputerze docelowym uruchom aplikację jako administrator i kliknij
   **ZAINSTALUJ zaznaczone**.

Tryb bez GUI:

```bat
Wdrozyciel.exe /download
Wdrozyciel.exe /download firefox,vlc,7zip
```

## Kompilacja

Na Windows uruchom `src\build.cmd`. Skrypt używa kompilatora .NET Framework 4.x,
osadza `src\wdrozyciel.ico` i manifest `requireAdministrator`. Opcjonalne
podpisywanie opisano w `SIGNING.md`.

## Kontrola wdrożeniowa

Przed użyciem produkcyjnym wykonaj test na reprezentatywnych Windows 10 i Windows
11, z winget obecnym i nieobecnym, online i offline. Sprawdź logi, publiczny pulpit,
aktualizację Firefox, `winget list`, Appx oraz kody instalatorów 0/3010/1641.


## Raport i automatyczny build

Zakres kontroli opisuje `TEST-REPORT.md`. Workflow `.github/workflows/build-windows.yml` buduje aplikację na Windows i udostępnia gotowy artefakt; opcjonalnie korzysta z sekretów `SIGN_PFX_BASE64` i `SIGN_PFX_PASSWORD`.
