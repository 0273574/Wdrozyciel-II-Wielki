$ErrorActionPreference = 'Stop'
Write-Host "Komputer: $env:COMPUTERNAME"
Write-Host "Uzytkownik: $env:USERDOMAIN\\$env:USERNAME"
Write-Host "PowerShell: $($PSVersionTable.PSVersion)"
Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, OSArchitecture | Format-List
