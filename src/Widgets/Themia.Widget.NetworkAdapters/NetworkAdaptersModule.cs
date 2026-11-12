using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.NetworkAdapters;

[assembly: ThemiaWidget("networkAdapters", typeof(NetworkAdaptersModule))]

namespace Themia.Widget.NetworkAdapters;

/// <summary>[S-11021] Every counted network adapter, a row each: its rates, its address and link speed. SPEC.md beside this file.</summary>
public sealed class NetworkAdaptersModule : IWidgetModule
{
    /// <summary>[S-11021] A click on the card opens Windows' network status page, as the reading's does.</summary>
    internal static CardOpen? Opens => CardOpen.Settings("network-status");

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("networkAdapters"),
        ConfigType = typeof(NetworkAdaptersConfig),
        CreateDefault = () => new NetworkAdaptersConfig(),
        Json = NetworkAdaptersJsonContext.Default,
        Keywords = ["network", "adapters", "wifi", "ethernet", "ip address", "link speed"],
        Label = NetworkAdaptersStr.NetworkAdapters_Label.Local(),
        Description = NetworkAdaptersStr.NetworkAdapters_Desc.Local(),
        Glyph = Glyphs.Network,
        Sizing = c => KitSizing.Sizing(Shown((NetworkAdaptersConfig)c), addFooter: false),
        SizeSets = (c, s) => KitSizing.SizeSets(c, s, Shown((NetworkAdaptersConfig)c), addFooter: false),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.NetworkReading, 0, 3),
    };

    /// <summary>[S-11021] Its rows, two lines each.</summary>
    internal static KitRowsChoice Shown(NetworkAdaptersConfig config) => new(KitSizing.ClampRows(config.Rows), 2);

    /// <summary>[S-11021] Its one setting: how many rows ([S-2440]); a row always has its two lines.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        menu.Lines.AddRange(KitMenuLines.Rows(menu, (NetworkAdaptersConfig)config, offersLines: false));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new NetworkAdaptersPart());
}

/// <summary>[S-11021] What one adapters list remembers: how many rows it shows.</summary>
public sealed class NetworkAdaptersConfig : DetailConfig, IReadsSystem, IKitRows
{
    public const int DefaultRows = 4;

    /// <summary>[S-11021] How many rows the list shows (1..20); more adapters scroll inside them.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>Every row has two lines: the adapter's rates, its address and link speed under it.</summary>
    [JsonIgnore]
    public int Lines
    {
        get => 2;
        set
        {
            // ignore: an adapter row always has its two lines; the menu offers no choice.
        }
    }

    /// <summary>[S-2513] The addresses and the throughput, the network reading's own feed.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.LocalAddresses, SystemSource.Network);

    public override WidgetConfig Clone() => new NetworkAdaptersConfig { Rows = Rows };

    public override IReadOnlyList<string> Repair()
    {
        var rows = KitSizing.ClampRows(Rows);
        if (rows == Rows)
        {
            return [];
        }

        var note = $"{Kind}: {Rows} rows is outside {KitSizing.MinRows}..{KitSizing.MaxRows}; using {rows}";
        Rows = rows;
        return [note];
    }
}

/// <summary>
/// [S-2416] [S-11021] The adapters from the network reading's own hub feed (the list rows of the
/// network sheet, [S-2416]): busiest first, each its kind's icon, its name, its rates and its address
/// and link speed; no adapter is "No connection".
/// </summary>
internal sealed class NetworkAdaptersPart : DetailPart
{
    private readonly WatchSlot<ThroughputSample?> _throughput = new();
    private readonly WatchSlot<IReadOnlyDictionary<string, string>> _addresses = new();
    private IDetailHost? _host;

    public override WidgetId Kind => new("networkAdapters");

    public override bool HasHero => false;

    public override DetailAtom? Atom => DetailAtom.List;

    public override int ListLines => 2;

    public override CardOpen? Opens(WidgetConfig config) => NetworkAdaptersModule.Opens;

    public override void Watch(ISystemFeeds hub, WidgetConfig config, IDetailHost host)
    {
        _host = host;
        _addresses.Hold(hub, Sources.LocalAddresses, static _ => { });
        _throughput.Hold(hub, Sources.Network, _ => Show());
        Show();
    }

    private void Show()
    {
        if (_host is not { } host || !_throughput.HasValue)
        {
            return;
        }

        var addresses = _addresses.HasValue ? _addresses.Value : new Dictionary<string, string>();
        var sheet = NetworkDetailRules.Sheet(Strings.Current, _throughput.Value, addresses, new PublicAddressReading(PublicAddressState.Off));
        // [S-11021] Two lines: the name and the download, then the address and link speed under them
        // (the upload is the reading's, under the download there).
        var rows = sheet.Rows.Select(r => r with { Second = r.Detail, Detail = null }).ToList();
        host.Present(new DetailSheet(null, null, [], rows, Note: rows.Count == 0 ? Str.Details_Network_NoConnection.Text() : null));
    }

    public override void Detach()
    {
        _throughput.Release();
        _addresses.Release();
        _host = null;
    }
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NetworkAdaptersConfig))]
internal sealed partial class NetworkAdaptersJsonContext : JsonSerializerContext;
