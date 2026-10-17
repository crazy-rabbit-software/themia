<#
.SYNOPSIS
    Publish Themia as a single native ahead-of-time binary.

.DESCRIPTION
    Wraps `dotnet publish` with the one thing the ILCompiler needs and cannot find on its own:
    vswhere.exe on PATH. Without it the native link step fails with

        'vswhere.exe' is not recognized as an internal or external command
        ... link.exe ... exited with code 123

    because the ILCompiler shells out to vswhere to locate the MSVC linker.
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [switch] $Run
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Themia.App\Themia.App.csproj'

$vswhereDir = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (-not (Test-Path (Join-Path $vswhereDir 'vswhere.exe'))) {
    throw "vswhere.exe not found under '$vswhereDir'. Visual Studio Build Tools with the C++ workload are required for Native AOT."
}

if ($env:PATH -notlike "*$vswhereDir*") {
    $env:PATH = "$vswhereDir;$env:PATH"
}

$publishDir = Join-Path $repoRoot "src\Themia.App\bin\$Configuration\net10.0-windows10.0.19041.0\win-x64\publish"

# Before PublishCompiledXaml excluded the publish folder from its glob, every publish copied the
# previous one's .xbf files into publish\publish\ (one level deeper each time). Clear what an older
# build left there; after the publish the folder must not come back.
$nested = Join-Path $publishDir 'publish'
if (Test-Path $nested) {
    Remove-Item -Recurse -Force $nested
}

Write-Host 'Publishing Themia (Native AOT)...' -ForegroundColor Cyan
dotnet publish $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $publishDir 'Themia.exe'
if (Test-Path $nested) {
    throw "The publish folder holds a nested publish\ again - the PublishCompiledXaml glob picked up its own output."
}

if (-not (Test-Path $exe)) {
    throw "Publish reported success but '$exe' is missing."
}

# The app dies at the first InitializeComponent without these, and only in the published build,
# so it is worth failing the script here rather than at runtime.
$xbf = @(Get-ChildItem $publishDir -Recurse -Filter '*.xbf' -ErrorAction SilentlyContinue)
if ($xbf.Count -eq 0) {
    throw 'No compiled XAML (.xbf) in the publish output - the PublishCompiledXaml target did not run.'
}

$size = (Get-Item $exe).Length / 1MB
$managed = @(Get-ChildItem $publishDir -Filter 'System.*.dll' -ErrorAction SilentlyContinue)

Write-Host ''
Write-Host ('  {0,-22} {1:N1} MB' -f 'native executable', $size) -ForegroundColor Green
Write-Host ('  {0,-22} {1}' -f 'compiled XAML files', $xbf.Count) -ForegroundColor Green
Write-Host ('  {0,-22} {1}' -f 'managed DLLs shipped', $managed.Count) -ForegroundColor Green
Write-Host ''
Write-Host "  $exe"

if ($Run) {
    Get-Process Themia -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Process $exe
    Write-Host '  started - look in the notification area' -ForegroundColor Cyan
}
