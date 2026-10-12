#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only: a system disk, a nearly full one, a USB stick, a mapped share connected, disconnected and still answering.</summary>
internal sealed partial class DriveView : IWidgetPreviewState
{
    private const ulong Gb = 1024UL * 1024 * 1024;
    private const string Share = @"\\nas\media";
    private const string Projects = @"\\server\projects";

    public IReadOnlyList<string> PreviewStates { get; } = ["local", "full", "usb", "network", "disconnected", "pending", "missing"];

    public Task ShowPreviewStateAsync(string state)
    {
        _previewDrive = state switch
        {
            "local" => new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb),
            "full" => new DriveEntry("D:\\", VolumeKind.Fixed, DriveState.Ready, "Games", "NTFS", 1863 * Gb, 92 * Gb),
            "usb" => new DriveEntry("E:\\", VolumeKind.Removable, DriveState.Ready, "STICK", "exFAT", 58 * Gb, 31 * Gb),
            "network" => new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Ready, string.Empty, "NTFS", 3726 * Gb, 1201 * Gb, Share),
            "disconnected" => new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected, Share: Share),
            "pending" => new DriveEntry("Y:\\", VolumeKind.Network, DriveState.Pending, Share: Projects),
            _ => new DriveEntry("Q:\\", VolumeKind.Unknown, DriveState.Missing),
        };

        Show();
        return Task.CompletedTask;
    }
}
#endif
