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
/// context; this pins that the file still loads with no repair, reads the same values and writes back
/// byte for byte.
/// </summary>
public sealed class SystemStateFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-system-configured.json"));

    [Fact]
    public void S44_the_system_widgets_configured_state_loads_without_a_repair_and_writes_back_unchanged()
    {
        var json = Fixture();
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);
        var written = JsonSerializer.Serialize(parsed.State, StateJson.AppState);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(written)), "the system widgets' state does not write back as it was read");
    }

    [Fact]
    public void S44_the_system_widgets_configured_state_reads_the_choices_it_holds()
    {
        var state = AppStateStore.Parse(Fixture()).State!;
        var configs = Assert.Single(state.Views).Widgets.Select(w => w.Config).ToList();
        Assert.Equal(21, configs.Count);

        var cpu = Assert.IsType<CpuConfig>(configs[0]);
        Assert.Equal((CpuStyle.CoreGraphs, 300), (cpu.Style, cpu.GraphSpanSeconds));
        var memory = Assert.IsType<MemoryConfig>(configs[1]);
        Assert.Equal((MemoryStyle.Bar, 900), (memory.Style, memory.GraphSpanSeconds));
        var battery = Assert.IsType<BatteryConfig>(configs[2]);
        Assert.Equal((BatteryStyle.Ring, 1800), (battery.Style, battery.GraphSpanSeconds));
        var disk = Assert.IsType<DiskConfig>(configs[3]);
        Assert.Equal((DiskStyle.Bars, DiskScope.OneDrive, "D:\\", 300), (disk.Style, disk.Scope, disk.Drive, disk.GraphSpanSeconds));
        var network = Assert.IsType<NetworkConfig>(configs[4]);
        Assert.Equal((NetworkStyle.Numbers, 3600), (network.Style, network.GraphSpanSeconds));
        var ping = Assert.IsType<PingConfig>(configs[5]);
        Assert.Equal((PingStyle.Signal, "example.com", 300), (ping.Style, ping.Host, ping.GraphSpanSeconds));
        var gpu = Assert.IsType<GpuConfig>(configs[6]);
        Assert.Equal((GpuStyle.Engines, 1, 900), (gpu.Style, gpu.Adapter, gpu.GraphSpanSeconds));

        Assert.IsType<CpuDetailsConfig>(configs[7]);
        Assert.IsType<MemoryDetailsConfig>(configs[8]);
        var processes = Assert.IsType<TopProcessesConfig>(configs[9]);
        Assert.Equal((ProcessSort.Memory, 10, false), (processes.SortBy, processes.MaxRows, processes.GroupByName));
        Assert.True(Assert.IsType<NetworkDetailsConfig>(configs[10]).ShowPublicAddress);
        Assert.IsType<BatteryDetailsConfig>(configs[11]);
        var pingDetails = Assert.IsType<PingDetailsConfig>(configs[12]);
        Assert.Equal(("9.9.9.9", 120), (pingDetails.Host, pingDetails.Window));
        Assert.Equal(1, Assert.IsType<GpuDetailsConfig>(configs[13]).Adapter);

        var drive = Assert.IsType<DriveConfig>(configs[14]);
        Assert.Equal((DriveStyle.Ring, "E:\\"), (drive.Style, drive.Drive));
        Assert.IsType<RemovableDrivesConfig>(configs[15]);
        Assert.Equal(BrightnessScreens.ThisDisplay, Assert.IsType<BrightnessConfig>(configs[16]).Screens);
        var mixer = Assert.IsType<MixerConfig>(configs[17]);
        Assert.Equal(("{0.0.0.00000000}.{mixer}", "Headphones", false, true, true, false), (mixer.DeviceId, mixer.DeviceName, mixer.GroupByApp, mixer.ShowSystemSounds, mixer.OnlyActive, mixer.ShowIcons));
        var volume = Assert.IsType<VolumeConfig>(configs[18]);
        Assert.Equal(("{0.0.0.00000000}.{volume}", "Speakers", false), (volume.DeviceId, volume.DeviceName, volume.AllowControl));
        var music = Assert.IsType<MusicConfig>(configs[19]);
        Assert.Equal((MusicStyle.Text, false, false, MusicHideWhenIdle.NotPlaying), (music.Style, music.ShowTransport, music.ShowProgress, music.HideWhenIdle));
        Assert.Equal(AudioVisualizerStyle.Waveform, Assert.IsType<AudioVisualizerConfig>(configs[20]).Style);
    }
}
