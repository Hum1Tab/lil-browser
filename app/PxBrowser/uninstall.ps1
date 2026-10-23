param(
    [switch]$RemoveData
)

$ErrorActionPreference = 'Continue'
$InstallDir = $PSScriptRoot

$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\lIl Browser.lnk'
if (Test-Path $startMenu) { Remove-Item $startMenu -Force -ErrorAction SilentlyContinue }
$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'lIl Browser.lnk'
if (Test-Path $desktop) { Remove-Item $desktop -Force -ErrorAction SilentlyContinue }

$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\lIlBrowser'
if (Test-Path $key) { Remove-Item $key -Recurse -Force -ErrorAction SilentlyContinue }

if ($RemoveData) {
    $data = Join-Path $env:LOCALAPPDATA 'lIlBrowser'
    if (Test-Path $data) { Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host "Removed user data at $data"
}

Get-ChildItem $InstallDir -Force | Where-Object { $_.Name -ne 'uninstall.ps1' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "lIl Browser uninstalled. If the folder remains, delete it manually:"
Write-Host $InstallDir
