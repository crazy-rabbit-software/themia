using System.Text.Json;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.NetworkFacts.Tests;

/// <summary>
/// [S-14960]..[S-14964] The network kinds' one Adapter setting (owner, 2026-11-21: "local address you should be able to select
/// the network adapter, for the similar widgets the same"): who holds it, its line, the hub reading the chosen adapter, and an
/// adapter that is not there kept and shown "--" with why.
/// </summary>
public sealed class NetworkAdapterTests
{
    private const string Wifi = "{5A3F2C1E-0B7D-4E8A-9C61-2D4F8B0E7A13}";
    private const string Cable = "{11111111-2222-3333-4444-555555555555}";
    private const string Gone = "{99999999-8888-7777-6666-555555555555}";

    /// <summary>[S-14960] The kinds that read one adapter's figures: every network fact but the public address, the reading (the download's graph stands in for the removed history).</summary>
    private static readonly string[] Holders = ["network", "networkDownload", "networkLinkSpeed", "networkLocalAddress", "networkReceived", "networkSent", "networkUpload"];

    /// <summary>Wi-Fi busy, the cable quiet: each with its own rates, totals, address and link.</summary>
    private static ThroughputSample Two() => new(
        3_100_000,
        310_000,
        [
            new InterfaceRate(Wifi, "Wi-Fi", 3_000_000, 300_000, 9_000_000, 900_000, AdapterMedium.Wireless, LinkBitsPerSecond: 866_700_000),
            new InterfaceRate(Cable, "Ethernet", 100_000, 10_000, 1_000_000, 100_000, AdapterMedium.Ethernet, LinkBitsPerSecond: 1_000_000_000),
        ],
        10_000_000,
        1_000_000);

    private static readonly Dictionary<string, string> Addresses = new() { [Wifi] = "192.168.1.42", [Cable] = "10.0.0.7" };

    private static readonly PublicAddressReading Off = new(PublicAddressState.Off);

    private static string Fact(DetailSheet sheet, string key) => MetricRules.Pick(sheet, key).Value.Text;

    [Fact]
    public void S14960_the_network_kinds_that_read_an_adapter_hold_the_one_shared_setting_and_the_public_address_does_not()
    {
        var held = ModuleRegistry.Widgets.Where(m => SharedSettings.NetworkAdapter.Holds(m.Descriptor.CreateDefault())).Select(m => m.Descriptor.Id.Value).Order(StringComparer.Ordinal);
        Assert.Equal(Holders, held);
        Assert.False(SharedSettings.NetworkAdapter.Holds(new NetworkPublicAddressConfig()));
        Assert.False(SharedSettings.NetworkAdapter.Holds(ModuleCatalog.Of(new WidgetId("networkAdapters")).CreateDefault())); // every adapter, a row each
        Assert.False(SharedSettings.NetworkAdapter.Holds(ModuleCatalog.Of(new WidgetId("ping")).CreateDefault())); // a host, not an adapter
        Assert.All(Holders, kind => Assert.Null(((IHoldsAdapter)ModuleCatalog.Of(new WidgetId(kind)).CreateDefault()).Adapter)); // Automatic
    }

    [Fact]
    public void S14960_a_chosen_adapter_survives_a_save_and_a_clone()
    {
        foreach (var kind in Holders)
        {
            var config = ModuleCatalog.Of(new WidgetId(kind)).CreateDefault();
            ((IHoldsAdapter)config).Adapter = Cable;
            ((IHoldsAdapter)config).AdapterName = "Ethernet";
            var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
            var back = (IHoldsAdapter)JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!.Clone();
            Assert.Equal((Cable, "Ethernet"), (back.Adapter, back.AdapterName));
        }
    }

    [Fact]
    public void S14964_an_adapter_this_pc_has_not_is_kept_at_load_and_a_blank_one_is_automatic()
    {
        var unknown = new NetworkLocalAddressConfig { Adapter = Gone, AdapterName = "USB Ethernet" };
        Assert.Empty(unknown.Repair());
        Assert.Equal((Gone, "USB Ethernet"), (unknown.Adapter, unknown.AdapterName));

        var blank = new NetworkConfig { Adapter = "  ", AdapterName = "Wi-Fi" };
        Assert.NotEmpty(blank.Repair());
        Assert.Equal(((string?)null, (string?)null), (blank.Adapter, blank.AdapterName));

        var padded = new NetworkDownloadConfig { Adapter = " " + Wifi + " ", AdapterName = "Wi-Fi" };
        Assert.Single(padded.Repair());
        Assert.Equal(Wifi, padded.Adapter);
    }

    [Fact]
    public void S14963_automatic_is_the_sheet_as_it_always_was()
    {
        var automatic = NetworkAdapterRules.Sheet(Locale.En, Two(), Addresses, Off, new NetworkLocalAddressConfig());
        var before = NetworkDetailRules.Sheet(Locale.En, Two(), Addresses, Off);
        Assert.True(automatic.SameAs(before));
        Assert.Equal("192.168.1.42", Fact(automatic, "localAddress")); // the busiest adapter's
    }

