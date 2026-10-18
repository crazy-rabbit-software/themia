using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-1221] The mixer's and the volume tile's device choice side by side (the blank id repaired, the clone keeping it): the configs are their modules', so they run here. Moved from Core's AudioRulesTests, unchanged in what they assert.</summary>
public sealed class AudioHostTests
{

    [Fact]
    public void S1221_a_blank_device_id_is_repaired_to_follow_the_default()
    {
        var mixer = new MixerConfig { DeviceId = " ", DeviceName = "x" };
        Assert.NotEmpty(mixer.Repair());
        Assert.Null(mixer.DeviceId);
        Assert.Null(mixer.DeviceName);

        var volume = new VolumeConfig { DeviceId = "{a}", DeviceName = "Speakers" };
        Assert.Empty(volume.Repair());
        Assert.Equal("{a}", volume.DeviceId);
    }

    [Fact]
    public void S1221_clone_keeps_the_device_and_the_new_toggles()
    {
        var clone = (VolumeConfig)new VolumeConfig { DeviceId = "{a}", DeviceName = "S", AllowControl = false }.Clone();
        Assert.Equal(("{a}", "S", false), (clone.DeviceId, clone.DeviceName, clone.AllowControl));

        var mixer = (MixerConfig)new MixerConfig { DeviceId = "{b}", ShowIcons = false }.Clone();
        Assert.Equal(("{b}", false), (mixer.DeviceId, mixer.ShowIcons));
    }
}
