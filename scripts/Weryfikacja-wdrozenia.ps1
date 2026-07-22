# Weryfikacja podstawowych elementów wdrożenia. Skrypt niczego nie zmienia.
$ErrorActionPreference = 'Continue'
$base = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $base 'logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$report = Join-Path $logDir ("weryfikacja-{0}-{1:yyyyMMdd-HHmmss}.txt" -f $env:COMPUTERNAME, (Get-Date))

function Add-Result {
    param([string]$Name, [bool]$Ok, [string]$Details)
    $line = "[{0}] {1} - {2}" -f ($(if ($Ok) { 'OK' } else { 'UWAGA' })), $Name, $Details
    $line | Tee-Object -FilePath $report -Append
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
Add-Result 'Uprawnienia administratora' $isAdmin ($(if ($isAdmin) { 'proces jest podniesiony' } else { 'uruchom aplikację jako administrator' }))

$winget = Get-Command winget.exe -ErrorAction SilentlyContinue
if (-not $winget) {
    $pkg = Get-AppxPackage Microsoft.DesktopAppInstaller -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($pkg) {
        $candidate = Join-Path $pkg.InstallLocation 'winget.exe'
        if (Test-Path $candidate) { $winget = Get-Item $candidate }
    }
}
if ($winget) {
    $version = & $winget.Source --version 2>&1 | Out-String
    Add-Result 'winget' $true ($winget.Source + ' ' + $version.Trim())
} else {
    Add-Result 'winget' $false 'nie znaleziono winget.exe; pobieranie będzie wymagało directUrl'
}

$service = Get-Service MozillaMaintenance -ErrorAction SilentlyContinue
$startMode = if ($service) { (Get-CimInstance Win32_Service -Filter "Name='MozillaMaintenance'").StartMode } else { '' }
Add-Result 'Mozilla Maintenance Service' ($null -ne $service) ($(if ($service) { "status=$($service.Status), start=$startMode" } else { 'usługa nie istnieje' }))

$ffPolicy = Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Mozilla\Firefox' -ErrorAction SilentlyContinue
$ffOk = $ffPolicy -and $ffPolicy.DisableAppUpdate -eq 0 -and $ffPolicy.AppAutoUpdate -eq 1 -and $ffPolicy.BackgroundAppUpdate -eq 1
Add-Result 'Polityki aktualizacji Firefox' $ffOk ($(if ($ffPolicy) { "DisableAppUpdate=$($ffPolicy.DisableAppUpdate), AppAutoUpdate=$($ffPolicy.AppAutoUpdate), BackgroundAppUpdate=$($ffPolicy.BackgroundAppUpdate)" } else { 'brak klucza polityk' }))

$fast = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' -Name HiberbootEnabled -ErrorAction SilentlyContinue).HiberbootEnabled
Add-Result 'Szybkie uruchamianie' ($fast -eq 0) ("HiberbootEnabled=$fast")

$power = powercfg /query SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 2>&1 | Out-String
$ac100 = $power -match 'Current AC Power Setting Index:\s+0x00000064'
$dc100 = $power -match 'Current DC Power Setting Index:\s+0x00000064'
Add-Result 'Maksymalny stan procesora' ($ac100 -and $dc100) ("AC100=$ac100, DC100=$dc100")

$publicDesktop = [Environment]::GetFolderPath('CommonDesktopDirectory')
if (-not $publicDesktop) { $publicDesktop = Join-Path $env:PUBLIC 'Desktop' }
$shortcuts = @(Get-ChildItem $publicDesktop -Filter '*.lnk' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
Add-Result 'Pulpit publiczny' ($shortcuts.Count -gt 0) ("$publicDesktop; skróty: " + ($shortcuts -join ', '))

$targets = @{
    'Firefox'    = @("$env:ProgramFiles\Mozilla Firefox\firefox.exe", "${env:ProgramFiles(x86)}\Mozilla Firefox\firefox.exe")
    'Notepad++'  = @("$env:ProgramFiles\Notepad++\notepad++.exe")
    'Inkscape'   = @("$env:ProgramFiles\Inkscape\bin\inkscape.exe", "$env:ProgramFiles\Inkscape\inkscape.exe")
    'GIMP'       = @("$env:ProgramFiles\GIMP 3\bin\gimp-3.0.exe", "$env:ProgramFiles\GIMP 2\bin\gimp-2.10.exe")
    'Krita'      = @("$env:ProgramFiles\Krita (x64)\bin\krita.exe", "$env:ProgramFiles\Krita\bin\krita.exe")
    '7-Zip'      = @("$env:ProgramFiles\7-Zip\7zFM.exe")
}
foreach ($name in $targets.Keys) {
    $found = @($targets[$name] | Where-Object { $_ -and (Test-Path $_) })
    Add-Result $name ($found.Count -gt 0) ($(if ($found) { $found -join ', ' } else { 'nie znaleziono w typowych lokalizacjach' }))
}

"Raport: $report"
