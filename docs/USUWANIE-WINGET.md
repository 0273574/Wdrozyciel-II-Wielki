# Usuwanie programów po Winget ID

Zakładka **Skrypty i narzędzia → System i usuwanie → Usuwanie winget** pozwala odinstalować
programy po dokładnym identyfikatorze pakietu winget (np. `Microsoft.Teams`).

## Jak to działa

Dla każdego ID z listy Wdrożyciel uruchamia:

```
winget uninstall --id <ID> --exact --accept-source-agreements --disable-interactivity --silent --scope machine
```

Jeśli ta próba zawiedzie (pakiet zainstalowany tylko dla użytkownika, a nie dla całego komputera),
następuje automatyczna druga próba **bez** `--scope machine`, czyli w zakresie użytkownika:

```
winget uninstall --id <ID> --exact --accept-source-agreements --disable-interactivity --silent
```

Wynik każdej operacji trafia do logu na dole okna oraz do zakładki **Wynik**.

- `--exact` — dopasowanie po dokładnym ID (bez zgadywania po nazwie).
- `--silent` — cicha deinstalacja bez okien kreatora.
- Kolejność **machine → user** jest istotna: program zainstalowany dla wszystkich użytkowników
  nie zostanie znaleziony w zakresie użytkownika i odwrotnie.

## Jak pozyskać dokładne ID

1. Kliknij **Lista aplikacji (winget list)** — wynik pojawi się w zakładce **Wynik** i zostanie
   zapisany do `.wdrozyciel\logs\winget-list-*.txt`.
2. Skopiuj wartość z kolumny **Id** (nie z kolumny Nazwa).
3. Wklej ID (po jednym w wierszu) w pole „Dokładne ID winget" albo trzymaj je w pliku
   `winget-remove-defaults.txt` obok programu i użyj **Wczytaj winget-remove-defaults.txt**.

Plik `winget-remove-defaults.txt` — jedno ID w wierszu, `#` rozpoczyna komentarz:

```
# Programy do usunięcia po ID winget
Microsoft.Teams
Disney.DisneyPlus
```

## Kody wyjścia i ich znaczenie

Wdrożyciel tłumaczy najczęstsze kody na czytelne komunikaty:

| Kod | Znaczenie |
|-----|-----------|
| `0` | OK — odinstalowano |
| `1638` | OK — pakiet nie był zainstalowany |
| `3010` | OK — odinstalowano, wymagany restart |
| `0x8A15002B` | Pominięto — brak zainstalowanego pakietu o tym ID (nic do usunięcia) |
| `0x8A150014` | Błąd — nie znaleziono źródła pakietu (patrz niżej) |
| `0x8A150011` | Błąd — odinstalowanie nie powiodło się |

`0x8A15002B` **nie jest błędem** — oznacza, że danego programu i tak nie ma w systemie.

## Uwagi i ograniczenia

- **Programy Microsoft Store (MSIX/UWP)** — część da się usunąć przez winget, ale pakiety
  systemowe i „provisioned" dla nowych użytkowników lepiej usuwać przez zakładkę
  **Usuwanie Appx** (`Remove-AppxPackage` / `Remove-AppxProvisionedPackage`).
- **Uprawnienia** — Wdrożyciel działa jako administrator, więc deinstalacja maszynowa ma
  wymagane uprawnienia. Deinstalacja w zakresie użytkownika dotyczy profilu, w którym
  uruchomiono program.
- **Brak potwierdzeń** — deinstalacja jest cicha (`--silent`). Lista ID jest pokazywana do
  potwierdzenia raz, przed startem — sprawdź ją dokładnie.
- **Źródło winget** (`0x8A150014` / `0x8A15000F`) — jeśli winget zgłasza brak źródła, napraw je:
  ```
  winget source reset --force
  winget source update
  ```
  Wdrożyciel robi to automatycznie przy pobieraniu, ale przy samym usuwaniu warto to sprawdzić.
