param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\..\dist\lIlBrowser\lIlBrowser.exe')
)

$ErrorActionPreference = 'Stop'
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq 'CN=lIl Browser Local Test' } |
    Select-Object -First 1

if (-not $cert) {
    throw 'Local signing certificate not found. Create/trust "CN=lIl Browser Local Test" first.'
}

$Exe = [System.IO.Path]::GetFullPath($Exe)
if (-not (Test-Path $Exe)) { throw "EXE not found: $Exe" }

$result = Set-AuthenticodeSignature -FilePath $Exe -Certificate $cert -HashAlgorithm SHA256
$result | Select-Object Status, StatusMessage, Path
