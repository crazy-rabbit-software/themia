using System.Buffers.Binary;
using System.Text;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-2410] One process as the kernel's snapshot lists it.</summary>
/// <param name="Pid">Process id; unique only among the living.</param>
/// <param name="Name">Image name without ".exe".</param>
/// <param name="CreateTime">FILETIME it started: with the pid, its identity ([S-2414]).</param>
/// <param name="CpuTicks">Kernel plus user time so far, in 100 ns.</param>
/// <param name="PrivateBytes">Private working set: Task Manager's Memory column.</param>
public readonly record struct ProcessEntry(int Pid, string Name, long CreateTime, long CpuTicks, long PrivateBytes);

/// <summary>[S-2410] One process (or, grouped, one program) in a read: its processor share and memory.</summary>
/// <param name="Name">The image name.</param>
/// <param name="Cpu">Share of the whole machine since the last read, 0..1.</param>
/// <param name="Memory">Private working set in bytes.</param>
/// <param name="Count">How many processes the row stands for (1 ungrouped).</param>
/// <param name="Process">[S-2433] Whose executable the row's icon is (a group's first member); default when unknown.</param>
public readonly record struct ProcessSample(string Name, double Cpu, long Memory, int Count = 1, ProcessIdentity Process = default);

/// <summary>[S-2414] [S-2433] A process as Windows tells two apart: its pid and when it started (a pid is reused within seconds).</summary>
public readonly record struct ProcessIdentity(int Pid, long CreateTime);

/// <summary>
/// [S-2433] Where each listed process's executable is, asked once per process: the path is a kernel
/// query on a handle, so a list read every second asks only about processes it has not seen before,
/// and forgets those that have exited. A process Windows will not open (protected, elevated) is
/// remembered as having no path, so it is not asked again either. One thread (the UI thread's tick).
/// </summary>
public sealed class ProcessPathCache
{
    /// <summary>A machine runs a few hundred processes; past this the cache starts over rather than grow.</summary>
    public const int Limit = 2048;

    private readonly Func<int, string?> _resolve;
    private readonly Dictionary<ProcessIdentity, string?> _paths = [];

    /// <param name="resolve">The executable's path for a pid, or null; called at most once per process.</param>
    public ProcessPathCache(Func<int, string?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        _resolve = resolve;
    }

    public int Count => _paths.Count;

    /// <summary>The executable of <paramref name="process"/>, or null (unknown, or not to be had).</summary>
    public string? PathOf(ProcessIdentity process)
    {
        if (process.Pid <= 0)
        {
            return null;
        }

        if (_paths.TryGetValue(process, out var known))
        {
            return known;
        }

        if (_paths.Count >= Limit)
        {
            _paths.Clear();
        }

        var path = _resolve(process.Pid);
        path = string.IsNullOrWhiteSpace(path) ? null : path;
        _paths[process] = path;
        return path;
    }

    /// <summary>Forget every process not in <paramref name="living"/> (the latest snapshot): an exited process's pid will be someone else's.</summary>
    public void Retain(IEnumerable<ProcessIdentity> living)
    {
        ArgumentNullException.ThrowIfNull(living);
        var keep = new HashSet<ProcessIdentity>(living);

        foreach (var process in _paths.Keys.Where(p => !keep.Contains(p)).ToList())
        {
            _paths.Remove(process);
        }
    }
}

/// <summary>
/// [S-2410]..[S-2414] The process list's rules: reading the kernel's snapshot, turning cumulative
/// times into shares, grouping and ranking. Pure, so the layout of the snapshot and the arithmetic
/// at its edges are pinned by tests.
/// </summary>
public static class ProcessTable
{
    // x64 SYSTEM_PROCESS_INFORMATION. A stable kernel ABI since Vista (what Task Manager reads).
    public const int NextEntryOffset = 0x00;
    public const int ThreadCountOffset = 0x04;
    public const int PrivateWorkingSetOffset = 0x08;
    public const int CreateTimeOffset = 0x20;
    public const int UserTimeOffset = 0x28;
    public const int KernelTimeOffset = 0x30;
    public const int NameLengthOffset = 0x38;
    public const int NameBufferOffset = 0x40;
    public const int PidOffset = 0x50;

