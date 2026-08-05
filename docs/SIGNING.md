# Podpisywanie Wdrożyciela

Aplikacja wymaga uprawnień administratora, ponieważ instaluje programy dla całej
maszyny, zmienia HKLM, plany zasilania, pakiety Appx i może dołączać komputer do
domeny. Podpis cyfrowy nie usuwa okna UAC; potwierdza wydawcę i integralność pliku.

## Certyfikat produkcyjny

Potrzebny jest certyfikat Code Signing od zaufanego urzędu certyfikacji albo
firmowy certyfikat, którego łańcuch zaufania jest wdrożony na wszystkich stacjach.
Samopodpisany certyfikat nie będzie zaufany na obcych komputerach bez wcześniejszego
wdrożenia go do magazynu Zaufani wydawcy.

```bat
set SIGN_PFX=C:\certyfikaty\firma-code-signing.pfx
set SIGN_PFX_PASSWORD=haslo
src\build.cmd
```

Można też ustawić `SIGN_CERT_SHA1` na odcisk certyfikatu w magazynie Windows.
Nie zapisuj hasła ani pliku PFX w repozytorium.


## Skrypty pomocnicze

- `src\sign.ps1` podpisuje istniejący EXE plikiem PFX albo certyfikatem z magazynu i od razu weryfikuje podpis.
- `src\create-dev-cert.ps1` tworzy samopodpisany certyfikat wyłącznie do testów lub środowiska, w którym certyfikat publiczny zostanie centralnie dodany do zaufanych wydawców.

Przykład podpisania istniejącego pliku:

```powershell
$haslo = Read-Host 'Hasło PFX' -AsSecureString
.\src\sign.ps1 -ExePath .\Wdrozyciel.exe -PfxPath C:\Certyfikaty\firma.pfx -PfxPassword $haslo
```
