# Raport kontroli wersji 21.38

Data kontroli: 2026-07-22

## Zrealizowane kontrole statyczne

- poprawność składni JSON plików `apps.json` i `manifest.json`;
- poprawność XML manifestu aplikacji oraz poziom `requireAdministrator`;
- zgodność identyfikatorów aplikacji między konfiguracją i kodem;
- obecność ikony, folderu `scripts`, przykładowych skryptów oraz dołączonego OfficeScrubbera;
- kontrola sumy SHA-256 OfficeScrubbera;
- kontrola ścieżek awaryjnych `winget download` i pobierania bezpośredniego;
- kontrola, że wybór programów i ustawienie weryfikacji SHA-256 są kopiowane z interfejsu przed uruchomieniem wątku roboczego;
- kontrola komend Appx dla istniejących i przyszłych użytkowników;
- lokalny commit i tag `v21.38`.

## Testy wymagające Windows

W tym środowisku nie było systemu Windows ani kompilatora .NET Framework, więc nie wykonano uruchomienia GUI ani rzeczywistych instalacji. Na komputerze Windows uruchom `BUILD.cmd`, a następnie sprawdź Windows 10 i Windows 11, z winget obecnym i nieobecnym, online i offline. Szczególnie potwierdź kody instalatorów, publiczny pulpit, Firefox Maintenance Service, MCPR, OfficeScrubber i usuwanie Appx.

Dołączony workflow `.github/workflows/build-windows.yml` buduje EXE na runnerze Windows i publikuje artefakt po pushu do GitHub.

## Podpis

Pakiet nie zawiera prywatnego certyfikatu. `src\build.cmd` i `src\sign.ps1` obsługują podpis PFX lub certyfikat z magazynu Windows. `src\create-dev-cert.ps1` tworzy certyfikat testowy, który wymaga osobnego wdrożenia zaufania na stacjach.
