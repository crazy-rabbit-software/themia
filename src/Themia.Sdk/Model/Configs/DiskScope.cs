using Themia.Core.Links;
using Themia.Core.SystemData;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-1822] Whose throughput the disk meter shows.</summary>
public enum DiskScope
{
    /// <summary>One drive: <see cref="IDiskScope.Drive"/> (empty = the system drive).</summary>
    OneDrive = 0,

    /// <summary>Every drive together.</summary>
    AllDrives = 1,
}

/// <summary>[S-1160] [S-1822] A disk atom's config: one drive (empty: the system drive) or every drive together.</summary>
public interface IDiskScope
{
    DiskScope Scope { get; set; }

    /// <summary>The drive's root (<c>C:\</c>); empty for the system drive.</summary>
    string Drive { get; set; }
}

/// <summary>[S-1160] [S-1822] [S-11000] The disk atoms' one rule for their series and their repair, so the reading and the history read the same drive.</summary>
public static class DiskScopes
{
    /// <summary>[S-1160] [S-1822] The series instance: every drive together, or its drive (the system drive when none is chosen).</summary>
    public static string Instance(IDiskScope disk, string systemRoot)
    {
        ArgumentNullException.ThrowIfNull(disk);
        return disk.Scope == DiskScope.AllDrives ? SystemInterests.AllDrives : DiskRules.Root(disk.Drive) ?? systemRoot;
    }

    /// <summary>
    /// [S-11952] The drive a disk atom is on its view's links: its drive (the system drive when none is chosen),
    /// keyed as every drive widget keys one (<see cref="LinkKeys.Drive"/>); every drive together is no one drive
    /// and takes no part.
    /// </summary>
    public static DriveLink? DriveLink(IDiskScope disk, string systemRoot)
    {
        ArgumentNullException.ThrowIfNull(disk);
        return disk.Scope == DiskScope.AllDrives ? null : LinkKeys.DriveLink(DiskRules.Root(disk.Drive) ?? systemRoot);
    }

    /// <summary>[S-2513] The disk activity of its series.</summary>
    public static SystemInterests Wants(IDiskScope disk, string systemRoot)
        => SystemInterests.Chart(SystemSource.DiskActivity, Instance(disk, systemRoot));

    /// <summary>[S-1160] [S-1822] Keep the drive as a root; one that is not a drive letter, or an unknown scope, is repaired loudly.</summary>
    public static IReadOnlyList<string> Repair(IDiskScope disk, string kind)
    {
        ArgumentNullException.ThrowIfNull(disk);
        var notes = new List<string>();

        disk.Drive ??= string.Empty;

        if (disk.Drive.Length > 0)
        {
            if (DiskRules.Root(disk.Drive) is { } root)
            {
                disk.Drive = root;
            }
            else
            {
                notes.Add($"{kind}: '{disk.Drive}' is not a drive letter; reporting on the system drive");
                disk.Drive = string.Empty;
            }
        }

        if (!Enum.IsDefined(disk.Scope))
        {
            notes.Add($"{kind}: scope {(int)disk.Scope} is unknown; reporting on one drive");
            disk.Scope = DiskScope.OneDrive;
        }

        return notes;
    }
}
