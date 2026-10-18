using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Drives;

/// <summary>
/// [S-4650]..[S-4655] The one walk of the drives: every letter Windows has, what kind it is, its label,
/// file system and space, and for a mapped network drive whether it is connected (after Karpo's
/// <c>DriveWatch</c>). The Disk meter's volume list, Drive and Removable drives all read
/// their drives from here, so there is one way a drive is asked.
/// <para>
/// A local drive is asked on the calling thread (<c>GetVolumeInformationW</c>, <c>GetDiskFreeSpaceExW</c>,
/// with the critical-error box off so an empty card reader never pops "insert a disk"). A network
/// drive is <b>never waited for</b>: a share that has gone makes those calls wait for the network to
/// time out, tens of seconds. What the redirector remembers (<c>WNetGetConnectionW</c>, local and
/// immediate) says first: a connection Windows remembers but has not made is disconnected and its
/// share is not asked at all, as Explorer leaves the reconnect to the person opening it. Any other is
/// probed on the pool, one probe per letter at a time; until its first answer the drive is
/// <see cref="DriveState.Pending"/>, after it the last answer stands while a new one is out, and an
/// answer that changes whether it is there raises <see cref="NetworkAnswered"/>.
/// </para>
/// </summary>
internal static unsafe partial class DriveCatalog
{
    private const string Scope = "drives";

    private const uint SemFailCriticalErrors = 0x0001;
    private const uint SemNoOpenFileErrorBox = 0x8000;

    private const uint DriveRemovable = 2;
    private const uint DriveFixed = 3;
    private const uint DriveRemote = 4;
    private const uint DriveCdRom = 5;
    private const uint DriveRamDisk = 6;

    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint FileShareReadWrite = 0x3;
    private const uint OpenExisting = 3;

    // STORAGE_BUS_TYPE values of a disk that comes and goes though Windows calls it fixed.
    private const int BusType1394 = 4;
    private const int BusTypeUsb = 7;
    private const int BusTypeSd = 0xC;
    private const int BusTypeMmc = 0xD;

    private static readonly Lock Gate = new();
    private static readonly Task<DriveEntry>?[] Probes = new Task<DriveEntry>?[26];
    private static readonly DriveEntry?[] Answers = new DriveEntry?[26];

    /// <summary>
    /// A network drive's probe came back with an answer that changes whether it is there (or its first
    /// answer). Raised on a pool thread; a handler hands off to its own thread and must not throw.
    /// </summary>
    public static event Action? NetworkAnswered;

    /// <summary>The system drive's root, what a widget naming no drive shows.</summary>

    /// <summary>
    /// Every drive, by letter: local ones always, mapped network drives when <paramref name="network"/>.
    /// Never blocks on the network. Any thread.
    /// </summary>
    public static IReadOnlyList<DriveEntry> Walk(bool network)
    {
        var drives = new List<DriveEntry>(8);
        uint previous;
        var moded = SetThreadErrorMode(SemFailCriticalErrors | SemNoOpenFileErrorBox, &previous);

        try
        {
            var mask = GetLogicalDrives();

            for (var letter = 0; letter < 26; letter++)
            {
                if ((mask & (1u << letter)) == 0)
                {
                    // Gone: a drive mapped at this letter later starts fresh.
                    Forget(letter);
                    continue;
                }

                var root = $"{(char)('A' + letter)}:\\";
                uint type;
                fixed (char* name = root)
                {
                    type = GetDriveTypeW(name);
                }

                var kind = Kind(type);
                if (kind == VolumeKind.Unknown)
                {
                    continue;
                }

                if (kind == VolumeKind.Network)
                {
                    if (network)
                    {
                        drives.Add(Remote(letter, root));
                    }

                    continue;
                }

                drives.Add(Local(root, kind));
            }
        }
        catch (Exception ex)
        {
            // A walk that fails part way is the drives it found; the next walk tries again.
            Log.Error(Scope, "walking the drives failed", ex);
        }
        finally
        {
            if (moded)
            {
                // ignore: the previous mode is restored best-effort; the flags only suppress a dialog.
                _ = SetThreadErrorMode(previous, null);
            }
        }

        return drives;
    }

