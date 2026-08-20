# Historia zmian

Format oparty luźno na [Keep a Changelog](https://keepachangelog.com/pl/).

## [Niewydane] — poprawki domeny / GPO / winget / Firefox

> Wersja aplikacji pozostaje **21.37** (celowo, na stałe).

- **dołączanie do domeny**: próba dołączenia najpierw do **istniejącego** konta komputera
  (jak kreator Ustawień Windows), a dopiero potem tworzenie nowego konta — usuwa błąd
  „przekroczono limit kont komputerów" (kod 8557) i konflikt z kontem już istniejącym w AD;
- dodano czytelny opis kodu **8557** (limit `ms-DS-MachineAccountQuota`);
- dodano przycisk **Weryfikuj domenę / GPO / ESET**: `gpupdate`, lista zastosowanych
  polityk domenowych (rejestr History + `gpresult`) oraz wykrycie ESET (usługa `ekrn`,
  katalog `Program Files\ESET`, wpis odinstalowania) wdrażanego przez GPO;
- naprawiono `winget download 0x8a15000f` („Data required by the source is missing”):
  po nieudanym `source update` wykonywany jest `source reset --force` i ponowna aktualizacja;
- **Firefox**: instalacja wymuszona do `Program Files` (plik INI) i wyłączenie własnego
  skrótu instalatora — koniec z dwoma skrótami i instalacją poza katalogiem systemowym;
- dodano pole `iniConfig` w `apps.json` (konfiguracja INI dla instalatorów EXE);
- utwardzono usuwanie winget (czytelne kody wyjścia) i dodano dokumentację
  [`docs/USUWANIE-WINGET.md`](docs/USUWANIE-WINGET.md).

## [21.39]

- ujednolicono przenośne dane aplikacji w ukrytym katalogu `.wdrozyciel`;
- OfficeScrubber pozwala wybrać zalecane czyszczenie fabrycznie preinstalowanego
  Office (Microsoft 365/Click-to-Run i UWP) albo pełne usunięcie wszystkich wersji Office;
- pełne czyszczenie wymaga dodatkowego potwierdzenia;
- OfficeScrubber jest uruchamiany w tle interfejsu, a aplikacja czeka na jego wynik
  i zapisuje kod zakończenia;
- przed uruchomieniem OfficeScrubbera sprawdzana jest znana suma SHA-256.

## [21.38] — 2026-07-22

- dodano Notepad++, Inkscape, GIMP, Krita i 7-Zip;
- naprawiono Adobe Reader: brak odczytanej wersji z `winget show` nie blokuje już `winget download`;
- dodano wykrywanie winget, odświeżanie źródła i próby awaryjne bez locale, scope oraz architektury;
- dodano instalację maszynową, `ALLUSERS=1` dla MSI i publiczne skróty;
- dodano polityki aktualizacji Firefox i Mozilla Maintenance Service;
- dodano wybieralne lokalne skrypty PowerShell uruchamiane jako administrator;
- dodano `winget list`, procesor 100% AC/DC, wyłączenie szybkiego uruchamiania,
  OfficeScrubber, McAfee MCPR i usuwanie Appx dla obecnych oraz nowych użytkowników;
- dodano ikonę aplikacji i opcjonalne podpisywanie Authenticode.

## [21.37] — wydanie sprawne/offline

- dodano jeden proces **PRZYGOTUJ KOMPUTER (offline)** z kontrolą kompletu plików;
- dodano pomijanie aplikacji zainstalowanych w tej samej lub nowszej wersji;
- dodano opcje wyłączenia szybkiego uruchamiania, czyszczenia fabrycznego Office
  i otwarcia Windows Update po lokalnych instalacjach;
- MCPR korzysta z lokalnego, zweryfikowanego pliku i nie wymaga internetu;
- zmiana nazwy jest wykonywana przed dołączeniem do domeny, a nietypowy format nazwy
  wymaga potwierdzenia;
- po zmianie nazwy lub domeny aplikacja proponuje restart przy zamykaniu;
- numer produktu i pliku wykonywalnego pozostaje zgodnie z wymaganiem `21.37`.
