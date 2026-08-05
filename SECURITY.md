# Polityka bezpieczeństwa

## Charakter narzędzia

Wdrożyciel II Wielki celowo działa z uprawnieniami administratora i wykonuje operacje
o dużym zasięgu (instalacje maszynowe, zmiany w `HKLM`, planach zasilania, pakietach Appx,
usuwanie Office, dołączanie do domeny). Używaj go świadomie i najlepiej najpierw na
maszynie testowej.

## Zgłaszanie podatności

Jeśli znajdziesz lukę bezpieczeństwa, **nie otwieraj publicznego Issue**. Zamiast tego
skorzystaj z prywatnego kanału GitHuba **Security → Report a vulnerability** (Private
Vulnerability Reporting) w tym repozytorium.

W zgłoszeniu opisz:
- czego dotyczy problem i jaki jest potencjalny wpływ,
- kroki do odtworzenia,
- wersję aplikacji i system.

Postaram się odpowiedzieć w rozsądnym czasie i uzgodnić termin ujawnienia po naprawie.

## Dobre praktyki dla użytkowników

- Weryfikuj sumy SHA-256 pobranych instalatorów (opcja włączona domyślnie).
- Przeglądaj każdy skrypt `.ps1` w `scripts/` przed uruchomieniem.
- Nie trzymaj certyfikatów podpisujących (`*.pfx`) ani haseł w repozytorium.