    /// <summary>The fixed part of one entry.</summary>
    public const int EntrySize = 0x100;

    /// <summary>One <c>SYSTEM_THREAD_INFORMATION</c>, an array of which follows each entry.</summary>
    public const int ThreadSize = 0x50;

    /// <summary>
    /// [S-2410] Every process in a snapshot buffer. <paramref name="bufferAddress"/> is where the
    /// buffer sits in memory: the kernel writes each image name into the same buffer and hands back
    /// a pointer to it, which is turned back into an offset here and read only when it lies inside.
    /// The walk stops at a terminator, a truncated entry, an entry claiming more threads than fit, or
    /// a link that does not run forward: a layout it does not understand is never read as data. The
    /// idle process (pid 0) is not a process and is left out.
    /// </summary>
    public static List<ProcessEntry> Parse(ReadOnlySpan<byte> buffer, ulong bufferAddress)
    {
        var entries = new List<ProcessEntry>(Math.Min(1024, buffer.Length / EntrySize));
        var offset = 0;

        for (var guard = 0; guard < 65536; guard++)
        {
            if (buffer.Length - offset < EntrySize)
            {
                break;
            }

            var entry = buffer[offset..];
            var threads = BinaryPrimitives.ReadUInt32LittleEndian(entry[ThreadCountOffset..]);
            var span = EntrySize + ((long)threads * ThreadSize);

            if (span > buffer.Length - offset)
            {
                break;
            }

            var pid = (int)BinaryPrimitives.ReadInt64LittleEndian(entry[PidOffset..]);

            if (pid != 0)
            {
                var cpu = BinaryPrimitives.ReadInt64LittleEndian(entry[UserTimeOffset..]) + BinaryPrimitives.ReadInt64LittleEndian(entry[KernelTimeOffset..]);
                entries.Add(new ProcessEntry(
                    pid,
                    Name(buffer, bufferAddress, entry, pid),
                    BinaryPrimitives.ReadInt64LittleEndian(entry[CreateTimeOffset..]),
                    Math.Max(0, cpu),
                    Math.Max(0, BinaryPrimitives.ReadInt64LittleEndian(entry[PrivateWorkingSetOffset..]))));
            }

            var next = BinaryPrimitives.ReadUInt32LittleEndian(entry);

            if (next == 0 || next < span || next > buffer.Length - offset)
            {
                break;
            }

            offset += (int)next;
        }

        return entries;
    }

