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
/// context; this pins that the file loads with nothing repaired (its facts as the single metrics they are now), reads the
/// same values, and that what it writes back loads again with no repair and writes back byte for byte.
/// </summary>
public sealed class SystemStateFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-system-configured.json"));

    [Fact]
    public void S44_the_system_widgets_configured_state_loads_as_it_is_and_writes_back_unchanged()
    {
        var parsed = StateDocument.Parse(Fixture());

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);

        var json = JsonSerializer.Serialize(parsed.State, StateJson.AppState);
        var again = StateDocument.Parse(json);
        Assert.Empty(again.Repairs);
        var written = JsonSerializer.Serialize(again.State!, StateJson.AppState);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(written)), "the system widgets' state does not write back as it was read");
    }

    [Fact]
    public void S44_the_system_widgets_configured_state_reads_the_choices_it_holds()
    {
        var state = StateDocument.Parse(Fixture()).State!;
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

        // The facts are single metrics, each with the settings of its own (the host and the window, the adapter).
        Assert.Equal(("9.9.9.9", 120), (Assert.IsType<Themia.Widget.PingFacts.PingLossConfig>(configs[15]).Host, Assert.IsType<Themia.Widget.PingFacts.PingLossConfig>(configs[15]).Window));
        Assert.Equal(1, Assert.IsType<Themia.Widget.GpuFacts.GpuTemperatureConfig>(configs[17]).Adapter);
        var processes = Assert.IsType<TopProcessesConfig>(configs[11]);
        Assert.Equal((ProcessSort.Memory, 10, false), (processes.SortBy, processes.Rows, processes.GroupByName));
        Assert.True(Assert.IsType<Themia.Widget.NetworkFacts.NetworkPublicAddressConfig>(configs[12]).ShowsPublicAddress);

        var drive = Assert.IsType<DriveConfig>(configs[18]);
        Assert.Equal((DriveStyle.Ring, "E:\\"), (drive.Style, drive.Drive));
        // [S-16656] The removable drives list became the USB sticks' list in place, its rows kept.
        Assert.Equal(3, Assert.IsType<UsbSticksConfig>(configs[19]).Rows);
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
