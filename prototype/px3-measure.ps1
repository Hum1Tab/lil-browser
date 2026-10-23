param(
    [int]$Tabs = 5,
    [ValidateSet('ours', 'edge')] [string]$Mode = 'ours',
    [string]$Url = 'http://127.0.0.1:8766/',
    [int]$Hold = 25
)

$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $PSScriptRoot
$out = Join-Path $base "prototype\results\px3-$Mode-$Tabs"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $out | Out-Null

function Get-ProcessTable {
    Get-CimInstance Win32_Process |
        Select-Object ProcessId, ParentProcessId, Name, CommandLine
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
        if ($seen.Add($id)) {
            if ($children.ContainsKey($id)) {
                foreach ($c in $children[$id]) { $queue.Enqueue($c) }
            }
        }
    }
    return $seen
}

function Measure-Pids($Pids) {
    $priv = 0L
    $ws = 0L
    $count = 0
    foreach ($id in @($Pids)) {
        $p = Get-Process -Id $id -ErrorAction SilentlyContinue
        if ($p) {
            $priv += $p.PrivateMemorySize64
            $ws += $p.WorkingSet64
            $count++
        }
    }
    [pscustomobject]@{
        PrivateMB       = [math]::Round($priv / 1MB, 1)
        WorkingSetMB    = [math]::Round($ws / 1MB, 1)
        ProcessCount    = $count
    }
}

$env:DOTNET_ROOT = Join-Path $base '.tools\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

$result = $null

if ($Mode -eq 'ours') {
    $dll = Join-Path $base 'prototype\px3-tabs\bin\Release\net8.0-windows\win-x64\Px3WebView2.dll'
    $argList = @("`"$dll`"", "`"$out`"", '--tabs', "$Tabs", '--url', "`"$Url`"", '--hold', "$Hold")
    $proc = Start-Process -FilePath (Join-Path $env:DOTNET_ROOT 'dotnet.exe') `
        -ArgumentList $argList `
        -PassThru

    $deadline = (Get-Date).AddSeconds(150)
    while (-not (Test-Path (Join-Path $out 'ready.json')) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }
    if (-not (Test-Path (Join-Path $out 'ready.json'))) {
        throw "ready.json timeout for $Tabs tabs"
    }

    $ready = Get-Content (Join-Path $out 'ready.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Start-Sleep -Seconds 3

    $table = Get-ProcessTable
    $pids = @(Get-DescendantPids ([int]$ready.pid) $table)

    $extra = $table | Where-Object {
        $_.Name -eq 'msedgewebview2.exe' -and $_.CommandLine -like "*$($ready.udf)*"
    }
    foreach ($e in $extra) {
        $pids += @(Get-DescendantPids ([int]$e.ProcessId) $table)
    }
    $pids = @($pids | Sort-Object -Unique)

    $stats = Measure-Pids $pids
    $result = [pscustomobject]@{
        mode            = 'ours'
        tabs            = $Tabs
        url             = $Url
        startupMs       = [math]::Round([double]$ready.startupMs, 1)
        loadMs          = $ready.loadMs
        switchMs        = $ready.switchMs
        privateMB       = $stats.PrivateMB
        workingSetMB    = $stats.WorkingSetMB
        processCount    = $stats.ProcessCount
        hostPid         = $ready.pid
        udf             = $ready.udf
        measuredAt      = (Get-Date).ToString('O')
    }

    Wait-Process -Id $proc.Id -Timeout 120 -ErrorAction SilentlyContinue
}
else {
    $edgeCandidates = @(
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
    )
    $edge = $edgeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $edge) { throw 'msedge.exe not found' }

    $profile = Join-Path $out 'edge-profile'
    $urls = 1..$Tabs | ForEach-Object { "$Url`?tab=$_" }
    $args = @(
        "--user-data-dir=`"$profile`"",
        '--no-first-run',
        '--no-default-browser-check',
        '--disable-sync',
        '--disable-background-networking',
        '--new-window'
    ) + $urls

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $proc = Start-Process -FilePath $edge -ArgumentList $args -PassThru

    $deadline = (Get-Date).AddSeconds(60)
    $windowMs = $null
    while ((Get-Date) -lt $deadline) {
        $procs = Get-CimInstance Win32_Process |
            Where-Object { $_.Name -eq 'msedge.exe' -and $_.CommandLine -like "*$profile*" }
        foreach ($p in $procs) {
            $gp = Get-Process -Id $p.ProcessId -ErrorAction SilentlyContinue
            if ($gp -and $gp.MainWindowHandle -ne 0) {
                $windowMs = $sw.ElapsedMilliseconds
                break
            }
        }
        if ($windowMs) { break }
        Start-Sleep -Milliseconds 100
    }

    Start-Sleep -Seconds 10

    $table = Get-ProcessTable
    $roots = $table | Where-Object {
        $_.Name -eq 'msedge.exe' -and $_.CommandLine -like "*$profile*" -and $_.CommandLine -notmatch '--type='
    }
    if (-not $roots) { throw 'edge root process not found' }
    $root = $roots | Select-Object -First 1
    $pids = @(Get-DescendantPids ([int]$root.ProcessId) $table)
    $stats = Measure-Pids $pids

    $result = [pscustomobject]@{
        mode            = 'edge'
        tabs            = $Tabs
        url             = $Url
        windowMs        = $windowMs
        privateMB       = $stats.PrivateMB
        workingSetMB    = $stats.WorkingSetMB
        processCount    = $stats.ProcessCount
        rootPid         = $root.ProcessId
        profile         = $profile
        measuredAt      = (Get-Date).ToString('O')
    }

    foreach ($id in (@($pids) | Sort-Object -Descending)) {
        Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
    }
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
}

$json = $result | ConvertTo-Json -Depth 8
$json | Set-Content (Join-Path $out 'measure.json') -Encoding UTF8
Write-Output $json