    [Fact]
    public void S14963_a_chosen_adapter_reads_its_own_address_link_rates_and_totals()
    {
        var sheet = NetworkAdapterRules.Sheet(Locale.En, Two(), Addresses, Off, new NetworkLocalAddressConfig { Adapter = Cable.ToLowerInvariant() });

        Assert.Equal("10.0.0.7", Fact(sheet, "localAddress"));
        Assert.Equal(NetworkDetailRules.LinkSpeed(Locale.En, 1_000_000_000), Fact(sheet, "linkSpeed"));
        Assert.Equal(100_000d, MetricRules.Pick(sheet, "download").Value.Number);
        Assert.Equal(10_000d, MetricRules.Pick(sheet, "upload").Value.Number);
        Assert.Equal(1_000_000d, MetricRules.Pick(sheet, "received").Value.Number);
        Assert.Equal(100_000d, MetricRules.Pick(sheet, "sent").Value.Number);
    }

    [Fact]
    public void S14963_a_chosen_adapter_that_is_not_there_is_every_fact_unavailable_with_why_never_another_adapter_s()
    {
        var held = new NetworkDownloadConfig { Adapter = Gone, AdapterName = "USB Ethernet" };
        var sheet = NetworkAdapterRules.Sheet(Locale.En, Two(), Addresses, Off, held);

        foreach (var key in new[] { "localAddress", "linkSpeed", "download", "upload", "received", "sent" })
        {
            var shown = MetricRules.Pick(sheet, key);
            Assert.Equal("--", shown.Value.Text);
            Assert.False(shown.Value.Available);
            Assert.Equal("USB Ethernet is not connected", shown.Why);
        }

        // No sample yet is waiting, whatever is chosen.
        Assert.Same(DetailSheet.Waiting, NetworkAdapterRules.Sheet(Locale.En, null, Addresses, Off, held));
    }

