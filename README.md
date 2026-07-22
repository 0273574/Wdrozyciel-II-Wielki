# Wdrożyciel II Wielki

Wersja 21.37 - fixed


> Naprawianie na podstawie wywiadu z pracownikami https://github.com/0273574/Wdrozyciel-II-Wielki/issues/1

Narzędzie do masowych wdrożeń komputerów z systemem Windows. Składa się z jednego
pliku wykonywalnego `Wdrozyciel.exe`, który pobiera aktualne wersje firmowego
zestawu programów do lokalnego repozytorium, a następnie instaluje je na nowych
komputerach w całości offline. Program obsługuje również dołączanie komputera do
domeny Active Directory wraz ze zmianą nazwy komputera.

Aplikacja nie wymaga żadnych zależności — działa na każdym systemie Windows 8/10/11,
ponieważ korzysta wyłącznie z .NET Framework wbudowanego w system.

## Problem, który rozwiązuje

Dotychczas każdy wdrażany komputer pobierał instalatory samodzielnie z internetu.
Przy równoległym wdrażaniu kilku maszyn łącze firmowe było przeciążone, a całość
trwała znacznie dłużej. W nowym modelu instalatory pobierane są jednokrotnie
(w biurze, na serwerze lub stacji z dostępem do internetu), a same wdrożenia odbywają
się w pełni lokalnie — z dysku zewnętrznego, pendrive'a albo udziału sieciowego.
Łącze internetowe obciążane jest raz, niezależnie od liczby wdrażanych komputerów.

## Schemat działania

```
Internet (strony producentów, katalog winget)
        |
        |  1. POBIERZ - uruchamiane w biurze; winget wyszukuje najnowsze
        |     wersje, dla wybranych programów istnieje zapasowy bezpośredni
        |     link producenta
        v
repo\ + manifest.json
   (instalatory + wersje, sumy kontrolne SHA256, zależności,
    argumenty cichej instalacji)
        |
        |  2. Kopiowanie całego folderu na dysk wdrożeniowy
        v
Nowy komputer (offline)
        |
        |  3. ZAINSTALUJ - weryfikacja sum kontrolnych, instalacja
        |     zależności i programów w trybie cichym, zapis logu
        v
Gotowe stanowisko
```

## Instrukcja użycia

### Aktualizacja repozytorium (wymaga internetu)

1. Uruchom `Wdrozyciel.exe` (program poprosi o uprawnienia administratora).
2. Zaznacz programy do zaktualizowania — zaznaczenie kategorii obejmuje całą grupę.
3. Kliknij **POBIERZ aktualne wersje**. Program sprawdzi najnowsze wersje,
   pobierze tylko te, które zmieniły się od ostatniego razu, i zaktualizuje
   plik `manifest.json`.
4. Skopiuj cały folder programu na dysk wdrożeniowy.

### Wdrożenie komputera (offline)

1. Podłącz dysk wdrożeniowy i uruchom z niego `Wdrozyciel.exe`.
2. Zaznacz programy do zainstalowania i kliknij **ZAINSTALUJ zaznaczone**.
3. Program weryfikuje sumę kontrolną każdego instalatora (ochrona przed plikiem
   uszkodzonym przy kopiowaniu), instaluje ewentualne zależności (np. pakiet
   Visual C++ Redistributable wymagany przez LibreOffice), a następnie instaluje
   programy w trybie cichym, bez żadnych okien i klikania.
