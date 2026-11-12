using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.DriveFacts;

[assembly: ThemiaWidget("driveFacts", typeof(DriveFactsModule))]

namespace Themia.Widget.DriveFacts;

/// <summary>[S-11026] One drive's facts as fact rows, which and in what order its own pick. SPEC.md beside this file.</summary>
public sealed class DriveFactsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("driveFacts"),
        ConfigType = typeof(DriveFactsConfig),
        CreateDefault = () => new DriveFactsConfig(),
        Json = DriveFactsJsonContext.Default,
        Keywords = ["drive", "disk", "storage", "free space", "capacity", "file system"],
        Label = DriveFactsStr.DriveFacts_Label.Local(),
        Description = DriveFactsStr.DriveFacts_Desc.Local(),
        Glyph = Glyphs.Disk,
        Sizing = c => FactsRules.Sizing((FactsConfig)c),
        SizeSets = FactsRules.SizeSets,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Devices, 2, 1),
    };

    /// <summary>[S-11026] Which drive (the reading's line, [S-4674]), then which facts in what order ([S-11007]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config) => Lines(menu, (DriveFactsConfig)config, DriveList.Now());

    internal static void Lines(WidgetMenuLines menu, DriveFactsConfig config, IReadOnlyList<DriveEntry> drives)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        menu.Lines.Add(DriveMenuLines.Drive(menu, config.Drive, value => config.Drive = value, drives));
        menu.Lines.Add(AtomMenuLines.Facts(menu, config));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new DriveFactsView(host);
}

/// <summary>[S-11026] What one drive facts widget remembers: its drive (empty: the system drive) and its pick.</summary>
public sealed class DriveFactsConfig : FactsConfig, IDriveChoice
{
    public DriveFactsConfig()
        : base(DriveRules.Facts)
    {
    }

    /// <summary>The drive's root (<c>C:\</c>); empty for the system drive.</summary>
    public string Drive { get; set; } = string.Empty;

    public override WidgetConfig Clone()
    {
        var copy = new DriveFactsConfig { Drive = Drive };
        CopyTo(copy);
        return copy;
    }

    /// <summary>Anything that is not a drive letter is the system drive again, loudly; the pick as every facts atom's.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());
        Drive ??= string.Empty;
        if (Drive.Length > 0)
        {
            if (DiskRules.Root(Drive) is { } root)
            {
                Drive = root;
            }
            else
            {
                notes.Add($"Drive facts named '{Drive}', which is not a drive; showing the system drive");
                Drive = string.Empty;
            }
        }

        return notes;
    }
}

/// <summary>
/// [S-11026] The drive's picked facts as fact rows ([S-6711]) under the header that names the drive; a
/// drive without figures keeps its rows, each it cannot read "--" dimmed ([S-11007]). The drives come
/// from the host's one walk (<see cref="IDrives"/>), as the Drive reading's.
/// </summary>
internal sealed partial class DriveFactsView : StackPanel, IWidgetView, IWidgetSurfaceAware
{
    private readonly IWidgetHost _host;
    private readonly List<RowBlock> _rows = [];
    private IWidgetSurface? _surface;
    private IDisposable? _watch;
    private DriveFactsConfig _config = new();

    public DriveFactsView(IWidgetHost host)
    {
        _host = host;
        VerticalAlignment = VerticalAlignment.Top;
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _config = config as DriveFactsConfig ?? new DriveFactsConfig();
        _watch?.Dispose();
        _watch = _host.Drives.Watch(ticker, Show);
        Show();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DriveFactsConfig ?? new DriveFactsConfig();
        Show();
    }

    private void OnLanguage(Locale locale) => Show();

    private void Show()
    {
        if (_previewDrive is null && !_host.Drives.HasValue)
        {
            return;
        }

        var locale = Strings.Current;
        var drive = _previewDrive ?? DriveRules.Pick(_host.Drives.Drives, _config.Drive, SystemDrive.Root);
        _surface?.SetHeaderTitle(DriveRules.Title(locale, drive));
        var facts = FactsRules.Pick(locale, DriveRules.Sheet(locale, drive), _config.Facts, DriveRules.Facts);

        while (_rows.Count < facts.Count)
        {
            var row = new RowBlock(RowKind.Fact, lead: 0);
            _rows.Add(row);
            Children.Add(row);
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Visibility = i < facts.Count ? Visibility.Visible : Visibility.Collapsed;
            if (i < facts.Count)
            {
                _rows[i].Show(facts[i].Label, facts[i].Value);
                _rows[i].Available = facts[i].Available;
            }
        }
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _watch?.Dispose();
        _watch = null;
    }

#if DEBUG
    private DriveEntry? _previewDrive;
#else
    // Release has no renders: the field's Debug-only writer (the .Preview.cs part) is not compiled.
    private DriveEntry? _previewDrive => null;
#endif
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DriveFactsConfig))]
internal sealed partial class DriveFactsJsonContext : JsonSerializerContext;
