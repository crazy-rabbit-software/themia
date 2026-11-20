using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-44] The system widgets' settings as users have them, generated once with the build from before
/// they became projects (`d7dd3ae7`; Fixtures/state-system-configured.json, ids w00.., not anyone's
/// real file): every meter's style and span, the disk's drive and the ping's host, the GPU adapter, the
/// process list's ranking, the public address switch, the ping window, the drive card's drive and
/// style, the brightness screens, the mixer's and the volume's device and switches, music's style and
/// hide-when-idle, the visualizer's waveform. Each widget now reads it from its own module's JSON
/// context; this pins that the file still loads, its facts cards replaced in place by their rows' single metrics
/// ([S-14606]) and nothing else repaired, reads the same values, and that what it writes back loads again with no
/// repair and writes back byte for byte.
/// </summary>
public sealed class SystemStateFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-system-configured.json"));

    [Fact]
    public void S44_the_system_widgets_configured_state_loads_with_its_facts_cards_replaced_and_then_writes_back_unchanged()
    {
        var parsed = AppStateStore.Parse(Fixture());

        Assert.NotNull(parsed.State);
        Assert.Equal(6, parsed.Repairs.Count);
        Assert.All(parsed.Repairs, r => Assert.StartsWith("replaced ", r, StringComparison.Ordinal));

        var json = JsonSerializer.Serialize(parsed.State, StateJson.AppState);
        var again = AppStateStore.Parse(json);
        Assert.Empty(again.Repairs);
        var written = JsonSerializer.Serialize(again.State!, StateJson.AppState);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(written)), "the system widgets' state does not write back as it was read");
    }

    [Fact]
    public void S14606_each_placed_facts_card_becomes_its_shown_rows_single_metrics_stacked_in_its_spot_with_its_settings()
    {
        var parsed = AppStateStore.Parse(Fixture());
        var widgets = Assert.Single(parsed.State!.Views).Widgets;

        // In the card's place in the drawing order, its rows in its order, each one row of the old card tall.
        Assert.Equal(
            ["w06", "w07-cpuUptime", "w07-cpuSpeed", "w08-memoryPageFile", "w08-memoryInUse", "w09", "w10-networkPublicAddress", "w10-networkLocalAddress",
             "w11-batteryCycles", "w12-pingLoss", "w12-pingAverage", "w13-gpuTemperature", "w14"],
            widgets.Skip(6).Take(13).Select(w => w.Id));
        var uptime = widgets.Single(w => w.Id == "w07-cpuUptime");
        var speed = widgets.Single(w => w.Id == "w07-cpuSpeed");
        Assert.IsType<Themia.Widget.CpuFacts.CpuUptimeConfig>(uptime.Config);
        Assert.IsType<Themia.Widget.CpuFacts.CpuSpeedConfig>(speed.Config);

        // The old card was the content top, two rows of 28 and the inset (80): each row's share of its 0.25 is a 30 row's.
        var row = 0.25 * 30 / 80;
        Assert.Equal((0.02, 0.35, 0.12, row), (uptime.Rect.X, uptime.Rect.Y, uptime.Rect.W, uptime.Rect.H), new RectComparer());
        Assert.Equal(0.35 + row, speed.Rect.Y, 9);
        Assert.Equal(row, speed.Rect.H, 9);

        // Its settings go with every row: the host and the window, the adapter.
        var loss = Assert.IsType<Themia.Widget.PingFacts.PingLossConfig>(widgets.Single(w => w.Id == "w12-pingLoss").Config);
        var average = Assert.IsType<Themia.Widget.PingFacts.PingAverageConfig>(widgets.Single(w => w.Id == "w12-pingAverage").Config);
        Assert.Equal(("9.9.9.9", 120), (loss.Host, loss.Window));
        Assert.Equal(("9.9.9.9", 120), (average.Host, average.Window));
        Assert.Equal(1, Assert.IsType<Themia.Widget.GpuFacts.GpuTemperatureConfig>(widgets.Single(w => w.Id == "w13-gpuTemperature").Config).Adapter);

        // One logged line a card, naming what it became.
        Assert.Contains(parsed.Repairs, r => r.StartsWith("replaced cpuFacts 'w07'", StringComparison.Ordinal) && r.Contains("cpuUptime 'w07-cpuUptime'", StringComparison.Ordinal));
    }

    private sealed class RectComparer : IEqualityComparer<(double, double, double, double)>
    {
        public bool Equals((double, double, double, double) a, (double, double, double, double) b)
            => Math.Abs(a.Item1 - b.Item1) < 1e-9 && Math.Abs(a.Item2 - b.Item2) < 1e-9 && Math.Abs(a.Item3 - b.Item3) < 1e-9 && Math.Abs(a.Item4 - b.Item4) < 1e-9;

        public int GetHashCode((double, double, double, double) obj) => 0;
    }

    [Fact]
    public void S44_the_system_widgets_configured_state_reads_the_choices_it_holds()
    {
        var state = AppStateStore.Parse(Fixture()).State!;
        var configs = Assert.Single(state.Views).Widgets.Select(w => w.Config).ToList();
        Assert.Equal(25, configs.Count);

        var cpu = Assert.IsType<CpuConfig>(configs[0]);
        Assert.Equal(CpuStyle.Gauge, cpu.Style);
        var memory = Assert.IsType<MemoryConfig>(configs[1]);
        Assert.Equal(MemoryStyle.Bar, memory.Style);
        var battery = Assert.IsType<BatteryConfig>(configs[2]);
        Assert.Equal(BatteryStyle.Ring, battery.Style);
        var disk = Assert.IsType<DiskConfig>(configs[3]);
        Assert.Equal((DiskStyle.Bars, DiskScope.OneDrive, "D:\\"), (disk.Style, disk.Scope, disk.Drive));
        Assert.IsType<NetworkConfig>(configs[4]);
        var ping = Assert.IsType<PingConfig>(configs[5]);
        Assert.Equal((PingStyle.Signal, "example.com"), (ping.Style, ping.Host));
        var gpu = Assert.IsType<GpuConfig>(configs[6]);
        Assert.Equal((GpuStyle.Gauge, 1), (gpu.Style, gpu.Adapter));

        // [S-14606] The facts cards are their rows' single metrics now (S14606_ above): the rest keeps its place.
        var processes = Assert.IsType<TopProcessesConfig>(configs[11]);
        Assert.Equal((ProcessSort.Memory, 10, false), (processes.SortBy, processes.Rows, processes.GroupByName));
        Assert.True(Assert.IsType<Themia.Widget.NetworkFacts.NetworkPublicAddressConfig>(configs[12]).ShowsPublicAddress);

        var drive = Assert.IsType<DriveConfig>(configs[18]);
        Assert.Equal((DriveStyle.Ring, "E:\\"), (drive.Style, drive.Drive));
        Assert.IsType<RemovableDrivesConfig>(configs[19]);
        Assert.Equal(BrightnessScreens.ThisDisplay, Assert.IsType<BrightnessConfig>(configs[20]).Screens);
        var mixer = Assert.IsType<MixerConfig>(configs[21]);
        Assert.Equal(("{0.0.0.00000000}.{mixer}", "Headphones", false, true, true, false), (mixer.DeviceId, mixer.DeviceName, mixer.GroupByApp, mixer.ShowSystemSounds, mixer.OnlyActive, mixer.ShowIcons));
        var volume = Assert.IsType<VolumeConfig>(configs[22]);
        Assert.Equal(("{0.0.0.00000000}.{volume}", "Speakers", false), (volume.DeviceId, volume.DeviceName, volume.AllowControl));
        var music = Assert.IsType<MusicConfig>(configs[23]);
        Assert.Equal(MusicHideWhenIdle.NotPlaying, music.HideWhenIdle);
        Assert.Equal(AudioVisualizerStyle.Waveform, Assert.IsType<AudioVisualizerConfig>(configs[24]).Style);
    }
}
