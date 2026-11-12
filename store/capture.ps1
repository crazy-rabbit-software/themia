# [S-1078] The Microsoft Store screenshots: every scene of store/scenes.json drawn by the Debug app's
# --scene-png ([S-1074]), one process per scene, a few at a time, to store/out/shots/<lang>/<scene>.png.
#
#   powershell -ExecutionPolicy Bypass -File store/capture.ps1 [-Lang en] [-Only a,b] [-NoBuild] [-Jobs 4]
#
# Builds the Debug app into artifacts/store first (its own folder, so it never fights another build's
# locks) unless -NoBuild. Fails, naming them, when a scene's PNG did not appear or its process did not
# exit 0; each scene's console output is beside its PNG as <scene>.log.
# Never starts Themia.exe without a render flag; draws off-screen, touches no user state.

param(
    [string]$Lang = 'en',
    [string]$Only,
    [switch]$NoBuild,
    [int]$Jobs = 4
)

$ErrorActionPreference = 'Stop'
$store = $PSScriptRoot
$root = Split-Path -Parent $store
$scenes = Join-Path $store 'scenes.json'
$artifacts = Join-Path $root 'artifacts\store'

if (-not $NoBuild) {
    & dotnet build (Join-Path $root 'src\Themia.App\Themia.App.csproj') -c Debug --artifacts-path $artifacts
    if ($LASTEXITCODE -ne 0) { throw "capture: the Debug build failed (exit $LASTEXITCODE)" }
}

$exe = @(Get-ChildItem -Path (Join-Path $artifacts 'bin') -Recurse -Filter Themia.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\debug_win-x64\\' } | Sort-Object LastWriteTime -Descending)
if ($exe.Count -eq 0) { throw "capture: no Debug Themia.exe under $artifacts; run without -NoBuild" }
$exe = $exe[0].FullName
Write-Host "capture: $exe"

# The scene names, in the file's order; the app checks the rest of the file itself.
$names = @((Get-Content $scenes -Raw -Encoding UTF8 | ConvertFrom-Json).scenes | ForEach-Object { $_.name })
if ($Only) {
    $wanted = @($Only.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $unknown = @($wanted | Where-Object { $names -notcontains $_ })
    if ($unknown.Count -gt 0) { throw ("capture: no scene named " + ($unknown -join ', ') + " in $scenes (has: " + ($names -join ', ') + ")") }
    $names = $wanted
}

$out = Join-Path $store "out\shots\$Lang"
New-Item -ItemType Directory -Force $out | Out-Null

# One process per scene, a few at a time.
$queue = New-Object System.Collections.Queue
foreach ($name in $names) { $queue.Enqueue($name) }
$running = @()
$failed = @()
while ($queue.Count -gt 0 -or $running.Count -gt 0) {
    while ($queue.Count -gt 0 -and $running.Count -lt $Jobs) {
        $name = $queue.Dequeue()
        $png = Join-Path $out "$name.png"
        $log = Join-Path $out "$name.log"
        if (Test-Path $png) { Remove-Item $png }
        $p = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden -RedirectStandardOutput $log `
            -ArgumentList @('--scene-png', "`"$scenes`"", $name, "`"$png`"", '--lang', $Lang)
        $null = $p.Handle # keeps the exit code readable once it has exited
        $running += [pscustomobject]@{ Process = $p; Name = $name; Png = $png; Log = $log; Started = Get-Date }
    }

    Start-Sleep -Milliseconds 250
    $still = @()
    foreach ($r in $running) {
        if ($r.Process.HasExited) {
            $r.Process.WaitForExit()
            $code = $r.Process.ExitCode
            if ($code -ne 0 -or -not (Test-Path $r.Png)) {
                $why = if (Test-Path $r.Log) { (Get-Content $r.Log | Where-Object { $_ -match '^scene: ' -and $_ -notmatch 'wrote' } | Select-Object -Last 1) } else { '' }
                $failed += "$($r.Name) (exit $code) $why"
            } else {
                Write-Host "capture: $($r.Png)"
            }
        } elseif (((Get-Date) - $r.Started).TotalSeconds -gt 300) {
            $r.Process.Kill()
            $failed += "$($r.Name) (timed out)"
        } else {
            $still += $r
        }
    }
    $running = $still
}

if ($failed.Count -gt 0) {
    Write-Host "capture: no picture for:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host ("capture: {0} scene(s) in {1}" -f $names.Count, $out) -ForegroundColor Green
exit 0
