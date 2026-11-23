using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-11850] The drive lists' sample: two disks, two sticks and two mapped shares, never this PC's (each list shows its kind).</summary>
internal sealed partial class DrivesView : IWidgetSample
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    /// <summary>The sample's drives, by letter.</summary>
    private static IReadOnlyList<DriveEntry> SampleDrives() =>
    [
        new("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb),
        new("D:\\", VolumeKind.Fixed, DriveState.Ready, "Games", "NTFS", 1863 * Gb, 92 * Gb),
        new("E:\\", VolumeKind.Removable, DriveState.Ready, "STICK", "exFAT", 58 * Gb, 31 * Gb),
        new("F:\\", VolumeKind.Removable, DriveState.Ready, string.Empty, "FAT32", 30 * Gb, 2 * Gb),
        new("W:\\", VolumeKind.Network, DriveState.Ready, string.Empty, "NTFS", 1863 * Gb, 211 * Gb, @"\\server\projects"),
        new("Z:\\", VolumeKind.Network, DriveState.Ready, string.Empty, "NTFS", 3726 * Gb, 1201 * Gb, @"\\nas\media"),
    ];

    public Task ShowSampleAsync()
    {
        Hold(SampleDrives());
        return Task.CompletedTask;
    }
}
