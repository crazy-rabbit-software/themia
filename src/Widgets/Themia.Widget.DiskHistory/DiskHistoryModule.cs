using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Meters;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.DiskHistory;

[assembly: ThemiaWidget("diskHistory", typeof(DiskHistoryModule))]

namespace Themia.Widget.DiskHistory;

/// <summary>[S-11018] A drive's reads and writes over time: its history alone, edge to edge. SPEC.md beside this file.</summary>
public sealed class DiskHistoryModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("diskHistory"),
        ConfigType = typeof(DiskHistoryConfig),
        CreateDefault = () => new DiskHistoryConfig(),
        Json = DiskHistoryJsonContext.Default,
        Keywords = ["storage", "ssd", "hdd", "drive", "io", "graph", "chart", "history"],
        Label = DiskHistoryStr.DiskHistory_Label.Local(),
        Description = DiskHistoryStr.DiskHistory_Desc.Local(),
        Glyph = Glyphs.Disk,
        Sizing = _ => MeterSizing.History,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.System, 0, 4),
    };

    /// <summary>[S-11018] The drive ([S-1160], [S-1822]) and the chart's span ([S-1121]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config) => Lines(menu, (DiskHistoryConfig)config, DriveList.Now());

    internal static void Lines(WidgetMenuLines menu, DiskHistoryConfig config, IReadOnlyList<DriveEntry> drives)
    {
        ArgumentNullException.ThrowIfNull(menu);
        menu.Lines.Add(DiskMenuLines.Drive(menu, config, drives));
        menu.Lines.Add(AtomMenuLines.Span(menu, config));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new DiskHistoryMeter(host));
}

/// <summary>[S-11018] What one disk history remembers: its drive and its span (five minutes).</summary>
public sealed class DiskHistoryConfig : HistoryConfig, IReadsSystem, IDiskScope
{
    public DiskHistoryConfig()
        : base(graphSpanSeconds: 300)
    {
    }

    /// <summary>[S-1822] One drive (<see cref="Drive"/>) or every drive together.</summary>
    public DiskScope Scope { get; set; } = DiskScope.OneDrive;

    /// <summary>[S-1160] The drive, as a root (<c>C:\</c>); empty for the system drive.</summary>
    public string Drive { get; set; } = string.Empty;

    /// <summary>[S-2513] The drive's series, the reading's own.</summary>
    public SystemInterests SystemWants(string systemRoot) => DiskScopes.Wants(this, systemRoot);

    public override WidgetConfig Clone()
    {
        var copy = new DiskHistoryConfig { Scope = Scope, Drive = Drive };
        CopyTo(copy);
        return copy;
    }

    public override IReadOnlyList<string> Repair() => [.. base.Repair(), .. DiskScopes.Repair(this, Kind.ToString())];
}

/// <summary>[S-11018] The drive's rate as a history against the window's peak ([S-1821]); the header, when it has one, names the drive.</summary>
internal sealed class DiskHistoryMeter(IWidgetHost host) : DiskPart(host)
{
    public override WidgetId Kind => new("diskHistory");

    public override MeterAtom Atom => MeterAtom.History;

    public override MeterLook Look(WidgetConfig config) => MeterLook.History;
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DiskHistoryConfig))]
internal sealed partial class DiskHistoryJsonContext : JsonSerializerContext;
