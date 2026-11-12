#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.Widget.DriveFacts;

/// <summary>Debug renders only: a system disk, a mapped share and a drive that is gone, as the Drive reading's states.</summary>
internal sealed partial class DriveFactsView : IWidgetPreviewState
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    public IReadOnlyList<string> PreviewStates { get; } = ["local", "network", "missing"];

    public Task ShowPreviewStateAsync(string state)
    {
        _previewDrive = state switch
        {
            "local" => new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb),
            "network" => new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Ready, string.Empty, "NTFS", 3726 * Gb, 1201 * Gb, @"\\nas\media"),
            _ => new DriveEntry("Q:\\", VolumeKind.Unknown, DriveState.Missing),
        };

        Show();
        return Task.CompletedTask;
    }
}
#endif
