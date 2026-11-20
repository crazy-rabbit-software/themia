using Themia.Core.Widgets;

namespace Themia.Widget.DriveFacts;

/// <summary>
/// Debug renders only: a system disk (the sample, <see cref="ShowSampleAsync"/>), a mapped share and a drive that is
/// gone, as the Drive reading's states; the base's <c>arrow</c> and <c>line</c> draw the sample with each trend ([S-14605]).
/// </summary>
internal sealed partial class DriveMetricView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample", "local", "network", "missing"];

    protected override Task ShowOwnPreviewStateAsync(string state)
    {
        if (state is "sample" or "local")
        {
            return ShowSampleAsync();
        }

        HoldSample(state == "network"
            ? new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Ready, string.Empty, "NTFS", 3726 * Gb, 1201 * Gb, @"\nas\media")
            : new DriveEntry("Q:\\", VolumeKind.Unknown, DriveState.Missing));
        return Task.CompletedTask;
    }
}
