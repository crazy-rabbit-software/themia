<#
.SYNOPSIS
    Scaffold a widget or provider module: one new folder, and one line in Themia.slnx.

.DESCRIPTION
    [S-6911] [S-6912] A module is a project one folder under src/Widgets (Themia.Widget.<Name>) or
    src/Providers (Themia.Provider.<Name>); adding the folder is the whole registration. This writes
    that folder and nothing else outside it but the solution line (so the project shows in an IDE;
    build, test and publish do not read the solution):

      widget    <Name>.csproj, <Name>Module.cs (descriptor, view factory, JSON context, glyph),
                <Name>Config.cs, <Name>View.cs, Localization/Locales/*.json (30 languages),
                SPEC.md (its own tag block), Tests/<Name>Tests.cs
      provider  <Name>.csproj, <Name>Provider.cs, Localization/Locales/*.json, SPEC.md,
                Tests/<Name>ProviderTests.cs

    The English texts are listed under $englishOnly and the 29 other languages start empty, so the
    module is complete by the rule of [S-1105] from its first build; translating a key means adding
    it to every file and taking it off the list. The SPEC block is the next free hundred at or above
    7400 (7000..7399 are the kit's and its three pilots', docs/WIDGET-KIT.md), unless -TagBlock says.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/new-module.ps1 -Kind widget -Name MoonPhase -Label "Moon phase" -Description "Tonight's moon"

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/new-module.ps1 -Kind provider -Name Todoist -Label "Todoist"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [ValidateSet('widget', 'provider')] [string] $Kind,
    [Parameter(Mandatory = $true)] [string] $Name,
    [Parameter(Mandatory = $true)] [string] $Label,
    [string] $Description = '',
    # [S-6907] The add menu folder it is at home in (an AddFolderId member). Widgets only.
    [string] $Shelf = 'More',
    # Segoe Fluent Icons code point of its glyph. Widgets only.
    [string] $Glyph = 'E71D',
    [int] $TagBlock = 0
)

$ErrorActionPreference = 'Stop'

if ($Name -notmatch '^[A-Z][A-Za-z0-9]*$') {
    throw "Name '$Name' must be PascalCase letters and digits (the id is it in camelCase)."
}

if ($Glyph -notmatch '^[0-9A-Fa-f]{4,5}$') {
    throw "Glyph '$Glyph' must be a hexadecimal code point such as E71D."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$id = $Name.Substring(0, 1).ToLowerInvariant() + $Name.Substring(1)
$project = if ($Kind -eq 'widget') { "Themia.Widget.$Name" } else { "Themia.Provider.$Name" }
$parent = if ($Kind -eq 'widget') { 'Widgets' } else { 'Providers' }
$folder = Join-Path $repoRoot "src\$parent\$project"
if ($Description -eq '') { $Description = $Label }

if (Test-Path $folder) {
    throw "$folder exists already."
}

# The ids of a widget and of a provider each are unique; a folder of the other kind may share a name.
foreach ($existing in @(Get-ChildItem (Join-Path $repoRoot "src\$parent") -Directory -ErrorAction SilentlyContinue)) {
    if ($existing.Name -ieq $project) { throw "$($existing.Name) exists already." }
}

$codes = @('en', 'de', 'es', 'fr', 'it', 'ja', 'pl', 'pt', 'zh', 'hi', 'ar', 'bn', 'ru', 'id', 'ur', 'mr', 'vi', 'ko', 'nl', 'tr',
    'zh-Hant', 'te', 'ha', 'pa', 'sw', 'fil', 'ta', 'fa', 'th', 'gu')

# ---- the SPEC tag block ---------------------------------------------------------------------------

if ($TagBlock -eq 0) {
    $specs = @(Join-Path $repoRoot 'SPEC.md') + @(Get-ChildItem (Join-Path $repoRoot 'src') -Filter 'SPEC.md' -Recurse -Depth 2 | ForEach-Object { $_.FullName })
    $used = New-Object 'System.Collections.Generic.HashSet[int]'
    foreach ($spec in $specs) {
        foreach ($m in [regex]::Matches([System.IO.File]::ReadAllText($spec), '\*\*\[S-(\d+)\]\*\*')) {
            [void]$used.Add([int]$m.Groups[1].Value)
        }
    }

    $TagBlock = 7400
    while (@($used | Where-Object { $_ -ge $TagBlock -and $_ -lt $TagBlock + 100 }).Count -gt 0) {
        $TagBlock += 100
    }
}

$tag = $TagBlock
$tagEnd = $TagBlock + 99

# ---- writing --------------------------------------------------------------------------------------

$utf8 = New-Object System.Text.UTF8Encoding $false

function Write-ModuleFile([string] $relative, [string] $text) {
    $path = Join-Path $folder $relative
    $dir = Split-Path -Parent $path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    $text = $text.Replace("`r`n", "`n").Replace('{{Name}}', $Name).Replace('{{id}}', $id).Replace('{{Project}}', $project).
        Replace('{{tag}}', "$tag").Replace('{{tagEnd}}', "$tagEnd").Replace('{{Shelf}}', $Shelf).Replace('{{Glyph}}', $Glyph.ToUpperInvariant())
    [System.IO.File]::WriteAllText($path, $text, $utf8)
}

function Json-String([string] $value) {
    return '"' + $value.Replace('\', '\\').Replace('"', '\"') + '"'
}

New-Item -ItemType Directory -Force $folder | Out-Null

Write-ModuleFile "$project.csproj" @'
<Project Sdk="Microsoft.NET.Sdk" />
'@

# Its strings: the name and the line under it, English only until translated.
$prefix = if ($Kind -eq 'widget') { $id } else { "$id.provider" }
$english = "{`n  `"`$englishOnly`": [`"$prefix.label`", `"$prefix.desc`"],`n  `"$prefix.label`": $(Json-String $Label),`n  `"$prefix.desc`": $(Json-String $Description)`n}`n"
Write-ModuleFile 'Localization\Locales\en.json' $english
foreach ($code in $codes | Where-Object { $_ -ne 'en' }) {
    Write-ModuleFile "Localization\Locales\$code.json" "{`n}`n"
}

if ($Kind -eq 'widget') {
    Write-ModuleFile "$($Name)Module.cs" @'
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using {{Project}};

[assembly: ThemiaWidget("{{id}}", typeof({{Name}}Module))]

namespace {{Project}};

/// <summary>[S-{{tag}}] The {{id}} widget. Its behaviour is SPEC.md beside this file.</summary>
public sealed class {{Name}}Module : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+{{Glyph}}), the module's own.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0x{{Glyph}});

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("{{id}}"),
        ConfigType = typeof({{Name}}Config),
        CreateDefault = () => new {{Name}}Config(),
        Json = {{Name}}JsonContext.Default,
        Label = {{Name}}Str.{{Name}}_Label.Local(),
        Description = {{Name}}Str.{{Name}}_Desc.Local(),
        Glyph = Glyph,
        DefaultWidth = 280,
        DefaultHeight = 180,
        Traits = WidgetTraits.CanHaveHeader,
        // [S-6907] Its own run at the end of its folder; move it to where it belongs among its neighbours.
        Add = new(AddFolderId.{{Shelf}}, 99, 0),
    };

    public FrameworkElement CreateView() => new {{Name}}View();

    /// <summary>
    /// [S-6904] Its settings rows; none, so it has no settings page ([S-1750]). Rows come from the
    /// host's FluentLayout, which a module reaches once it is in the SDK (docs/WIDGET-KIT.md 11.1).
    /// </summary>
    public void SettingsRows(WidgetSettingsRows rows, Themia.Core.Model.WidgetConfig config)
    {
    }
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof({{Name}}Config))]
internal sealed partial class {{Name}}JsonContext : JsonSerializerContext;
'@

    Write-ModuleFile "$($Name)Config.cs" @'
using Themia.Core.Model;

namespace {{Project}};

/// <summary>[S-{{tag}}] What one {{id}} widget remembers. Every property is stored; copy each in <see cref="Clone"/>.</summary>
public sealed class {{Name}}Config : WidgetConfig
{
    public override WidgetConfig Clone() => new {{Name}}Config();
}
'@

    Write-ModuleFile "$($Name)View.cs" @'
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace {{Project}};

/// <summary>[S-{{tag}}] The {{id}} widget's view: lays itself out for the room it has ([S-663]).</summary>
internal sealed partial class {{Name}}View : Grid, IWidgetView, IWidgetSizeAware
{
    private readonly TextBlock _text = Fluent.Text(FluentType.Body);

    public {{Name}}View()
    {
        _text.Margin = WidgetLayout.Inset;
        Children.Add(_text);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config) => Render();

    public void ApplySize(WidgetArea area) => Render();

    public void Detach() => Strings.Changed -= OnLanguage;

    private void OnLanguage(Locale locale) => Render();

    private void Render() => _text.Text = {{Name}}Str.{{Name}}_Label.Text();
}
'@

    Write-ModuleFile 'SPEC.md' @'
# {{Name}} ({{id}})

The module `{{id}}` ([S-6911]). Its tags are [S-{{tag}}]..[S-{{tagEnd}}], this file's alone; the root
`SPEC.md` holds the host's sections.

Owning files: everything in this folder. Tests: `Tests/{{Name}}Tests.cs` (run by
`tests/Themia.Modules.Tests`).

- **[S-{{tag}}]** What it shows, at every size tier ([S-660]..[S-666], concise first).

Needs a manual pass: every size tier on the live desktop (showcase views, [S-1810]).
'@

    Write-ModuleFile "Tests\$($Name)Tests.cs" @'
using Themia.Core.Model;
using Xunit;

namespace {{Project}}.Tests;

/// <summary>[S-{{tag}}] The {{id}} widget's rules.</summary>
public sealed class {{Name}}Tests
{
    [Fact]
    public void S{{tag}}_a_clone_is_a_config_of_its_own()
    {
        var config = new {{Name}}Config();
        Assert.IsType<{{Name}}Config>(config.Clone());
        Assert.NotSame(config, config.Clone());
    }
}
'@
}
else {
    Write-ModuleFile "$($Name)Provider.cs" @'
using Themia.Core.Accounts;
using Themia.Sdk;
using {{Project}};

[assembly: ThemiaProvider("{{id}}", typeof({{Name}}Provider))]

namespace {{Project}};

/// <summary>[S-{{tag}}] The {{id}} provider: a service widgets read from. Its behaviour is SPEC.md beside this file.</summary>
public sealed class {{Name}}Provider : IProviderModule
{
    public static readonly ProviderId ProviderId = new("{{id}}");

    public ProviderId Id => ProviderId;

    public LocalText Label => {{Name}}Str.{{Name}}_Provider_Label.Local();

    /// <summary>A kit service's one feature is its provider's id (docs/WIDGET-KIT.md section 5.7).</summary>
    public FeatureSet Features { get; } = FeatureSet.Of(new FeatureId("{{id}}"));

    /// <summary>How its accounts sign in; the kit's sign-in strategies replace this (docs/WIDGET-KIT.md 11.2, step 11).</summary>
    public AccountAuth Auth => AccountAuth.OAuthRefresh;

    public int AddRank => 100;

    /// <summary>Its accounts follow the host's Accounts row of Settings > Widgets > Updates ([S-3780]).</summary>
    public CadenceRow? Cadence => null;
}
'@

    Write-ModuleFile 'SPEC.md' @'
# {{Name}} provider ({{id}})

The provider module `{{id}}` ([S-6910], [S-6911]). Its tags are [S-{{tag}}]..[S-{{tagEnd}}], this
file's alone.

Owning files: everything in this folder. Tests: `Tests/{{Name}}ProviderTests.cs` (run by
`tests/Themia.Modules.Tests`).

- **[S-{{tag}}]** What it offers widgets: the feature `{{id}}`.

Needs a manual pass: a live sign-in.
'@

    Write-ModuleFile "Tests\$($Name)ProviderTests.cs" @'
using Themia.Sdk;
using Xunit;

namespace {{Project}}.Tests;

/// <summary>[S-{{tag}}] The {{id}} provider's rules.</summary>
public sealed class {{Name}}ProviderTests
{
    [Fact]
    public void S{{tag}}_it_offers_its_own_feature()
        => Assert.True(new {{Name}}Provider().Features.Has(new FeatureId("{{id}}")));
}
'@
}

# ---- the one line outside the folder: the solution, for an IDE ------------------------------------

$slnx = Join-Path $repoRoot 'Themia.slnx'
$line = "    <Project Path=`"src/$parent/$project/$project.csproj`" />"
$text = [System.IO.File]::ReadAllText($slnx)
if ($text -notlike "*src/$parent/$project/*") {
    $folderTag = "  <Folder Name=`"/src/$parent/`">"
    if ($text -like "*$folderTag*") {
        $text = $text.Replace("$folderTag`n", "$folderTag`n$line`n")
    }
    else {
        $text = $text.Replace("  <Folder Name=`"/tests/`">", "$folderTag`n$line`n  </Folder>`n  <Folder Name=`"/tests/`">")
    }

    [System.IO.File]::WriteAllText($slnx, $text, $utf8)
}

Write-Host "Created $project in src\$parent (id '$id', SPEC block [S-$tag]..[S-$tagEnd])." -ForegroundColor Green
Write-Host '  Build:  dotnet build src/Themia.App/Themia.App.csproj -c Debug'
Write-Host '  Test:   dotnet test tests/Themia.Modules.Tests/Themia.Modules.Tests.csproj'
