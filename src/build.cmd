@echo off
setlocal EnableExtensions
rem Buduje Wdrozyciel.exe kompilatorem .NET Framework dostepnym w Windows.
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo BLAD: nie znaleziono kompilatora C# .NET Framework.
  exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:"%~dp0..\Wdrozyciel.exe" ^
  /win32manifest:"%~dp0app.manifest" /win32icon:"%~dp0wdrozyciel.ico" ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Management.dll ^
  "%~dp0Program.cs"

if errorlevel 1 (
  echo BLAD kompilacji.
  exit /b 1
)

echo OK: zbudowano ..\Wdrozyciel.exe

rem Opcjonalne podpisanie Authenticode. Certyfikat z zaufanego CA jest wymagany,
rem aby SmartScreen nie ostrzegal na nowych komputerach. Samopodpisany certyfikat
rem dziala dopiero po dodaniu go do zaufanych wydawcow na kazdym komputerze.
rem Przyklad PFX:
rem   set SIGN_PFX=C:\certyfikaty\firma-code-signing.pfx
rem   set SIGN_PFX_PASSWORD=haslo
rem   src\build.cmd
rem Alternatywnie ustaw SIGN_CERT_SHA1 na odcisk certyfikatu w magazynie certyfikatow.

if not defined SIGN_PFX if not defined SIGN_CERT_SHA1 goto :unsigned
if not defined SIGNTOOL (
  for /f "delims=" %%I in ('dir /b /s "%ProgramFiles(x86)%\Windows Kits\10\bin\*\x64\signtool.exe" 2^>nul') do set "SIGNTOOL=%%I"
)
if not defined SIGNTOOL set "SIGNTOOL=signtool.exe"
if not exist "%SIGNTOOL%" (
  where "%SIGNTOOL%" >nul 2>nul
  if errorlevel 1 (
    echo UWAGA: zbudowano EXE, ale nie znaleziono signtool.exe - plik pozostaje niepodpisany.
    exit /b 0
  )
)

if defined SIGN_PFX (
  "%SIGNTOOL%" sign /fd SHA256 /f "%SIGN_PFX%" /p "%SIGN_PFX_PASSWORD%" /tr http://timestamp.digicert.com /td SHA256 "%~dp0..\Wdrozyciel.exe"
) else (
  "%SIGNTOOL%" sign /fd SHA256 /sha1 "%SIGN_CERT_SHA1%" /tr http://timestamp.digicert.com /td SHA256 "%~dp0..\Wdrozyciel.exe"
)
if errorlevel 1 (
  echo BLAD podpisywania. EXE zostal zbudowany, ale podpis nie zostal dodany.
  exit /b 1
)
"%SIGNTOOL%" verify /pa /v "%~dp0..\Wdrozyciel.exe"
if errorlevel 1 (
  echo BLAD: podpis zostal dodany, ale weryfikacja Authenticode nie powiodla sie.
  exit /b 1
)
echo OK: podpisano i zweryfikowano ..\Wdrozyciel.exe
exit /b 0

:unsigned
echo INFO: brak SIGN_PFX/SIGN_CERT_SHA1 - EXE pozostaje niepodpisany.
exit /b 0
