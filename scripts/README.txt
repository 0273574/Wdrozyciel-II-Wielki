SKRYPTY POWERSHELL
==================

1. Wrzucaj pliki *.ps1 do tego folderu.
2. Nazwa widoczna w aplikacji to nazwa pliku bez rozszerzenia.
3. Kliknij „Odśwież”, zaznacz skrypty i wybierz „URUCHOM ZAZNACZONE”.
4. Aplikacja ma manifest requireAdministrator, więc powershell.exe uruchomiony
   przez aplikację dziedziczy uprawnienia administratora.
5. Skrypty są wykonywane kolejno z parametrami:
   -NoLogo -NoProfile -ExecutionPolicy Bypass -File <ścieżka>

Przed uruchomieniem obcego skryptu zawsze przejrzyj jego zawartość.
