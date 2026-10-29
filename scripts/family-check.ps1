# [S-6731] The family's render walk: every widget kind at the seven tiers, in light and dark, in its
# default, preview states and styles, judged element by element against the family's tokens
# (Themia.exe --family-check, rules in Core: FamilyCheck). Needs a Debug build of the app.
#
#   powershell -ExecutionPolicy Bypass -File scripts/family-check.ps1 [-Exe <Themia.exe>] [-Kinds a,b] [-Out <dir>]
#
# Fails on any finding (kind, element type, rule). It held a baseline of the day's findings that every
# phase of the family shrank; since group G5 every kind walks clean and the list is gone (shell sweep,
# 2026-10-30). The full findings with their values and the first variant each was seen in are in
# <Out>/<theme>-<kind>.json.
# Never starts Themia.exe without a render flag; draws off-screen, touches no user state.

param(
    [string]$Exe,
    [string]$Kinds,
    [string]$Out,
    [int]$Jobs = 6
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Out) { $Out = Join-Path $root 'TestResults\family-check' }
New-Item -ItemType Directory -Force $Out | Out-Null

if (-not $Exe) {
    $found = @(Get-ChildItem -Path (Join-Path $root 'artifacts'), (Join-Path $root 'src\Themia.App\bin') -Recurse -Filter Themia.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\(debug_win-x64|Debug)\\' -and $_.FullName -notmatch '\\publish\\' } |
        Sort-Object LastWriteTime -Descending)
    if ($found.Count -eq 0) { throw 'No Debug Themia.exe found: build src/Themia.App/Themia.App.csproj -c Debug, or pass -Exe.' }
    $Exe = $found[0].FullName
}
Write-Host "family-check: $Exe"

# Every kind is a module folder: src/Widgets/Themia.Widget.<Name>, its id <name> in camelCase.
if ($Kinds) {
    $all = $Kinds.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
} else {
    $all = Get-ChildItem (Join-Path $root 'src\Widgets') -Directory -Filter 'Themia.Widget.*' |
        ForEach-Object { $n = $_.Name.Substring('Themia.Widget.'.Length); $n.Substring(0, 1).ToLowerInvariant() + $n.Substring(1) } | Sort-Object
}

# One process per kind and theme, a few at a time.
$queue = New-Object System.Collections.Queue
foreach ($kind in $all) { foreach ($theme in 'light', 'dark') { $queue.Enqueue(@($kind, $theme)) } }
$running = @()
$failed = @()
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($queue.Count -gt 0 -and $running.Count -lt $Jobs) {
        $job = $queue.Dequeue()
        $json = Join-Path $Out ("{0}-{1}.json" -f $job[1], $job[0])
        if (Test-Path $json) { Remove-Item $json }
        $p = Start-Process -FilePath $Exe -ArgumentList @('--family-check', "`"$json`"", '--theme', $job[1], '--kinds', $job[0]) -PassThru -WindowStyle Hidden
        $running += [pscustomobject]@{ Process = $p; Kind = $job[0]; Theme = $job[1]; Json = $json; Started = Get-Date }
    }

    Start-Sleep -Milliseconds 250
    $still = @()
    foreach ($r in $running) {
        if ($r.Process.HasExited) {
            if (-not (Test-Path $r.Json)) { $failed += "$($r.Theme) $($r.Kind)" }
        } elseif (((Get-Date) - $r.Started).TotalSeconds -gt 600) {
            $r.Process.Kill()
            $failed += "$($r.Theme) $($r.Kind) (timed out)"
        } else {
            $still += $r
        }
    }
    $running = $still
}

if ($failed.Count -gt 0) {
    Write-Host ("family-check: no result for: " + ($failed -join ', ')) -ForegroundColor Red
    exit 2
}

# The findings of this run: kind <TAB> element and rule (values stripped), with their element counts.
$today = @{}
$elements = 0
foreach ($file in Get-ChildItem $Out -Filter '*.json') {
    $doc = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($kind in $doc.kinds.PSObject.Properties) {
        foreach ($f in $kind.Value) {
            $key = "$($kind.Name)`t$($f.rule)"
            if (-not $today.ContainsKey($key)) { $today[$key] = 0 }
            $today[$key] += [int]$f.count
            $elements += [int]$f.count
        }
    }
}

$kindsWith = ($today.Keys | ForEach-Object { $_.Split("`t")[0] } | Sort-Object -Unique).Count
Write-Host ("family-check: {0} findings (kind, element, rule) in {1} of {2} kinds, {3} elements" -f $today.Count, $kindsWith, $all.Count, $elements)

if ($today.Count -gt 0) {
    Write-Host "family-check: findings off the family's tokens:" -ForegroundColor Red
    $today.Keys | Sort-Object | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host "family-check: no finding" -ForegroundColor Green
exit 0
