# [S-6731] The family's render walk: every widget kind at the seven tiers, in light and dark, in its
# default, preview states and styles, judged element by element against the family's tokens
# (Themia.exe --family-check, rules in Core: FamilyCheck). Needs a Debug build of the app.
#
#   powershell -ExecutionPolicy Bypass -File scripts/family-check.ps1 [-Exe <Themia.exe>] [-Kinds a,b] [-Out <dir>] [-Write]
#
# Fails on any finding (kind, element type, rule) not in scripts/family-check.allow.txt, and on any line
# of that list that no longer occurs: the list only shrinks, and the family is done when it is empty.
# -Write rewrites the list from this run (to shrink it after fixing offenders, never to grow it).
# The full findings with their values and the first variant each was seen in are in <Out>/<theme>-<kind>.json.
# Never starts Themia.exe without a render flag; draws off-screen, touches no user state.

param(
    [string]$Exe,
    [string]$Kinds,
    [string]$Out,
    [int]$Jobs = 6,
    [switch]$Write
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$allowPath = Join-Path $PSScriptRoot 'family-check.allow.txt'
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

if ($Write) {
    $lines = @(
        '# [S-6731] Today''s render-walk findings: kind <TAB> element and rule (values stripped). Only shrinks:',
        '# scripts/family-check.ps1 fails on a finding not listed here and on a line no longer found. Empty is done.'
    ) + ($today.Keys | Sort-Object)
    [System.IO.File]::WriteAllLines($allowPath, $lines)
    Write-Host "family-check: wrote $allowPath ($($today.Count) lines)"
    exit 0
}

$allowed = @{}
if (Test-Path $allowPath) {
    foreach ($line in [System.IO.File]::ReadAllLines($allowPath)) {
        if ($line -and -not $line.StartsWith('#')) { $allowed[$line] = $true }
    }
}

# Only the kinds checked in this run are held to their lines of the list.
$checked = @{}
foreach ($k in $all) { $checked[$k] = $true }
$fresh = @($today.Keys | Where-Object { -not $allowed.ContainsKey($_) } | Sort-Object)
$stale = @($allowed.Keys | Where-Object { $checked.ContainsKey($_.Split("`t")[0]) -and -not $today.ContainsKey($_) } | Sort-Object)

if ($fresh.Count -gt 0) {
    Write-Host "family-check: new findings (off the family's tokens):" -ForegroundColor Red
    $fresh | ForEach-Object { Write-Host "  $_" }
}
if ($stale.Count -gt 0) {
    Write-Host "family-check: fixed, take off scripts/family-check.allow.txt:" -ForegroundColor Yellow
    $stale | ForEach-Object { Write-Host "  $_" }
}
if ($fresh.Count -gt 0 -or $stale.Count -gt 0) { exit 1 }
Write-Host "family-check: matches its baseline ($($allowed.Count) lines)" -ForegroundColor Green
exit 0
