using Themia.App.Widgets;
using Themia.Core.Widgets;

namespace Themia.Widget.DriveFacts;

/// <summary>[S-11850] The drive facts' sample: a made-up system disk, as the Drive reading's.</summary>
internal sealed partial class DriveFactsView : IWidgetSample
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    public Task ShowSampleAsync()
    {
        HoldSample(new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb));
        return Task.CompletedTask;
    }

    /// <summary>Show <paramref name="drive"/> in place of the walk's.</summary>
    internal void HoldSample(DriveEntry drive)
    {
        _previewDrive = drive;
        Show();
    }
}
