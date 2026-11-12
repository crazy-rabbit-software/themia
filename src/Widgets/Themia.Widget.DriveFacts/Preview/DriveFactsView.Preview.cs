#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.Widget.DriveFacts;

/// <summary>Debug renders only: a system disk (the sample, <see cref="ShowSampleAsync"/>), a mapped share and a drive that is gone, as the Drive reading's states.</summary>
internal sealed partial class DriveFactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["local", "network", "missing"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "local")
        {
            return ShowSampleAsync();
        }

        HoldSample(state == "network"
            ? new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Ready, string.Empty, "NTFS", 3726 * Gb, 1201 * Gb, @"\\nas\media")
            : new DriveEntry("Q:\\", VolumeKind.Unknown, DriveState.Missing));
        return Task.CompletedTask;
    }
}
#endif
