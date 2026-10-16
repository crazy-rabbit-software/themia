using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-4650] What kind of drive Windows says a letter is (<c>GetDriveTypeW</c>), as a closed set.</summary>
public enum VolumeKind
{
    Unknown = 0,
    Fixed = 1,
    Removable = 2,
    Optical = 3,
    Network = 4,
    RamDisk = 5,
}

/// <summary>[S-4650] Whether a drive can be read right now.</summary>
public enum DriveState
{
    /// <summary>Mounted and answering: its space is known.</summary>
    Ready = 0,

    /// <summary>A reader with nothing in it (an empty card slot, a disc drive with no disc).</summary>
    NoMedia = 1,

    /// <summary>A network drive whose first answer is still out on a worker: neither there nor disconnected yet.</summary>
    Pending = 2,

    /// <summary>A mapped network drive Windows remembers but cannot reach: Explorer's red X.</summary>
    Disconnected = 3,

    /// <summary>A drive a widget names that is not on the machine now.</summary>
    Missing = 4,
}

/// <summary>
/// [S-4650] One drive as the last walk saw it.
/// </summary>
/// <param name="Root">Its root, <c>C:\</c>.</param>
/// <param name="Kind">What Windows says it is.</param>
/// <param name="State">Whether it can be read now.</param>
/// <param name="Label">The volume label ("System"), or empty.</param>
/// <param name="FileSystem">"NTFS", "exFAT", or empty when not known.</param>
/// <param name="Total">Its size in bytes (to this user, quotas included); 0 when not known.</param>
/// <param name="Free">What this user can still write, in bytes.</param>
/// <param name="Share">For a mapped drive, the share it is mapped to (<c>\\server\share</c>); empty otherwise.</param>
/// <param name="Hotplug">An external disk on a bus that comes and goes (USB, SD, FireWire) though Windows calls it fixed.</param>
public sealed record DriveEntry(
    string Root,
    VolumeKind Kind,
    DriveState State,
    string Label = "",
    string FileSystem = "",
    ulong Total = 0,
    ulong Free = 0,
    string Share = "",
    bool Hotplug = false)
{
    public bool Known => State == DriveState.Ready && Total > 0;

    public ulong Used => Total > Free ? Total - Free : 0;

    /// <summary>The share used, 0..1, or null when not known.</summary>
    public double? UsedFraction => Known ? (double)Used / Total : null;

    /// <summary>"C:".</summary>
    public string Letter => DiskRules.Instance(Root);
}

/// <summary>
/// [S-4650]..[S-4656] How a drive is named, described and picked, and which drives are removable:
/// pure, so the rules are tested without a disk or a network (after Karpo's <c>DriveTargets</c> and
/// <c>DriveWatch</c>). Explorer is the reference for every word: a drive is called what This PC
/// calls it, and a mapped share it cannot reach is a "Disconnected network drive".
/// </summary>
public static class DriveRules
{
    /// <summary>WNetGetConnectionW's ERROR_CONNECTION_UNAVAIL: a connection Windows remembers but has not made.</summary>
    public const uint ConnectionUnavailable = 1201;

    /// <summary>[S-4651] What Explorer calls a drive without a label, by kind.</summary>
    public static string PlainLabel(Locale locale, VolumeKind kind) => Strings.Get(locale, kind switch
    {
        VolumeKind.Removable => Str.WidgetsB_Disk_UsbDrive,
        VolumeKind.Optical => Str.Drives_Label_Cd,
        VolumeKind.Network => Str.Drives_Label_Network,
        VolumeKind.RamDisk => Str.Drives_Label_Ram,
        _ => Str.WidgetsB_Disk_LocalDisk,
    });

    /// <summary>[S-4651] "share (\\server)" for a drive mapped to \\server\share, as Explorer names it; the share itself where it has no server part.</summary>
    public static string ShareLabel(string share)
    {
        var trimmed = (share ?? string.Empty).TrimEnd('\\');
        var cut = trimmed.LastIndexOf('\\');
        return cut > 2 && cut < trimmed.Length - 1 ? $"{trimmed[(cut + 1)..]} ({trimmed[..cut]})" : trimmed;
    }

    /// <summary>[S-4651] The drive's name as This PC shows it: "System (C:)", "USB Drive (E:)", "media (\\nas) (Z:)".</summary>
    public static string Title(Locale locale, DriveEntry drive)
    {
        ArgumentNullException.ThrowIfNull(drive);
        var name = drive.Label.Trim();

        if (name.Length == 0 && drive.Kind == VolumeKind.Network && drive.Share.Length > 0)
        {
            name = ShareLabel(drive.Share);
        }

        if (name.Length == 0)
        {
            name = PlainLabel(locale, drive.Kind);
        }

        return Strings.Format(locale, Str.WidgetsB_Disk_Title, ("name", name), ("letter", drive.Letter));
    }