    private static string Name(ReadOnlySpan<byte> buffer, ulong bufferAddress, ReadOnlySpan<byte> entry, int pid)
    {
        var length = BinaryPrimitives.ReadUInt16LittleEndian(entry[NameLengthOffset..]);
        var pointer = BinaryPrimitives.ReadUInt64LittleEndian(entry[NameBufferOffset..]);

        if (length == 0 || pointer < bufferAddress || pointer - bufferAddress > (ulong)buffer.Length || (length & 1) != 0)
        {
            return pid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var start = (int)(pointer - bufferAddress);

        if (start + length > buffer.Length)
        {
            return pid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ImageName(Encoding.Unicode.GetString(buffer.Slice(start, length)));
    }

    /// <summary>An image name without its ".exe", as Task Manager's details list shows it.</summary>
    public static string ImageName(string raw)
    {
        var name = raw.Trim();
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Length > 4 ? name[..^4] : name;
    }

    /// <summary>
    /// [S-2414] Processor time over a wall-clock interval as a share of the whole machine: a process
    /// on every core burns <paramref name="processors"/> seconds a second, so dividing by the count
    /// puts a saturated machine at 100%, matching the CPU meter.
    /// </summary>
    public static double CpuFraction(long cpuDeltaTicks, long elapsedTicks, int processors)
        => cpuDeltaTicks <= 0 || elapsedTicks <= 0 || processors <= 0
            ? 0
            : Math.Clamp((double)cpuDeltaTicks / (elapsedTicks * (double)processors), 0, 1);

    /// <summary>
    /// [S-2411] [S-2413] The samples ranked by <paramref name="sort"/>, heaviest first, ties by name so
    /// a list at rest does not reshuffle. Grouped, every process of one image name (ignoring case) is
    /// one row: the shares and the memory added up.
    /// </summary>
    public static List<ProcessSample> Rank(IEnumerable<ProcessSample> samples, ProcessSort sort, bool group)
    {
        ArgumentNullException.ThrowIfNull(samples);
        IEnumerable<ProcessSample> rows = samples;

        if (group)
        {
            rows = samples
                .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ProcessSample(g.First().Name, Math.Min(1, g.Sum(s => s.Cpu)), g.Sum(s => s.Memory), g.Sum(s => s.Count), FirstKnown(g)));
        }

        return sort == ProcessSort.Memory
            ? [.. rows.OrderByDescending(s => s.Memory).ThenByDescending(s => s.Cpu).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)]
            : [.. rows.OrderByDescending(s => s.Cpu).ThenByDescending(s => s.Memory).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>A group's icon is its oldest member's: the browser's main process, not a helper that comes and goes.</summary>
    private static ProcessIdentity FirstKnown(IEnumerable<ProcessSample> group)
    {
        var known = group.Where(s => s.Process.Pid > 0).ToList();
        return known.Count == 0 ? default : known.MinBy(s => s.Process.CreateTime).Process;
    }

    /// <summary>
    /// [S-2412] The rows to list: the ranking cut to <paramref name="maxRows"/> (0 = no ceiling), the
    /// ranked figure first and the other beside it. A group of several shows its count after the name.
    /// [S-2433] Each row leads with its executable's icon (<paramref name="iconPath"/>), or the
    /// program symbol when the path cannot be had.
    /// </summary>
    public static List<DetailRow> Rows(Locale locale, IReadOnlyList<ProcessSample> ranked, ProcessSort sort, int maxRows, Func<ProcessSample, string?>? iconPath = null)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        var take = maxRows > 0 ? Math.Min(maxRows, ranked.Count) : ranked.Count;
        var rows = new List<DetailRow>(take);

        for (var i = 0; i < take; i++)
        {
            var s = ranked[i];
            var cpu = Formatting.Percent(locale, s.Cpu);
            var memory = Formatting.Bytes(locale, s.Memory);
            var name = s.Count > 1 ? $"{s.Name} ({LocaleFormat.Integer(locale, s.Count)})" : s.Name;
            var icon = iconPath?.Invoke(s);
            rows.Add(sort == ProcessSort.Memory
                ? new DetailRow(s.Name, name, memory, cpu, Symbol: RowSymbol.App, IconPath: icon)
                : new DetailRow(s.Name, name, cpu, memory, Symbol: RowSymbol.App, IconPath: icon));
        }

        return rows;
    }
}

/// <summary>
/// [S-2414] Per-process processor shares from the snapshot's cumulative times. CPU is a delta, so
/// each process's last reading is kept — keyed on the pid <i>and</i> its start time, because Windows
/// reuses pids within seconds and subtracting one process's times from another's pins the wrong load
/// to the wrong name. A process seen for the first time reads 0 (its lifetime is not one interval's
/// work); a process gone is forgotten. One instance for the app, read once per interval.
/// </summary>
public sealed class ProcessTracker
{
    private readonly Dictionary<int, (long Create, long Cpu)> _last = [];
    private long? _lastAt;

    /// <summary>Fold one snapshot taken at <paramref name="nowTicks"/> (100 ns, monotonic) into shares.</summary>
    public List<ProcessSample> Update(IReadOnlyList<ProcessEntry> entries, long nowTicks, int processors)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var elapsed = _lastAt is { } at ? nowTicks - at : 0;
        _lastAt = nowTicks;

        var samples = new List<ProcessSample>(entries.Count);
        var seen = new Dictionary<int, (long, long)>(entries.Count);

        foreach (var e in entries)
        {
            var cpu = _last.TryGetValue(e.Pid, out var before) && before.Create == e.CreateTime
                ? ProcessTable.CpuFraction(e.CpuTicks - before.Cpu, elapsed, processors)
                : 0;

            seen[e.Pid] = (e.CreateTime, e.CpuTicks);
            samples.Add(new ProcessSample(e.Name, cpu, e.PrivateBytes, Process: new ProcessIdentity(e.Pid, e.CreateTime)));
        }

        _last.Clear();
        foreach (var (pid, state) in seen)
        {
            _last[pid] = state;
        }

        return samples;
    }
}
