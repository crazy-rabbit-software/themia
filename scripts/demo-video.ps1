<#
.SYNOPSIS
  Records the primary screen for the Google verification demo (docs/GOOGLE-VERIFICATION.md).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/demo-video.ps1 start
  powershell -ExecutionPolicy Bypass -File scripts/demo-video.ps1 stop
#>
param([Parameter(Mandatory)][ValidateSet('start', 'stop')][string]$Action)

$ErrorActionPreference = 'Stop'
$dir = Join-Path ([Environment]::GetFolderPath('MyVideos')) 'ThemiaDemo'
$pidFile = Join-Path $dir 'recording.pid'
New-Item -ItemType Directory -Force $dir | Out-Null

if ($Action -eq 'start') {
    if (Test-Path $pidFile) { throw "A recording is already running (pid $(Get-Content $pidFile)); stop it first." }
    if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { throw 'ffmpeg is not on PATH (winget install Gyan.FFmpeg).' }

    Add-Type -AssemblyName System.Windows.Forms
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $out = Join-Path $dir ("raw-{0:yyyyMMdd-HHmmss}.mkv" -f (Get-Date))

    # Stop ends ffmpeg without a goodbye, so nothing may wait in a buffer: no lookahead (zerolatency),
    # every packet written at once, into MKV, which plays without a closing index. CRF 16 keeps small
    # text (the client ID) readable.
    $ffArgs = @('-hide_banner', '-loglevel', 'error', '-f', 'gdigrab', '-framerate', '30', '-draw_mouse', '1',
        '-offset_x', $screen.X, '-offset_y', $screen.Y, '-video_size', "$($screen.Width)x$($screen.Height)",
        '-i', 'desktop', '-c:v', 'libx264', '-preset', 'veryfast', '-tune', 'zerolatency', '-crf', '16', '-flush_packets', '1', '-pix_fmt', 'yuv420p', "`"$out`"")
    # Windows PowerShell's Start-Process joins arguments with spaces and quotes none, hence the quotes.
    $process = Start-Process ffmpeg -ArgumentList $ffArgs -WindowStyle Hidden -PassThru
    Set-Content $pidFile $process.Id
    "Recording $($screen.Width)x$($screen.Height) to $out (pid $($process.Id))."
}
else {
    if (-not (Test-Path $pidFile)) { throw 'No recording is running.' }
    $id = [int](Get-Content $pidFile)
    Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
    Remove-Item $pidFile
    $latest = Get-ChildItem $dir -Filter 'raw-*.mkv' | Sort-Object LastWriteTime | Select-Object -Last 1
    "Stopped. $($latest.FullName) ($([math]::Round($latest.Length / 1MB, 1)) MB)."
}
