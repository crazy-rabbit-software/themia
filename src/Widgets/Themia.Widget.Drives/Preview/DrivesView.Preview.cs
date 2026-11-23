using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: the sample (<see cref="ShowSampleAsync"/>); every kind and state (a disc, an external disk, a
/// share disconnected and one still answering); or no drive to show. The gap and a filter are the config's
/// (<c>--config {"gap":true}</c>, <c>--config {"hidden":["D:\\"]}</c>).
/// </summary>
internal sealed partial class DrivesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "devices", "none"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        Hold(state switch
        {
            "devices" =>
            [
                .. SampleDrives(),
                new DriveEntry("G:\\", VolumeKind.Fixed, DriveState.Ready, "Backup", "NTFS", 1863 * Gb, 640 * Gb, Hotplug: true),
                new DriveEntry("H:\\", VolumeKind.Optical, DriveState.Ready, "HOLIDAY_2025", "UDF", 4 * Gb, 0),
                new DriveEntry("Y:\\", VolumeKind.Network, DriveState.Pending, Share: @"\\server\projects"),
                new DriveEntry("X:\\", VolumeKind.Network, DriveState.Disconnected, Share: @"\\nas\backup"),
            ],
            _ => [],
        });
        return Task.CompletedTask;
    }
}
