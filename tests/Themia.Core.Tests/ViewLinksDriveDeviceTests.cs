using Themia.Core.Links;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11952] A disk atom's drive on the links; [S-11957] a Bluetooth device by its endpoint id.</summary>
public sealed class ViewLinksDriveDeviceTests
{
    private sealed class Disk : IDiskScope
    {
        public DiskScope Scope { get; set; }

        public string Drive { get; set; } = string.Empty;
    }

    [Theory]
    [InlineData(@"D:\", "D:")]
    [InlineData("e:", "E:")]
    [InlineData("", "C:")]
    public void S11952_one_drive_is_its_drive_and_none_chosen_the_system_drive(string drive, string key)
        => Assert.Equal(new DriveLink(key), DiskScopes.DriveLink(new Disk { Drive = drive }, @"C:\"));

    [Fact]
    public void S11952_every_drive_together_is_no_drive()
        => Assert.Null(DiskScopes.DriveLink(new Disk { Scope = DiskScope.AllDrives, Drive = @"D:\" }, @"C:\"));

    [Fact]
    public void S11952_a_disk_atom_and_a_drive_card_of_one_drive_meet()
        => Assert.Equal(LinkKeys.DriveLink(@"d:\Photos"), DiskScopes.DriveLink(new Disk { Drive = @"D:\" }, @"C:\"));

    [Theory]
    [InlineData("Bluetooth#Bluetoothe0:d4:e8:00:00:01-a4:c1:38:0b:22:9f", "A4C1380B229F")]
    [InlineData("BluetoothLE#BluetoothLEe0:d4:e8:00:00:01-A4:C1:38:0B:22:9F", "A4C1380B229F")]
    public void S11957_an_endpoint_id_is_its_device_by_the_address_after_the_last_dash(string id, string key)
        => Assert.Equal(new DeviceLink(key), LinkKeys.DeviceOfEndpoint(id));

    [Theory]
    [InlineData("container:6b1c2d4e-0000-4000-8000-000000000003")]
    [InlineData("controller:{0f1e2d3c-0000-4000-8000-000000000001}")]
    [InlineData(@"BTHENUM\{0000110b-0000-1000-8000-00805f9b34fb}_LOCALMFG&0002\7&1a2b3c4d&0&A4C1380B229F_C00000000")]
    [InlineData("Bluetooth#Bluetoothe0:d4:e8:00:00:01-")]
    [InlineData("Bluetooth#Bluetoothe0:d4:e8:00:00:01-headset")]
    [InlineData("Surface Headphones")]
    [InlineData("")]
    [InlineData(null)]
    public void S11957_any_other_id_names_no_device(string? id)
        => Assert.Null(LinkKeys.DeviceOfEndpoint(id));

    [Fact]
    public void S11957_the_same_device_seen_by_two_lists_is_one_key()
        => Assert.Equal(LinkKeys.DeviceLink("A4:C1:38:0B:22:9F"), LinkKeys.DeviceOfEndpoint("Bluetooth#Bluetoothe0:d4:e8:00:00:01-a4:c1:38:0b:22:9f"));
}
