# Raport walidacji wersji 21.38

Data: 2026-07-22

## Sprawdzone automatycznie

- `apps.json` jest poprawnym JSON-em i zawiera 12 aplikacji;
- każda aplikacja ma zakres `machine`;
- Krita jest przypięta do podanego instalatora 5.3.2.1;
- `app.manifest` jest poprawnym XML-em i wymaga administratora;
- kod C# ma zbilansowane nawiasy, poprawnie domknięte komentarze i literały;
- obecne są mechanizmy: odporne pobieranie winget, odinstalowanie winget, Appx dla
  wszystkich i nowych użytkowników, Firefox Maintenance Service, plany zasilania,
  szybkie uruchamianie, skrypty PowerShell, OfficeScrubber, MCPR i publiczne skróty;
- stary plik wykonywalny 21.37 został usunięty z paczki;
- repozytorium lokalne ma commit i tag `v21.38`.

## Wymagane sprawdzenie na Windows

To środowisko nie ma kompilatora .NET Framework ani systemu Windows, więc nie
uruchomiono rzeczywistej kompilacji i instalatorów. Do projektu dołączono:

- `BUILD_AND_RUN.cmd` — lokalna kompilacja i uruchomienie;
- `src/build.cmd` — kompilacja oraz opcjonalne podpisywanie;
- `.github/workflows/build-windows.yml` — kompilacja na `windows-latest` i publikacja
  artefaktu z plikiem EXE.

Przed produkcją należy sprawdzić Windows 10 i Windows 11, konto lokalne i domenowe,
winget obecny/nieobecny, tryb online/offline, instalacje na publicznym pulpicie,
aktualizację Firefox, odinstalowanie winget/Appx oraz zachowanie po restarcie.

## Podpis cyfrowy

Nie dołączono prywatnego certyfikatu. Produkcyjny podpis wymaga certyfikatu Code
Signing i bezpiecznego przekazania go jako PFX lub certyfikatu z magazynu Windows.
Szczegóły znajdują się w `SIGNING.md`.
