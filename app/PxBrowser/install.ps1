param(
    [string]$Source = $PSScriptRoot,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\lIlBrowser'),
    [switch]$NoShortcut,
    [switch]$NoRegistry,
    [switch]$DesktopShortcut
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path (Join-Path $Source 'lIlBrowser.exe'))) {
    throw "lIlBrowser.exe not found in $Source"
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item (Join-Path $Source '*') $InstallDir -Recurse -Force

if (-not $NoShortcut) {
    $ws = New-Object -ComObject WScript.Shell
    $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    New-Item -ItemType Directory -Force -Path $startMenu | Out-Null
    $lnk = $ws.CreateShortcut((Join-Path $startMenu 'lIl Browser.lnk'))
    $lnk.TargetPath = Join-Path $InstallDir 'lIlBrowser.exe'
    $lnk.WorkingDirectory = $InstallDir
    $lnk.Description = 'lIl Browser'
    $lnk.Save()
    if ($DesktopShortcut) {
        $desktop = [Environment]::GetFolderPath('Desktop')
        $lnk2 = $ws.CreateShortcut((Join-Path $desktop 'lIl Browser.lnk'))
        $lnk2.TargetPath = Join-Path $InstallDir 'lIlBrowser.exe'
        $lnk2.WorkingDirectory = $InstallDir
        $lnk2.Save()
    }
}

if (-not $NoRegistry) {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\lIlBrowser'
    New-Item -Path $key -Force | Out-Null
    Set-ItemProperty -Path $key -Name DisplayName -Value 'lIl Browser'
    Set-ItemProperty -Path $key -Name DisplayVersion -Value '0.3.0'
    Set-ItemProperty -Path $key -Name Publisher -Value 'PX'
    Set-ItemProperty -Path $key -Name InstallLocation -Value $InstallDir
    Set-ItemProperty -Path $key -Name DisplayIcon -Value (Join-Path $InstallDir 'lIlBrowser.exe')
    Set-ItemProperty -Path $key -Name UninstallString -Value ('powershell -ExecutionPolicy Bypass -File "' + (Join-Path $InstallDir 'uninstall.ps1') + '"')
    Set-ItemProperty -Path $key -Name NoModify -Value 1 -Type DWord
    Set-ItemProperty -Path $key -Name NoRepair -Value 1 -Type DWord
}

Write-Host "Installed lIl Browser to $InstallDir"
if (-not $NoShortcut) { Write-Host "Start Menu shortcut created." }
if (-not $NoRegistry) { Write-Host "Uninstall entry created (HKCU)." }
Write-Host "This installer does not register lIl Browser as the default browser."
