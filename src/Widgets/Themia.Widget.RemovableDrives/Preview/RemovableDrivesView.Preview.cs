#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only: one stick, three devices of each kind, or nothing plugged in.</summary>
internal sealed partial class RemovableDrivesView : IWidgetPreviewState
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    public IReadOnlyList<string> PreviewStates { get; } = ["one", "devices", "none"];

    public Task ShowPreviewStateAsync(string state)
    {
        var stick = new DriveEntry("E:\\", VolumeKind.Removable, DriveState.Ready, "STICK", "exFAT", 58 * Gb, 31 * Gb);
        _previewDevices = state switch
        {
            "one" => [stick],
            "devices" =>
            [
                stick,
                new DriveEntry("F:\\", VolumeKind.Removable, DriveState.Ready, string.Empty, "FAT32", 30 * Gb, 2 * Gb),
                new DriveEntry("G:\\", VolumeKind.Fixed, DriveState.Ready, "Backup", "NTFS", 1863 * Gb, 640 * Gb, Hotplug: true),
                new DriveEntry("H:\\", VolumeKind.Optical, DriveState.Ready, "HOLIDAY_2025", "UDF", 4 * Gb, 0),
            ],
            _ => [],
        };

        Show();
        return Task.CompletedTask;
    }
}
#endif
