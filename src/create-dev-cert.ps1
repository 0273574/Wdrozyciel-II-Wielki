[CmdletBinding()]
param(
    [string]$Subject = 'CN=Wdrozyciel II Wielki DEV',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dev-cert'),
    [Security.SecureString]$Password = (Read-Host 'Haslo do pliku PFX' -AsSecureString)
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -HashAlgorithm SHA256 `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears(2)

$pfx = Join-Path $OutputDirectory 'Wdrozyciel-DEV.pfx'
$cer = Join-Path $OutputDirectory 'Wdrozyciel-DEV.cer'
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $Password | Out-Null
Export-Certificate -Cert $cert -FilePath $cer | Out-Null

Write-Host "Utworzono certyfikat deweloperski: $($cert.Thumbprint)"
Write-Host "PFX: $pfx"
Write-Host "CER: $cer"
Write-Warning 'Certyfikat samopodpisany nie usuwa ostrzezen SmartScreen na obcych komputerach. Wdroż CER do zaufanych wydawcow albo uzyj certyfikatu Code Signing od zaufanego CA.'
