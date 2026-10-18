using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-1160]..[S-1163] [S-1820] The disk meter's data. Which volumes exist, and their labels (the
/// header's name for a drive), are the shared drive walk's (<see cref="Drives.DriveService"/>, [S-4655]),
/// which asks every drive at most every 30 s and on a device change, so no tick wakes a sleeping
/// drive (the original's [S-21]).
/// <para>
/// Reads and writes come from one <see cref="PdhQuery"/> shared by every disk widget, opened by the
/// first widget and closed when the last one lets go (<see cref="Acquire"/>), so a desktop without a
/// disk widget holds no counters. The system hub is the only reader, once per due tick for every
/// disk widget ([S-2501]); every read happens on the UI thread's tick, so the state below needs no lock.
/// </para>
/// </summary>
internal static class DiskMetrics
{
    private const string Scope = "disk";

    private static int _leases;
    private static PdhQuery? _query;
    private static bool _queryFailed;
    private static PdhCounter? _readBytes;
    private static PdhCounter? _writeBytes;

    /// <summary>Hold the I/O counters open; dispose to let them go.</summary>
    public static IDisposable Acquire()
    {
        _leases++;
        return new Lease();
    }

    /// <summary>
    /// The volumes on this machine (fixed, and removable ones that are ready), as of the drive service's
    /// last walk; empty until its first. Never touches a drive, so a layout pass can ask.
    /// </summary>
    public static IReadOnlyList<DiskVolume> Volumes() => DriveRules.LocalVolumes(Drives.DriveService.Shared.Drives);

    /// <summary>
    /// The I/O counters as of now, or null while no widget holds them or PDH would not open. Rate
    /// counters need two collections, so the first call after opening has no rates yet.
    /// </summary>
    public static DiskActivity? Activity()
    {
        if (Open() is not { } query)
        {
            return null;
        }

        query.Collect();

        var rates = _readBytes is { } r && _writeBytes is { } w
            ? DiskRules.Pair(query.Doubles(r), query.Doubles(w))
            : new Dictionary<string, DiskRates>();

        return new DiskActivity(rates);
    }

    private static PdhQuery? Open()
    {
        if (_query is not null || _queryFailed || _leases == 0)
        {
            return _query;
        }

        _query = PdhQuery.Open(Scope);

        if (_query is null)
        {
            // Logged by Open. Not retried every tick: the I/O figures simply stay hidden.
            _queryFailed = true;
            return null;
        }

        // Each is optional: a counter this machine lacks leaves its figure out rather than guessed.
        _readBytes = _query.Add(@"\LogicalDisk(*)\Disk Read Bytes/sec");
        _writeBytes = _query.Add(@"\LogicalDisk(*)\Disk Write Bytes/sec");
        return _query;
    }

    private static void Release()
    {
        _leases = Math.Max(0, _leases - 1);

        if (_leases > 0)
        {
            return;
        }

        _query?.Dispose();
        _query = null;
        _queryFailed = false;
        _readBytes = _writeBytes = null;
    }

    /// <summary>One widget's hold on the counters. Disposing twice releases once.</summary>
    private sealed class Lease : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            Release();
        }
    }
}
