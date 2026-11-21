# [S-6731] The family's render walk: every widget kind at its sizes, in light and dark, in its
# default, preview states and styles, judged element by element against the family's tokens
# (Themia.exe --check family, rules in Core: FamilyCheck). Needs a Debug build of the app.
#
#   powershell -ExecutionPolicy Bypass -File scripts/family-check.ps1 [-Exe <Themia.exe>] [-Kinds a,b] [-Out <dir>] [-Jobs 2]
#
# [S-14342] The kinds are the build's own (Themia.exe --render catalog: a family's every kind); a kind it does not have
# fails the walk. A few kinds walk in one process (-Chunk, default 12), at most -Jobs processes at once (default 2: the
# PC hard-hung under ~16 renders at once).
#
# Fails on any finding (kind, element type, rule). It held a baseline of the day's findings that every
# phase of the family shrank; since group G5 every kind walks clean and the list is gone (shell sweep,
# 2026-10-30). The full findings with their values and the first variant each was seen in are in
# <Out>/<theme>-<kind>.json, with how many elements each kind drew ("walked"); a kind that drew
# nothing is the finding "nothing drawn". Every kind is walked at its range (min, natural, max),
# each box with the config that size means (its SizeSets hook).
# Never starts Themia.exe without a render flag; draws off-screen, touches no user state.

param(
    [string]$Exe,
    [string]$Kinds,
    [string]$Out,
    [int]$Jobs = 2,
    [int]$Chunk = 12
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Out) { $Out = Join-Path $root 'TestResults\family-check' }
New-Item -ItemType Directory -Force $Out | Out-Null

if (-not $Exe) {
    $found = @(Get-ChildItem -Path (Join-Path $root 'artifacts'), (Join-Path $root 'src\Themia.App\bin') -Recurse -Filter Themia.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\(debug_win-x64|Debug)\\' -and $_.FullName -notmatch '\\publish\\' })
    # Never a guess among several builds (other branches' land under artifacts/ too): exactly one, or -Exe.
    if ($found.Count -ne 1) { throw "$($found.Count) Debug Themia.exe found: build src/Themia.App/Themia.App.csproj -c Debug and pass -Exe." }
    $Exe = $found[0].FullName
}
Write-Host "family-check: $Exe"

# Every kind the build has, from its own catalog (never a folder name: a family project declares several).
$catalog = Join-Path $Out '_catalog\catalog.json'
New-Item -ItemType Directory -Force (Split-Path $catalog) | Out-Null
$p = Start-Process -FilePath $Exe -ArgumentList @('--render', 'catalog', "`"$catalog`"") -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit(300000)) { $p.Kill(); throw 'family-check: --render catalog timed out' }
if (-not (Test-Path $catalog)) { throw "family-check: $Exe wrote no catalog (a build from before --render catalog?)" }
$known = @((Get-Content $catalog -Raw -Encoding UTF8 | ConvertFrom-Json).kinds | ForEach-Object { $_.id })
if ($Kinds) {
    $all = @($Kinds.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $unknown = @($all | Where-Object { $known -notcontains $_ })
    if ($unknown.Count -gt 0) { throw ("family-check: no such kind in this build: " + ($unknown -join ', ')) }
} else {
    $all = $known
}

# This run's results only: an earlier run's files would add their findings to this one's.
Get-ChildItem $Out -Filter '*.json' | Remove-Item

# A few kinds to a process and theme, at most -Jobs processes at once.
$queue = New-Object System.Collections.Queue
for ($i = 0; $i -lt $all.Count; $i += $Chunk) {
    $group = ($all[$i..([Math]::Min($i + $Chunk, $all.Count) - 1)]) -join ','
    foreach ($theme in 'light', 'dark') { $queue.Enqueue(@($group, $theme, $i)) }
}
$running = @()
$failed = @()
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($queue.Count -gt 0 -and $running.Count -lt $Jobs) {
        $job = $queue.Dequeue()
        $json = Join-Path $Out ("{0}-{1:000}.json" -f $job[1], $job[2])
        if (Test-Path $json) { Remove-Item $json }
        $p = Start-Process -FilePath $Exe -ArgumentList @('--check', 'family', "`"$json`"", '--theme', $job[1], '--kinds', $job[0]) -PassThru -WindowStyle Hidden
        $running += [pscustomobject]@{ Process = $p; Kind = $job[0]; Theme = $job[1]; Json = $json; Started = Get-Date }
    }

    Start-Sleep -Milliseconds 250
    $still = @()
    foreach ($r in $running) {
        if ($r.Process.HasExited) {
            if (-not (Test-Path $r.Json)) { $failed += "$($r.Theme) $($r.Kind)" }
        } elseif (((Get-Date) - $r.Started).TotalSeconds -gt 120 * $Chunk) {
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

# The findings of this run: kind <TAB> element and rule (values stripped), with how many elements
# offend; and how many elements the kinds drew and had judged.
$today = @{}
$offending = 0
$walked = 0
foreach ($file in Get-ChildItem $Out -Filter '*.json') {
    $doc = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($kind in $doc.kinds.PSObject.Properties) {
        foreach ($f in $kind.Value) {
            $key = "$($kind.Name)`t$($f.rule)"
            if (-not $today.ContainsKey($key)) { $today[$key] = 0 }
            $today[$key] += [int]$f.count
            $offending += [int]$f.count
        }
    }
    foreach ($kind in $doc.walked.PSObject.Properties) { $walked += [int]$kind.Value }
}

$kindsWith = ($today.Keys | ForEach-Object { $_.Split("`t")[0] } | Sort-Object -Unique).Count
Write-Host ("family-check: {0} findings (kind, element, rule) in {1} of {2} kinds, {3} offending of {4} elements judged" -f $today.Count, $kindsWith, $all.Count, $offending, $walked)

if ($today.Count -gt 0) {
    Write-Host "family-check: findings off the family's tokens:" -ForegroundColor Red
    $today.Keys | Sort-Object | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host "family-check: no finding" -ForegroundColor Green
exit 0
