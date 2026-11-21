using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.DriveFacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("driveActivity", typeof(DriveActivityModule))]
[assembly: ThemiaWidget("driveUsed", typeof(DriveUsedModule))]
[assembly: ThemiaWidget("driveFree", typeof(DriveFreeModule))]
[assembly: ThemiaWidget("driveCapacity", typeof(DriveCapacityModule))]
[assembly: ThemiaWidget("driveFileSystem", typeof(DriveFileSystemModule))]
[assembly: ThemiaWidget("driveType", typeof(DriveTypeModule))]

namespace Themia.Widget.DriveFacts;

/// <summary>
/// [S-14670] [S-14604] The family's kinds: one drive's facts, one widget a fact (owner, 2026-11-20: "split all the other widgets
/// that are alike to the weather and air facts into individual metrics too"), in the removed card's order: each one's
/// id, the metric's key, its glyph, its history (its trends), its row's name, its name and description on the add page,
/// its search words and its config.
/// </summary>
internal static class DriveKinds
{
    public static IReadOnlyList<MetricKind> All { get; } = MetricKind.Opening(
    [
        // [S-14714] Its reads and writes, the disk history's metric: the hub's series of the drive, its graph standing in for
        // the history ([S-14713]), a rate's fold its peak.
        new MetricKind("driveActivity", "activity", Glyphs.Disk, MetricHistory.Series, DriveFactsStr.DriveFacts_DriveActivity_Label.Local(), DriveFactsStr.DriveFacts_DriveActivity_Label.Local(), DriveFactsStr.DriveFacts_DriveActivity_Desc.Local(),
            ["drive", "disk", "activity", "read", "write", "throughput", "graph", "history"], () => new DriveActivityConfig())
            .Graphed(MetricGraph.Hub(300, peak: true), new MergeFrom("diskHistory")),
        new("driveUsed", "used", Glyphs.Disk, MetricHistory.Readings, Str.Details_Drive_Used.Local(), DriveFactsStr.DriveFacts_DriveUsed_Label.Local(), DriveFactsStr.DriveFacts_DriveUsed_Desc.Local(),
            ["drive", "disk", "used", "storage"], () => new DriveUsedConfig()),
        new("driveFree", "free", Glyphs.Disk, MetricHistory.Readings, Str.Details_Drive_Free.Local(), DriveFactsStr.DriveFacts_DriveFree_Label.Local(), DriveFactsStr.DriveFacts_DriveFree_Desc.Local(),
            ["drive", "disk", "free space", "storage", "room"], () => new DriveFreeConfig()),
        new("driveCapacity", "capacity", Glyphs.Disk, MetricHistory.None, Str.Details_Drive_Capacity.Local(), DriveFactsStr.DriveFacts_DriveCapacity_Label.Local(), DriveFactsStr.DriveFacts_DriveCapacity_Desc.Local(),
            ["drive", "disk", "capacity", "size", "storage"], () => new DriveCapacityConfig()),
        new("driveFileSystem", "fileSystem", Glyphs.Disk, MetricHistory.None, Str.Details_Drive_FileSystem.Local(), DriveFactsStr.DriveFacts_DriveFileSystem_Label.Local(), DriveFactsStr.DriveFacts_DriveFileSystem_Desc.Local(),
            ["drive", "disk", "file system", "ntfs", "exfat", "fat32"], () => new DriveFileSystemConfig()),
        new("driveType", "type", Glyphs.Disk, MetricHistory.None, Str.Details_Drive_Type.Local(), DriveFactsStr.DriveFacts_DriveType_Label.Local(), DriveFactsStr.DriveFacts_DriveType_Desc.Local(),
            ["drive", "disk", "type", "usb", "network drive", "ssd"], () => new DriveTypeConfig()),
    ], MetricOpens.Drive);

    /// <summary>The kind showing the metric <paramref name="key"/>.</summary>
    public static MetricKind Of(string key) => All.Single(k => k.Key == key);

    /// <summary>Its place in the family's order: its add-page rank.</summary>
    public static int Rank(string key) => All.ToList().FindIndex(k => k.Key == key);
}

// [S-14604] Each kind is its module and its config (a config's type is how it knows its id, [S-6902]); everything
// else is the family's (DriveMetricModule, DriveMetricConfig, the view) over the single metric's base.

public sealed class DriveActivityModule() : DriveMetricModule("activity");

public sealed class DriveUsedModule() : DriveMetricModule("used");

public sealed class DriveFreeModule() : DriveMetricModule("free");

public sealed class DriveCapacityModule() : DriveMetricModule("capacity");

public sealed class DriveFileSystemModule() : DriveMetricModule("fileSystem");

public sealed class DriveTypeModule() : DriveMetricModule("type");

/// <summary>[S-14714] The activity alone reads the hub: its drive's series (the system drive while none is chosen), as a disk meter's.</summary>
public sealed class DriveActivityConfig() : DriveMetricConfig("activity"), IReadsSystem
{
    /// <summary>[S-2513] The drive's activity series.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.DiskActivity, DiskRules.Root(Drive) ?? systemRoot);
}

public sealed class DriveUsedConfig() : DriveMetricConfig("used");

public sealed class DriveFreeConfig() : DriveMetricConfig("free");

public sealed class DriveCapacityConfig() : DriveMetricConfig("capacity");

public sealed class DriveFileSystemConfig() : DriveMetricConfig("fileSystem");

public sealed class DriveTypeConfig() : DriveMetricConfig("type");