    [Fact]
    public void S14963_the_hub_reads_the_chosen_adapter_as_its_series_of_the_one_throughput_feed()
    {
        // The meter, its history and a rate's line ask the one Network feed for the adapter's own series.
        var meter = SystemInterests.For(new NetworkConfig { Adapter = Cable }, @"C:\");
        Assert.Equal([FeedKey.Create(SystemSource.Network)], meter.Feeds);
        Assert.Equal(new SeriesInterest(FeedKey.Create(SystemSource.Network), Cable), Assert.Single(meter.Series));
        Assert.Equal(Cable, Assert.Single(SystemInterests.For(new NetworkDownloadConfig { Adapter = Cable, Trend = MetricTrend.Line }, @"C:\").Series).Instance);
        Assert.Equal(Cable, Assert.Single(SystemInterests.For(new NetworkUploadConfig { Adapter = Cable, Trend = MetricTrend.Line }, @"C:\").Series).Instance);
        Assert.Equal(string.Empty, Assert.Single(SystemInterests.For(new NetworkConfig(), @"C:\").Series).Instance); // Automatic: every adapter

        // The series of that instance reads that adapter: its download the value, its upload the second.
        var chosen = NetworkAdapterRules.Reading(Locale.En, Two(), Cable);
        Assert.True(chosen.Available);
        Assert.Equal((100_000d, 10_000d), (chosen.Value, chosen.Second));
        Assert.Equal((3_100_000d, 310_000d), (NetworkAdapterRules.Reading(Locale.En, Two(), string.Empty).Value, NetworkAdapterRules.Reading(Locale.En, Two(), string.Empty).Second));

        // One that is not there: "--" and why, no point.
        var gone = NetworkAdapterRules.Reading(Locale.En, Two(), Gone);
        Assert.False(gone.Available);
        Assert.Null(gone.Fraction);
        Assert.Equal("The chosen adapter is not connected", gone.Reason);
    }

    [Fact]
    public void S14961_the_line_lists_windows_adapters_up_or_not_but_never_a_mirror_a_binding_a_loopback_or_wifi_direct()
    {
        string[] names = ["Wi-Fi", "Wi-Fi-WFP Native MAC Layer LightWeight Filter-0000", "Ethernet"];
        Assert.True(NetworkAdapterRules.Offered(false, "Wi-Fi", "Intel(R) Wi-Fi 6E AX211 160MHz", names));
        Assert.True(NetworkAdapterRules.Offered(false, "Ethernet", "Realtek PCIe GbE Family Controller", names));
        Assert.False(NetworkAdapterRules.Offered(true, "Loopback Pseudo-Interface 1", null, names));
        Assert.False(NetworkAdapterRules.Offered(false, "Wi-Fi-WFP Native MAC Layer LightWeight Filter-0000", null, names));
        Assert.False(NetworkAdapterRules.Offered(false, "Ethernet-Npcap Packet Driver (NPCAP)-0000", null, names));
        Assert.False(NetworkAdapterRules.Offered(false, "Local Area Connection* 1", "Microsoft Wi-Fi Direct Virtual Adapter", names));
    }

    [Fact]
    public void S14962_the_line_is_automatic_then_the_adapters_connected_first_and_keeps_one_that_is_gone_by_its_name()
    {
        NetworkAdapterEntry[] adapters = [new(Cable, "Ethernet", AdapterMedium.Ethernet, false), new(Wifi, "Wi-Fi", AdapterMedium.Wireless, true)];

        var (options, current) = NetworkAdapterRules.Options(Locale.En, adapters, null, null);
        Assert.Equal(["Automatic", "Wi-Fi", "Ethernet (not connected)"], options.Select(o => o.Text));
        Assert.Equal(string.Empty, current);

        var (kept, held) = NetworkAdapterRules.Options(Locale.En, adapters, Gone, "USB Ethernet");
        Assert.Equal((Gone, "USB Ethernet (not connected)"), kept[^1]);
        Assert.Equal(Gone, held);

        Assert.Equal(Wifi, NetworkAdapterRules.Options(Locale.En, adapters, Wifi.ToLowerInvariant(), "Wi-Fi").Current);
    }

    [Fact]
    public void S14962_every_holder_s_page_has_the_one_adapter_line_marked_shared_and_a_pick_stores_its_id_and_name()
    {
        foreach (var kind in Holders)
        {
            var module = ModuleRegistry.Widgets.Single(m => m.Descriptor.Id.Value == kind);
            var config = module.Descriptor.CreateDefault();
            var saves = 0;
            var menu = new WidgetMenuLines(() => saves++);
            module.MenuLines(menu, config);

            var line = Assert.Single(menu.Lines, l => l.Shares == SharedSettings.NetworkAdapter.Key);
            Assert.Equal(Str.NetworkAdapter_Label.Text(), line.Text);
            var wifi = Assert.Single(line.Children!, c => c.Text == "Wi-Fi");
            wifi.Action!();
            Assert.Equal((SamplePc.WifiId, "Wi-Fi", 1), (((IHoldsAdapter)config).Adapter, ((IHoldsAdapter)config).AdapterName, saves));
        }

        // The public address's page has its note alone.
        var publicMenu = new WidgetMenuLines(() => { });
        ((IWidgetModule)new NetworkPublicAddressModule()).MenuLines(publicMenu, new NetworkPublicAddressConfig());
        Assert.DoesNotContain(publicMenu.Lines, l => l.Shares is not null);
    }

    [Fact]
    public void S14962_a_shown_line_is_built_again_when_windows_adapters_change_and_not_when_they_read_the_same()
    {
        var menu = new WidgetMenuLines(() => { });
        var outdated = 0;
        menu.Outdated += () => outdated++;
        ((IWidgetModule)new NetworkLocalAddressModule()).MenuLines(menu, new NetworkLocalAddressConfig());

        try
        {
            // An address change that leaves the adapters as they were: nothing to build again.
            Themia.Modules.Tests.TestAdapters.Change([.. SamplePc.Adapters]);
            Assert.Equal(0, outdated);

            // The dock's Ethernet plugged in: the line said "not connected", so it is built again, once.
            Themia.Modules.Tests.TestAdapters.Change([SamplePc.Adapters[0], SamplePc.Adapters[1] with { Connected = true }]);
            Assert.Equal(1, outdated);
            Themia.Modules.Tests.TestAdapters.Change(null);
            Assert.Equal(1, outdated); // told once: the rebuilt lines follow from there
        }
        finally
        {
            Themia.Modules.Tests.TestAdapters.Change(null);
        }
    }

    [Fact]
    public void S14962_several_network_widgets_selected_set_their_adapter_once_for_all_of_them()
    {
        var view = ViewState.Create("v");
        foreach (var (kind, id) in new[] { ("networkLocalAddress", "local"), ("networkDownload", "down"), ("network", "meter"), ("networkPublicAddress", "public") })
        {
            var widget = WidgetInstance.Create(new WidgetId(kind), new FracRect(0, 0, 0.1, 0.1));
            widget.Id = id;
            view.Widgets.Add(widget);
        }

        var state = new AppState { Views = [view] };
        var selection = new WidgetSelection();
        selection.Replace(view.Id, ["local", "down", "meter", "public"]);

        var row = Assert.Single(SelectionPage.Of(state, selection, view.Id)!.Shared, r => r.Setting == SharedSettings.NetworkAdapter);
        Assert.Equal(["local", "down", "meter"], row.Holders);
        Assert.False(row.Mixed);

        // One write from the group's line sets every holder; the public address has nothing to set.
        var scratch = view.Find("local")!.Config.Clone();
        ((IHoldsAdapter)scratch).Adapter = Cable;
        ((IHoldsAdapter)scratch).AdapterName = "Ethernet";
        Assert.Equal(3, QuickSet.SetShared(view, ["local", "down", "meter", "public"], SharedSettings.NetworkAdapter, scratch));
        Assert.All(["local", "down", "meter"], id => Assert.Equal(Cable, ((IHoldsAdapter)view.Find(id)!.Config).Adapter));
    }
}