    private static VolumeKind Kind(uint type) => type switch
    {
        DriveFixed => VolumeKind.Fixed,
        DriveRemovable => VolumeKind.Removable,
        DriveCdRom => VolumeKind.Optical,
        DriveRemote => VolumeKind.Network,
        DriveRamDisk => VolumeKind.RamDisk,
        _ => VolumeKind.Unknown,
    };

    /// <summary>A local drive: its label and file system, its space, and for a fixed disk whether its bus comes and goes.</summary>
    private static DriveEntry Local(string root, VolumeKind kind)
    {
        if (Volume(root) is not var (label, fileSystem))
        {
            // No medium (an empty card slot, no disc): there, with nothing to read.
            return new DriveEntry(root, kind, DriveState.NoMedia);
        }

        var (total, free) = Space(root) ?? (0, 0);
        var hotplug = kind == VolumeKind.Fixed && Hotplug(root);
        return new DriveEntry(root, kind, total > 0 ? DriveState.Ready : DriveState.NoMedia, label, fileSystem, total, free, Hotplug: hotplug);
    }

    /// <summary>A mapped drive: the redirector's record first, then the last probe's answer, a probe started where none is out.</summary>
    private static DriveEntry Remote(int letter, string root)
    {
        var (status, share) = Connection(letter);

        if (status == DriveRules.ConnectionUnavailable)
        {
            lock (Gate)
            {
                Answers[letter] = null;
            }

            return new DriveEntry(root, VolumeKind.Network, DriveState.Disconnected, Share: share);
        }

        lock (Gate)
        {
            if (Probes[letter] is { IsCompleted: true } done)
            {
                // Probe does not throw, so the result is always there.
                Answers[letter] = done.Result;
                Probes[letter] = null;
            }

            if (Probes[letter] is null)
            {
                var was = Answers[letter];
                var probe = Task.Run(() => Probe(root, share));
                Probes[letter] = probe;
                _ = probe.ContinueWith(
                    finished => Told(was, finished.Result),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            return Answers[letter] is { } answer
                ? answer with { Share = share.Length > 0 ? share : answer.Share }
                : new DriveEntry(root, VolumeKind.Network, DriveState.Pending, Share: share);
        }
    }

    /// <summary>The answer is in: whoever listens is told when it changes whether the drive is there.</summary>
    private static void Told(DriveEntry? was, DriveEntry now)
    {
        try
        {
            if (was is null || was.State != now.State)
            {
                NetworkAnswered?.Invoke();
            }
        }
        catch (Exception ex)
        {
            // A continuation on the pool: nothing may escape it.
            Log.Error(Scope, "telling the drive widgets about a network drive failed", ex);
        }
    }

    /// <summary>One question to the share, on the pool. Never throws.</summary>
    private static DriveEntry Probe(string root, string share)
    {
        uint previous;
        var moded = SetThreadErrorMode(SemFailCriticalErrors | SemNoOpenFileErrorBox, &previous);

        try
        {
            (ulong Total, ulong Free)? space = null;
            var state = DriveRules.NetworkState(0, () => (space = Space(root)) is not null);
            if (state != DriveState.Ready || space is not { } read)
            {
                return new DriveEntry(root, VolumeKind.Network, DriveState.Disconnected, Share: share);
            }

            var (label, fileSystem) = Volume(root) ?? (string.Empty, string.Empty);
            return new DriveEntry(root, VolumeKind.Network, DriveState.Ready, label, fileSystem, read.Total, read.Free, share);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"asking {root} failed", ex);
            return new DriveEntry(root, VolumeKind.Network, DriveState.Disconnected, Share: share);
        }
        finally
        {
            if (moded)
            {
                // ignore: best-effort restore of a dialog-suppression flag on a pool thread.
                _ = SetThreadErrorMode(previous, null);
            }
        }
    }

    private static void Forget(int letter)
    {
        lock (Gate)
        {
            Answers[letter] = null;

            // A probe still out finishes on its own and is collected (or dropped) by the next walk.
            if (Probes[letter] is { IsCompleted: true })
            {
                Probes[letter] = null;
            }
        }
    }

    /// <summary>The label and file system, or null when the drive has no medium (or will not say).</summary>
    private static (string Label, string FileSystem)? Volume(string root)
    {
        var label = stackalloc char[261];
        var fileSystem = stackalloc char[261];

        fixed (char* name = root)
        {
            if (!GetVolumeInformationW(name, label, 261, null, null, null, fileSystem, 261))
            {
                return null;
            }
        }

        return (new string(label), new string(fileSystem));
    }

    /// <summary>What this user can write and the size (quotas included, as Explorer shows them); null when it will not say.</summary>
    private static (ulong Total, ulong Free)? Space(string root)
    {
        ulong available, total, free;

        fixed (char* name = root)
        {
            return GetDiskFreeSpaceExW(name, &available, &total, &free) && total > 0 ? (total, available) : null;
        }
    }

    /// <summary>The share a letter is mapped to and what the redirector says of it. Local and immediate: no question to the network.</summary>
    private static (uint Status, string Share) Connection(int letter)
    {
        var local = $"{(char)('A' + letter)}:";
        var path = stackalloc char[1024];
        uint length = 1024;
        uint status;

        fixed (char* name = local)
        {
            status = WNetGetConnectionW(name, path, &length);
        }

        // A remembered connection that is not connected still has its path.
        return status == 0 || status == DriveRules.ConnectionUnavailable
            ? (status, new string(path).TrimEnd('\\'))
            : (status, string.Empty);
    }

    /// <summary>
    /// Whether a fixed disk sits on a bus that comes and goes (USB, FireWire, SD): an external disk,
    /// which Windows calls fixed but Explorer can eject. Asked of the volume's device with no access
    /// rights (a property query needs none), so it neither needs elevation nor wakes the disk.
    /// </summary>
    private static bool Hotplug(string root)
    {
        using var device = CreateFileW($@"\\.\{root.TrimEnd('\\')}", 0, FileShareReadWrite, 0, OpenExisting, 0, 0);
        if (device.IsInvalid)
        {
            return false;
        }

        // STORAGE_PROPERTY_QUERY { StorageDeviceProperty = 0, PropertyStandardQuery = 0, AdditionalParameters[1] }.
        var query = stackalloc int[3];
        query[0] = 0;
        query[1] = 0;
        query[2] = 0;

        // STORAGE_DEVICE_DESCRIPTOR: BusType is the DWORD at offset 28; 1 KB holds the strings after it.
        var descriptor = stackalloc byte[1024];
        uint returned = 0;

        if (!DeviceIoControl(device, IoctlStorageQueryProperty, query, 12, descriptor, 1024, &returned, 0) || returned < 32)
        {
            return false;
        }

        var bus = *(int*)(descriptor + 28);
        return bus is BusTypeUsb or BusType1394 or BusTypeSd or BusTypeMmc;
    }

    [LibraryImport("kernel32.dll")]
    private static partial uint GetLogicalDrives();

    [LibraryImport("kernel32.dll")]
    private static partial uint GetDriveTypeW(char* root);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetThreadErrorMode(uint mode, uint* previous);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumeInformationW(char* root, char* label, uint labelSize, uint* serial, uint* maxComponent, uint* flags, char* fileSystem, uint fileSystemSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceExW(char* root, ulong* available, ulong* total, ulong* free);

    [LibraryImport("mpr.dll")]
    private static partial uint WNetGetConnectionW(char* local, char* remote, uint* length);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle device, uint code, void* input, uint inputSize, void* output, uint outputSize, uint* returned, nint overlapped);
}
