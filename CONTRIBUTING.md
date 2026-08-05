# Współtworzenie — Wdrożyciel II Wielki

Dzięki, że chcesz pomóc! Kilka prostych zasad, żeby projekt trzymał się kupy.

## Zgłaszanie błędów i pomysłów

- Otwórz **Issue** i opisz: system (Windows 10/11), tryb (online/offline), obecność
  `winget`, kroki do odtworzenia i oczekiwany rezultat.
- Do błędów instalacji dołącz istotny fragment logu z folderu `.wdrozyciel` lub `logs/`
  (usuń dane wrażliwe: nazwy hostów, loginy domenowe).

## Pull requesty

1. Zrób forka i gałąź opisową (`feature/...`, `fix/...`).
2. **Cały kod aplikacji mieści się w [`src/Program.cs`](src/Program.cs)** — trzymaj się tego,
   dopóki nie ma zgody na podział na wiele plików/projekt `.csproj`.
3. Zbuduj lokalnie przez [`BUILD.cmd`](BUILD.cmd) i sprawdź, że kompiluje się bez ostrzeżeń.
4. Nowy program? Dodaj wpis w [`apps.json`](apps.json) (patrz format istniejących pozycji)
   zamiast wpisywać go na sztywno w kodzie, jeśli to możliwe.
5. Opisz w PR, co i po co — oraz na czym testowałeś (Windows 10/11, online/offline).

## Styl i konwencje

- Komentarze i teksty UI po polsku (spójnie z resztą).
- Bez zależności zewnętrznych NuGet — projekt celowo kompiluje się samym `csc.exe`
  z .NET Framework, żeby dało się go zbudować na czystym Windowsie.
- Nie commituj plików budowanych (`Wdrozyciel.exe`, `*.pdb`), certyfikatów (`*.pfx`)
  ani danych runtime (`.wdrozyciel/`, `logs/`) — pilnuje tego `.gitignore`.

## Zmiany dotyczące narzędzi zewnętrznych

Aktualizując cokolwiek w [`tools/`](tools/), zaktualizuj też sumę SHA-256 i wpis w
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## Bezpieczeństwo

Podatności zgłaszaj zgodnie z [`SECURITY.md`](SECURITY.md), a nie w publicznym Issue.
