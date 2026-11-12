using Themia.Core.Links;
using Themia.Modules.Tests.Modules;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-11950] [S-11955] The drive and the Bluetooth device under the pointer: who points at one and who lights for one.</summary>
public sealed class ViewLinksDriveKindsTests
{
    [Theory]
    [InlineData("drive")]
    [InlineData("driveFacts")]
    [InlineData("disk")]
    [InlineData("diskHistory")]
    [InlineData("removableDrives")]
    [InlineData("folder")]
    public void S11950_a_drive_card_a_disk_atom_a_removable_row_and_a_folder_point_at_a_drive_and_light_for_one(string id)
    {
        var links = Links(id);

        Assert.Contains(LinkTopicId.HoveredDrive, links.Publishes);
        Assert.Contains(LinkTopicId.HoveredDrive, links.Follows);
    }

    [Fact]
    public void S11953_the_disk_history_publishes_the_moment_and_the_drive_both()
        => Assert.Equal([LinkTopicId.HoveredInstant, LinkTopicId.HoveredDrive], Links("diskHistory").Publishes);

    [Theory]
    [InlineData("bluetooth")]
    [InlineData("deviceBatteries")]
    public void S11955_a_bluetooth_row_and_a_battery_row_point_at_a_device_and_light_for_one(string id)
    {
        var links = Links(id);

        Assert.Contains(LinkTopicId.HoveredDevice, links.Publishes);
        Assert.Contains(LinkTopicId.HoveredDevice, links.Follows);
    }

    private static LinkUse Links(string id) => ModuleRegistry.Widgets.Single(m => m.Descriptor.Id.Value == id).Descriptor.Links;
}
