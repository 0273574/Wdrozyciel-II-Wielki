[CmdletBinding(DefaultParameterSetName = 'Pfx')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ExePath,

    [Parameter(Mandatory = $true, ParameterSetName = 'Pfx')]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$PfxPath,

    [Parameter(Mandatory = $true, ParameterSetName = 'Pfx')]
    [Security.SecureString]$PfxPassword,

    [Parameter(Mandatory = $true, ParameterSetName = 'Store')]
    [ValidatePattern('^[0-9A-Fa-f ]{40,}$')]
    [string]$Thumbprint,

    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

function Find-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $kitsRoot) {
        $candidate = Get-ChildItem -LiteralPath $kitsRoot -Filter signtool.exe -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
    }

    throw 'Nie znaleziono signtool.exe. Zainstaluj Windows SDK.'
}

$signTool = Find-SignTool
$exe = (Resolve-Path -LiteralPath $ExePath).Path
$args = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')

if ($PSCmdlet.ParameterSetName -eq 'Pfx') {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PfxPassword)
    try {
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        $args += @('/f', (Resolve-Path -LiteralPath $PfxPath).Path, '/p', $plain, $exe)
        & $signTool @args
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
        $plain = $null
    }
}
else {
    $normalized = ($Thumbprint -replace '\s', '').ToUpperInvariant()
    $args += @('/sha1', $normalized, $exe)
    & $signTool @args
}

if ($LASTEXITCODE -ne 0) { throw "signtool zakonczyl sie kodem $LASTEXITCODE." }
& $signTool verify /pa /v $exe
if ($LASTEXITCODE -ne 0) { throw "Weryfikacja podpisu nie powiodla sie (kod $LASTEXITCODE)." }
Write-Host "Podpisano i zweryfikowano: $exe"
