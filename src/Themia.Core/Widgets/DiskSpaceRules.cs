using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-2420] One drive's space at one read. <paramref name="Total"/> 0 means it did not answer.</summary>
public readonly record struct DriveSpace(DiskVolume Volume, ulong Total, ulong Free)
{
    public bool Known => Total > 0;

    public ulong Used => Total > Free ? Total - Free : 0;

    /// <summary>Share used, 0..1, or null when not known.</summary>
    public double? UsedFraction => Total == 0 ? null : (double)Used / Total;
}

/// <summary>[S-2420]..[S-2422] The disk space widget's rules, pure.</summary>
public static class DiskSpaceRules
{
    /// <summary>
    /// [S-2421] A drive list made safe: every entry a root (<c>C:\</c>), anything that is not a drive
    /// letter dropped, a repeat dropped, the order kept.
    /// </summary>
    public static List<string> Normalise(IEnumerable<string?> drives)
    {
        ArgumentNullException.ThrowIfNull(drives);
        var kept = new List<string>();

        foreach (var drive in drives)
        {
            if (DiskRules.Root(drive) is { } root && !kept.Contains(root, StringComparer.OrdinalIgnoreCase))
            {
                kept.Add(root);
            }
        }

        return kept;
    }

    /// <summary>
    /// [S-2421] Which drives a tile lists. An empty selection is every drive Windows lists, by letter.
    /// A selection lists those drives in its order; one that is not connected now stays in its place,
    /// as a volume with no label, so a tile does not silently lose a row.
    /// </summary>
    public static List<DiskVolume> Select(IReadOnlyList<DiskVolume> volumes, IReadOnlyList<string> selection)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.Count == 0)
        {
            return [.. volumes.OrderBy(v => v.Root, StringComparer.OrdinalIgnoreCase)];
        }

        var chosen = new List<DiskVolume>(selection.Count);

        foreach (var root in Normalise(selection))
        {
            var volume = volumes.FirstOrDefault(v => string.Equals(v.Root, root, StringComparison.OrdinalIgnoreCase));
            chosen.Add(volume.Root is null ? new DiskVolume(root, string.Empty, false) : volume);
        }

        return chosen;
    }

    /// <summary>
    /// [S-2420] [S-2422] One drive's row: the drive named as Explorer names it, the share used, "120 GB
    /// free of 476 GB" under it and a bar of the used share, coloured as a load (caution from 70%,
    /// critical from 90%, as Explorer turns a nearly full drive red). A drive that did not answer
    /// reads "--" with "Not connected" and no bar.
    /// </summary>
    public static DetailRow Row(Locale locale, DriveSpace drive)
    {
        var name = DiskRules.Title(locale, drive.Volume);

        if (drive.UsedFraction is not { } used)
        {
            return new DetailRow(drive.Volume.Root, name, "--", Detail: Strings.Get(locale, Str.Details_Disk_NotConnected));
        }

        var detail = Strings.Format(locale, Str.Details_Disk_FreeOf,
            ("free", Formatting.Bytes(locale, drive.Free)),
            ("total", Formatting.Bytes(locale, drive.Total)));

        return new DetailRow(drive.Volume.Root, name, Formatting.Percent(locale, used), Detail: detail,
            Fraction: used, Pressure: MeterPressures.For(PressureScale.Usage, used));
    }

    /// <summary>[S-2420] The sheet: a list, no hero, no facts. No drive at all says so.</summary>
    public static DetailSheet Sheet(Locale locale, IReadOnlyList<DriveSpace> drives)
    {
        ArgumentNullException.ThrowIfNull(drives);

        return drives.Count == 0
            ? new DetailSheet(null, null, [], [], Note: Strings.Get(locale, Str.Details_Disk_NoDrives))
            : new DetailSheet(null, null, [], [.. drives.Select(d => Row(locale, d))]);
    }
}
