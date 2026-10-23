param(
    [string]$Manifest = (Join-Path $PSScriptRoot '..\update.json'),
    [string]$Signature = (Join-Path $PSScriptRoot '..\update.json.sig'),
    [string]$Key = (Join-Path $env:USERPROFILE '.lil-browser-update-key.json')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Manifest)) { throw "Manifest not found: $Manifest" }
if (-not (Test-Path $Key)) { throw "Private signing key not found: $Key" }

$jsonText = Get-Content $Manifest -Raw -Encoding UTF8
$keyData = Get-Content $Key -Raw -Encoding UTF8 | ConvertFrom-Json
$rsa = [System.Security.Cryptography.RSA]::Create()
$params = New-Object System.Security.Cryptography.RSAParameters
$params.Modulus = [Convert]::FromBase64String($keyData.Modulus)
$params.Exponent = [Convert]::FromBase64String($keyData.Exponent)
$params.D = [Convert]::FromBase64String($keyData.D)
$params.P = [Convert]::FromBase64String($keyData.P)
$params.Q = [Convert]::FromBase64String($keyData.Q)
$params.DP = [Convert]::FromBase64String($keyData.DP)
$params.DQ = [Convert]::FromBase64String($keyData.DQ)
$params.InverseQ = [Convert]::FromBase64String($keyData.InverseQ)
$rsa.ImportParameters($params)
$bytes = [System.Text.Encoding]::UTF8.GetBytes($jsonText)
$sig = $rsa.SignData($bytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
[Convert]::ToBase64String($sig) | Set-Content $Signature -Encoding ASCII
Write-Host "Signed $Manifest -> $Signature"