4. Przebieg całego wdrożenia zapisywany jest w folderze `logs\` — po jednej
   nazwie pliku na komputer i datę, co ułatwia późniejsze rozliczenie prac.

### Dołączenie do domeny

Sekcja "Domena AD" w prawej części okna dotyczy domeny `ad.gliwice.cloud`.
Program co 5 sekund sprawdza, czy kontroler domeny jest osiągalny (połączenie
TCP z portem LDAP 389) i wyświetla wynik: "Domena dostepna" (zielony) lub
"Domena niedostepna" (czerwony).

1. W polu nazwy komputera wpisz docelową nazwę stanowiska (do 15 znaków:
   litery, cyfry, myślnik). Przycisk "Zmien tylko nazwe" pozwala zmienić samą
   nazwę bez dołączania do domeny.
2. Podaj login i hasło konta domenowego uprawnionego do dołączania komputerów.
3. Kliknij **DODAJ DO DOMENY**. Program dołącza komputer do domeny, a jeśli
   podano nową nazwę — zmienia ją razem z kontem komputera w Active Directory.
4. Na koniec wymagane jest jedno ponowne uruchomienie komputera.

## Programy w zestawie

| Kategoria | Programy |
|---|---|
| Przeglądarki | Mozilla Firefox (wersja polska), Google Chrome (pakiet MSI enterprise) |
| Biurowe | Adobe Acrobat Reader, LibreOffice |
| Multimedia | VLC media player |
| Narzędzia | Everything, Visual Studio Code (instalacja dla wszystkich użytkowników) |

## Dodawanie kolejnych programów

Lista programów znajduje się w pliku `apps.json` obok pliku wykonywalnego.
Dodanie programu nie wymaga rekompilacji ani zmian w kodzie.

Krok 1 — znajdź identyfikator pakietu w katalogu winget:

```
winget search 7-zip
```

Kolumna "Id" zawiera identyfikator, np. `7zip.7zip`.

Krok 2 — dopisz wpis do listy `apps` w pliku `apps.json`:

```json
{
  "id": "7zip",
  "name": "7-Zip",
  "category": "Narzedzia",
  "wingetId": "7zip.7zip",
  "exeArgs": "/S",
  "msiArgs": "/qn"
}
```

Krok 3 — uruchom program i kliknij POBIERZ. Nowy program pojawi się na liście
i po pobraniu będzie instalowany offline tak jak pozostałe.

Znaczenie pól:

| Pole | Opis |
|---|---|
| `id` | krótki identyfikator bez spacji; nazwa podfolderu w `repo\` |
| `name` | nazwa wyświetlana na liście |
| `category` | grupa w drzewku; nowa nazwa kategorii tworzy nową grupę automatycznie |
| `wingetId` | identyfikator pakietu z `winget search` |
| `exeArgs` | argumenty cichej instalacji dla instalatorów `.exe` |
| `msiArgs` | argumenty cichej instalacji dla pakietów `.msi` |
| `locale` | opcjonalnie wersja językowa, np. `pl-PL` |
| `scope` | opcjonalnie `machine` — wymusza instalację dla wszystkich użytkowników |
| `directUrl` | opcjonalny bezpośredni adres pobierania, używany gdy winget zawiedzie |

Typowe argumenty cichej instalacji: pakiety `.msi` — `/qn`; instalatory `.exe`
typu NSIS (Firefox, 7-Zip) — `/S`; instalatory typu Inno Setup (Visual Studio
Code) — `/VERYSILENT /NORESTART`. Jeżeli podczas instalacji pojawi się okno
instalatora zamiast instalacji cichej, należy zweryfikować argumenty dla danego
programu.

## Automatyczna aktualizacja repozytorium

Program można uruchomić bez interfejsu graficznego, co pozwala wpiąć aktualizację
repozytorium w Harmonogram zadań systemu Windows (np. codziennie w nocy na
serwerze):

```
Wdrozyciel.exe /download                 aktualizuje wszystkie programy
Wdrozyciel.exe /download firefox,vlc     aktualizuje tylko wskazane
```

Przebieg zapisywany jest w `logs\download-*.log`.

## Kompilacja

Do zbudowania programu nie jest potrzebne Visual Studio ani żaden dodatkowy
pakiet SDK — wykorzystywany jest kompilator C# dostarczany razem z .NET
Framework w każdej instalacji systemu Windows:

```
src\build.cmd
```

Wynikiem jest plik `Wdrozyciel.exe` (ok. 40 KB) w katalogu głównym projektu.

## Struktura katalogu

```
Wdrozyciel.exe      aplikacja
apps.json           lista programów (edytowalna)
manifest.json       generowany: wersje, sumy kontrolne, zależności
repo\               generowany: pobrane instalatory (ok. 1,6 GB dla pełnego zestawu)
logs\               generowany: logi pobrań i wdrożeń
src\                kod źródłowy (Program.cs, app.manifest, build.cmd)
docs\               zrzuty ekranu do dokumentacji
```

Do repozytorium git trafiają wyłącznie źródła, `apps.json` i dokumentacja.
Instalatory, manifest i logi są celowo wykluczone w pliku `.gitignore`.

## Uwagi techniczne

- Sumy kontrolne SHA256 są zapisywane przy pobieraniu i weryfikowane przed każdą
  instalacją; uszkodzony plik nie zostanie zainstalowany.
- Kody wyjścia instalatorów traktowane jako powodzenie: 0, 3010 i 1641 (wymagany
  restart), a dla zależności dodatkowo 1638 (nowsza wersja już zainstalowana).
- Dla Google Chrome program korzysta z zapasowego, bezpośredniego adresu
  producenta, ponieważ katalog winget regularnie zgłasza dla tego pakietu
  niezgodność sumy kontrolnej.
- Dołączanie do domeny wykorzystuje systemowe API `NetJoinDomain`, a zmiana nazwy
  komputera — WMI (`Win32_ComputerSystem.Rename`), dzięki czemu zmiana nazwy
  wykonana po dołączeniu aktualizuje również konto komputera w Active Directory.
