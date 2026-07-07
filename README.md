# Wdrożyciel II Wielki

**Wersja 21.37** · Firmowy instalator offline do masowych wdrożeń komputerów z Windows.

Jeden plik `Wdrozyciel.exe` (WinForms, .NET Framework — działa na każdym Windows 8/10/11
bez instalowania czegokolwiek), który:

- 📦 **pobiera** najnowsze wersje programów do lokalnego repozytorium (winget + fallback
  na bezpośrednie linki producentów),
- 💾 **instaluje** je potem w pełni **offline** — z pendrive'a, dysku zewnętrznego albo
  udziału sieciowego, bez obciążania łącza internetowego przy równoległych wdrożeniach,
- 🏢 **dołącza komputer do domeny AD** (`ad.gliwice.cloud`) z podglądem dostępności
  domeny na żywo i zmianą nazwy komputera w jednym kroku.

## Screenshoty

![Okno główne](docs/screenshot-main.png)

*(screeny w `docs/`)*

## Jak to działa

```
[Internet / strony producentów]
        │  POBIERZ (w biurze) — winget śledzi najnowsze wersje,
        │  fallback na bezpośrednie URL-e (Chrome, Firefox PL, VS Code)
        ▼
[repo\ + manifest.json]  ← wersje, hashe SHA256, zależności, argumenty cichej instalacji
        │  kopiujesz folder na dysk wdrożeniowy
        ▼
[ZAINSTALUJ (u klienta, offline)] — weryfikacja SHA256, zależności (VC++ Redist),
                                    ciche instalacje, log z każdego wdrożenia
```

Sieć firmowa jest obciążana **raz** — przy pobieraniu. Wdrożenie dziesięciu komputerów
naraz nie zużywa internetu w ogóle.

## Programy w zestawie

| Kategoria | Programy |
|---|---|
| Przeglądarki | Mozilla Firefox (PL), Google Chrome (MSI enterprise) |
| Biurowe | Adobe Acrobat Reader, LibreOffice |
| Multimedia | VLC media player |
| Narzędzia | Everything, Visual Studio Code (instalacja systemowa) |

Ptaszek na kategorii zaznacza całą grupę.

## Dodawanie własnych programów — bez rekompilacji

Lista programów mieszka w **`apps.json`** obok exe. Nowy program to nowy wpis:

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

| Pole | Znaczenie |
|---|---|
| `id` | unikalny identyfikator (nazwa podfolderu w `repo\`) |
| `name` | nazwa wyświetlana w GUI |
| `category` | grupa w drzewku (nowe kategorie tworzą się same) |
| `wingetId` | id pakietu — znajdziesz przez `winget search <nazwa>` |
| `exeArgs` / `msiArgs` | argumenty cichej instalacji (`.exe`: zwykle `/S` lub `/VERYSILENT`; `.msi`: `/qn`) |
| `locale` | opcjonalnie, np. `pl-PL` |
| `scope` | opcjonalnie `machine` — wymusza instalator systemowy |
| `directUrl` | opcjonalny bezpośredni link, gdy winget zawiedzie |

Po edycji wystarczy ponownie uruchomić program i kliknąć **POBIERZ**.

## Domena AD

Kafelek "Domena AD" co 5 sekund sprawdza osiągalność kontrolera domeny (LDAP, port 389):
🟢 *Domena dostepna* / 🔴 *Domena niedostepna*. Wpisujesz nazwę komputera, login i hasło
domenowe → **DODAJ DO DOMENY** dołącza maszynę (natywne `NetJoinDomain`) i od razu
zmienia jej nazwę wraz z kontem w AD. Jeden restart na koniec.

## Tryb bez GUI (serwer / harmonogram zadań)

```
Wdrozyciel.exe /download                # aktualizuje całe repo
Wdrozyciel.exe /download firefox,vlc    # tylko wybrane
```

Wpięte w Harmonogram zadań (np. codziennie w nocy) trzyma repo zawsze aktualne.
Log: `logs\download-*.log`.

## Budowanie

Bez Visual Studio, bez SDK — kompiluje wbudowany w Windows kompilator .NET Framework:

```
src\build.cmd
```

Wynik: `Wdrozyciel.exe` (~40 KB) w katalogu głównym. Program wymaga uprawnień
administratora (sam poprosi o UAC).

## Struktura katalogu roboczego

```
Wdrozyciel.exe      # aplikacja
apps.json           # edytowalna lista programów
manifest.json       # generowany: wersje, hashe, zależności (nie commitować)
repo\               # generowane: instalatory (nie commitować, ~1.6 GB)
logs\               # generowane: logi wdrożeń i pobrań
src\                # źródła (Program.cs + build.cmd + app.manifest)
```

---

*Dlaczego 21.37? Bo wersja 1.0 brzmiałaby zbyt skromnie jak na Wielkiego.* 🍰
