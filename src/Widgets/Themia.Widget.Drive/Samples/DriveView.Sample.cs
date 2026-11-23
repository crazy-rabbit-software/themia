using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-11850] The drive's sample: a made-up system disk (a stick, a share for those kinds), never one of this PC's.</summary>
internal sealed partial class DriveView : IWidgetSample
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    public Task ShowSampleAsync()
    {
        // [S-16646] Each kind its own: the system disk, a stick, a mapped share.
        HoldSample(_kind.Group switch
        {
            DriveGroup.External => new DriveEntry("E:\\", VolumeKind.Removable, DriveState.Ready, "STICK", "exFAT", 58 * Gb, 31 * Gb),
            DriveGroup.Network => new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Ready, string.Empty, "NTFS", 3726 * Gb, 1201 * Gb, @"\\nas\media"),
            _ => new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb),
        });
        return Task.CompletedTask;
    }

    /// <summary>Show <paramref name="drive"/> in place of the walk's.</summary>
    internal void HoldSample(DriveEntry drive)
    {
        _previewDrive = drive;
        Show();
    }
}
