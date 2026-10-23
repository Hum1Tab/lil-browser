param(
    [int]$Tabs = 1,
    [ValidateSet('ours', 'edge')] [string]$Mode = 'ours',
    [int]$Runs = 3,
    [int]$Hold = 10,
    [string]$Url = 'http://127.0.0.1:8766/'
)

$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $PSScriptRoot
$out = Join-Path $base "prototype\results\px3b-$Mode-$Tabs"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $out | Out-Null

$env:DOTNET_ROOT = Join-Path $base '.tools\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

function Get-Marks([string]$Run) {
    try {
        $content = (Invoke-WebRequest "http://127.0.0.1:8766/marks?run=$Run" -UseBasicParsing -TimeoutSec 2).Content
        return @($content | ConvertFrom-Json)
    }
    catch {
        return @()
    }
}

function Get-DescendantPids([int]$Root, $Table) {
    $children = @{}
    foreach ($p in $Table) {
        $pp = [int]$p.ParentProcessId
        if (-not $children.ContainsKey($pp)) {
            $children[$pp] = New-Object System.Collections.Generic.List[int]
        }
        $children[$pp].Add([int]$p.ProcessId)
    }
    $seen = New-Object System.Collections.Generic.HashSet[int]
    $queue = New-Object System.Collections.Generic.Queue[int]
    $queue.Enqueue($Root)
    while ($queue.Count -gt 0) {
        $id = $queue.Dequeue()
        if ($seen.Add($id) -and $children.ContainsKey($id)) {
            foreach ($c in $children[$id]) { $queue.Enqueue($c) }
        }
    }
    return @($seen)
}

$rows = @()

for ($i = 1; $i -le $Runs; $i++) {
    $run = [guid]::NewGuid().ToString('N')
    $launch = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $profile = Join-Path $out "profile-$i"
    $proc = $null

    if ($Mode -eq 'ours') {
        $runDir = Join-Path $out "run-$i"
        New-Item -ItemType Directory -Force -Path $runDir | Out-Null
        $dll = Join-Path $base 'prototype\px3-tabs\bin\Release\net8.0-windows\win-x64\Px3WebView2.dll'
        $argList = @(
            "`"$dll`"", "`"$runDir`"",
            '--tabs', "$Tabs",
            '--url', "`"$Url`"",
            '--startup-only',
            '--run', $run,
            '--hold', "$Hold"
        )
        $proc = Start-Process -FilePath (Join-Path $env:DOTNET_ROOT 'dotnet.exe') -ArgumentList $argList -PassThru
    }
    else {
        $edgeCandidates = @(
            "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
            "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
        )
        $edge = $edgeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $edge) { throw 'msedge.exe not found' }
        $urls = 1..$Tabs | ForEach-Object { "$Url`?run=$run&tab=$_" }
        $argList = @(
            "--user-data-dir=`"$profile`"",
            '--no-first-run',
            '--no-default-browser-check',
            '--disable-sync',
            '--disable-background-networking',
            '--new-window'
        ) + $urls
        $proc = Start-Process -FilePath $edge -ArgumentList $argList -PassThru
    }

    $mark = $null
    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline) {
        $marks = Get-Marks $run
        $mark = $marks | Where-Object { $_.tab -eq '1' } | Select-Object -First 1
        if ($mark) { break }
        Start-Sleep -Milliseconds 100
    }

    $beaconMs = $null
    $startupMs = $null
    if ($mark) {
        $beaconMs = [double]$mark.time * 1000.0
        $startupMs = [math]::Round($beaconMs - $launch, 1)
    }

    $readyPath = Join-Path (Join-Path $out "run-$i") 'ready.json'
    $ready = $null
    if ($Mode -eq 'ours') {
        $readyDeadline = (Get-Date).AddSeconds(8)
        while (-not (Test-Path $readyPath) -and (Get-Date) -lt $readyDeadline) {
            Start-Sleep -Milliseconds 100
        }
        if (Test-Path $readyPath) {
            $ready = Get-Content $readyPath -Raw -Encoding UTF8 | ConvertFrom-Json
        }
    }
    $readyMs = $null
    $loadMs = $null
    if ($ready) {
        $readyMs = [math]::Round([double]$ready.startupMs, 1)
        $loadMs = $ready.loadMs
    }

    $rows += [pscustomobject]@{
        run        = $run
        tabs       = $Tabs
        mode       = $Mode
        startupMs  = $startupMs
        beacon     = $mark
        readyMs    = $readyMs
        loadMs     = $loadMs
        launch     = $launch
    }

    # stop the app / browser tree
    if ($proc) {
        $table = Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId, Name, CommandLine
        $pids = @(Get-DescendantPids ([int]$proc.Id) $table)
        if ($Mode -eq 'edge') {
            $roots = $table | Where-Object {
                $_.Name -eq 'msedge.exe' -and $_.CommandLine -like "*$profile*" -and $_.CommandLine -notmatch '--type='
            }
            foreach ($r in $roots) {
                $pids += @(Get-DescendantPids ([int]$r.ProcessId) $table)
            }
        }
        $pids = @($pids | Sort-Object -Unique -Descending)
        foreach ($id in $pids) {
            Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
        }
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $profile) {
        Remove-Item $profile -Recurse -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Milliseconds 500
}

$result = [pscustomobject]@{
    mode  = $Mode
    tabs  = $Tabs
    runs  = $rows
    medianMs = if ($rows.Count -gt 0) {
        $vals = $rows | Where-Object { $_.startupMs -ne $null } | ForEach-Object { [double]$_.startupMs } | Sort-Object
        if ($vals.Count -gt 0) { [math]::Round($vals[[int]([math]::Floor($vals.Count / 2))], 1) } else { $null }
    } else { $null }
}

$json = $result | ConvertTo-Json -Depth 8
$json | Set-Content (Join-Path $out 'startup.json') -Encoding UTF8
Write-Output $json
