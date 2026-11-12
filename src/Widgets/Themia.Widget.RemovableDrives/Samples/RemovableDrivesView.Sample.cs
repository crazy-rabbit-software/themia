using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-11850] The removable drives' sample: one stick plugged in, never this PC's devices.</summary>
internal sealed partial class RemovableDrivesView : IWidgetSample
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    /// <summary>The sample's stick.</summary>
    private static DriveEntry SampleStick() => new("E:\\", VolumeKind.Removable, DriveState.Ready, "STICK", "exFAT", 58 * Gb, 31 * Gb);

    public Task ShowSampleAsync()
    {
        _previewDevices = [SampleStick()];
        Show();
        return Task.CompletedTask;
    }
}