    /// <summary>
    /// [S-4652] The kind as Explorer's Type column says it, with the file system after it where known:
    /// "Local Disk · NTFS", "Removable Disk · exFAT", "Network Drive", "Disconnected network drive".
    /// </summary>
    public static string Description(Locale locale, DriveEntry drive)
    {
        ArgumentNullException.ThrowIfNull(drive);

        var type = drive.State == DriveState.Disconnected
            ? Strings.Get(locale, Str.Drives_Type_Disconnected)
            : Strings.Get(locale, drive.Kind switch
            {
                VolumeKind.Removable => Str.Drives_Type_Removable,
                VolumeKind.Optical => Str.Drives_Type_Optical,
                VolumeKind.Network => Str.Drives_Type_Network,
                VolumeKind.RamDisk => Str.Drives_Label_Ram,
                _ => Str.WidgetsB_Disk_LocalDisk,
            });

        return drive.FileSystem.Length > 0 && drive.State == DriveState.Ready ? $"{type} · {drive.FileSystem}" : type;
    }

    /// <summary>
    /// [S-4653] The line under the bar: "419 GB free of 931 GB"; or why there are no figures —
    /// "No disc", "Connecting…", "Disconnected", "Not connected".
    /// </summary>
    public static string SpaceLine(Locale locale, DriveEntry drive)
    {
        ArgumentNullException.ThrowIfNull(drive);

        return drive.State switch
        {
            DriveState.Ready when drive.Total > 0 => Strings.Format(locale, Str.Details_Disk_FreeOf,
                ("free", Formatting.Bytes(locale, drive.Free)),
                ("total", Formatting.Bytes(locale, drive.Total))),
            DriveState.NoMedia => Strings.Get(locale, Str.Drives_NoMedia),
            DriveState.Pending => Strings.Get(locale, Str.Drives_Pending),
            DriveState.Disconnected => Strings.Get(locale, Str.Drives_Disconnected),
            _ => Strings.Get(locale, Str.Details_Disk_NotConnected),
        };
    }

    /// <summary>[S-4653] The bar's colour: a load (caution from 70 %, critical from 90 %), as Explorer turns a nearly full drive red.</summary>
    public static MeterPressure Pressure(DriveEntry drive) =>
        drive?.UsedFraction is { } used ? MeterPressures.For(PressureScale.Usage, used) : MeterPressure.Normal;

    /// <summary>
    /// [S-4654] A network drive's state from what <c>WNetGetConnectionW</c> said of it and whether its
    /// space could be read: a connection Windows remembers but has not made is disconnected, and
    /// <paramref name="space"/> is never asked — reading the share would try to reconnect, which
    /// Explorer leaves to the person opening the drive. Any other is there when its space reads.
    /// </summary>
    public static DriveState NetworkState(uint status, Func<bool> space)
    {
        ArgumentNullException.ThrowIfNull(space);
        return status == ConnectionUnavailable ? DriveState.Disconnected : space() ? DriveState.Ready : DriveState.Disconnected;
    }

    /// <summary>
    /// [S-4660] Whether a drive belongs on the removable devices list: a stick or a card (removable), a
    /// disc in its drive (optical), or an external disk on a bus that comes and goes. An empty reader
    /// is left out, as Explorer hides empty drives by default.
    /// </summary>
    public static bool IsRemovable(DriveEntry drive) =>
        drive is not null
        && drive.State == DriveState.Ready
        && (drive.Kind is VolumeKind.Removable or VolumeKind.Optical || (drive.Kind == VolumeKind.Fixed && drive.Hotplug));

    /// <summary>[S-4660] The removable devices in <paramref name="drives"/>, by letter.</summary>
    public static IReadOnlyList<DriveEntry> Removables(IEnumerable<DriveEntry> drives) =>
        [.. (drives ?? []).Where(IsRemovable).OrderBy(d => d.Root, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// [S-4655] The local volumes of a walk, as the disk meter lists them: the fixed and
    /// removable drives that are there (not an empty reader, a disc, a share or a RAM disk), in walk order.
    /// </summary>
    public static IReadOnlyList<DiskVolume> LocalVolumes(IEnumerable<DriveEntry> drives) =>
        [.. (drives ?? [])
            .Where(d => d.State == DriveState.Ready && d.Kind is VolumeKind.Fixed or VolumeKind.Removable)
            .Select(d => new DiskVolume(d.Root, d.Label, d.Kind == VolumeKind.Removable))];

    /// <summary>
    /// [S-4670] The drive a Drive widget shows: <paramref name="root"/>'s (empty: the system drive,
    /// <paramref name="systemRoot"/>), else a <see cref="DriveState.Missing"/> one at that letter, so
    /// the widget keeps its place and says it is not connected.
    /// </summary>
    public static DriveEntry Pick(IEnumerable<DriveEntry> drives, string? root, string systemRoot)
    {
        var wanted = DiskRules.Root(root) ?? DiskRules.Root(systemRoot) ?? "C:\\";

        foreach (var drive in drives ?? [])
        {
            if (string.Equals(drive.Root, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return drive;
            }
        }

        return new DriveEntry(wanted, VolumeKind.Unknown, DriveState.Missing);
    }

    /// <summary>
    /// [S-4656] Whether two walks show the same thing, so a read that changed nothing redraws nothing.
    /// Free space is compared to the megabyte: a log file growing does not repaint every drive widget.
    /// </summary>
    public static bool SameAs(IReadOnlyList<DriveEntry> a, IReadOnlyList<DriveEntry> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            var (x, y) = (a[i], b[i]);
            if (x with { Free = x.Free >> 20 } != y with { Free = y.Free >> 20 })
            {
                return false;
            }
        }

        return true;
    }
}
