@echo off
rem Buduje Wdrozyciel.exe kompilatorem wbudowanym w .NET Framework
rem (obecny na kazdym Windows 8/10/11 - nie trzeba nic instalowac).
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:winexe /out:"%~dp0..\Wdrozyciel.exe" ^
  /win32manifest:"%~dp0app.manifest" ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Management.dll ^
  "%~dp0Program.cs"

if %errorlevel%==0 (echo OK: zbudowano ..\Wdrozyciel.exe) else (echo BLAD kompilacji & exit /b 1)
