# Noty o oprogramowaniu zewnętrznym

Ten projekt (kod źródłowy) jest udostępniony na licencji MIT (patrz [`LICENSE`](LICENSE)).
Poniższe składniki pochodzą od osób trzecich, mają **własne licencje/warunki** i **nie są
objęte** licencją MIT tego projektu.

## OfficeScrubber

- **Ścieżka:** [`tools/OfficeScrubber/OfficeScrubberAIO.cmd`](tools/OfficeScrubber/)
- **Źródło:** repozytorium `abbodi1406/BatUtil`, katalog `OfficeScrubber`.
- **Rola:** interaktywne czyszczenie instalacji Microsoft Office. Aplikacja uruchamia je
  wyłącznie na wyraźne żądanie użytkownika, po ostrzeżeniu i weryfikacji sumy SHA-256.
- **SHA-256 dołączonego pliku:** `e418f8a6b36d9c55d6efdb4b5ad378ebbb848a6a5e38c44eb94690eae35fff44`
- **Licencja:** według warunków autora oryginalnego narzędzia. Zachowaj oryginalne
  informacje o autorstwie przy dalszej dystrybucji.

## McAfee MCPR

- **Ścieżka:** *niedołączony* — pobierany na żądanie z serwera McAfee.
- **Rola:** usuwanie oprogramowania McAfee. Uruchamiany interaktywnie po decyzji użytkownika;
  jego SHA-256 trafia do logu.
- **Licencja:** własność i warunki McAfee. Narzędzie nie jest redystrybuowane w tym repozytorium.

## Programy instalowane przez aplikację

Wdrożyciel jedynie **pobiera i uruchamia** oficjalne instalatory (Firefox, Chrome, VLC,
LibreOffice, VS Code, GIMP, Krita itd.) z `winget` lub adresów producentów. Każdy z tych
programów podlega **własnej licencji** swojego dostawcy. Ten projekt ich nie redystrybuuje.

## Grafika

- `assets/papiez.jpg` — mem z polskiego internetu, użyty w celach dekoracyjnych/ikonograficznych.
  Jeśli jesteś autorem i chcesz zmiany/usunięcia — otwórz zgłoszenie (Issue).
