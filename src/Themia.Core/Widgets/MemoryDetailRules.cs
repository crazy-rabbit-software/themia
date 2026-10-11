using System.Buffers.Binary;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-2408] What <c>GetPerformanceInfo</c> reports, already in bytes (the call counts pages; the App
/// multiplies by its page size through <see cref="MemoryDetailRules.Bytes"/>).
/// </summary>
public readonly record struct PerformanceFigures(
    ulong CommitTotal,
    ulong CommitLimit,
    ulong PhysicalTotal,
    ulong PhysicalAvailable,
    ulong SystemCache,
    ulong KernelPaged,
    ulong KernelNonpaged,
    uint Handles,
    uint Processes,
    uint Threads);

/// <summary>[S-2409] The page files together: their size and what is in use, in bytes. Both 0: there is no page file.</summary>
public readonly record struct PagefileUsage(ulong Total, ulong Used);

/// <summary>[S-2408] [S-2409] The memory details widget's rules, pure.</summary>
public static class MemoryDetailRules
{
    /// <summary>"31.7 GB" is 7; "1023.9 MB" the widest a byte figure gets.</summary>
    public const double HeroCharacters = 9;

    /// <summary>The fixed part of one <c>SYSTEM_PAGEFILE_INFORMATION</c> entry on 64-bit Windows.</summary>
    public const int PagefileEntrySize = 32;

    /// <summary>A count of pages as bytes, saturating rather than wrapping on a nonsense count.</summary>
    public static ulong Bytes(ulong pages, ulong pageSize)
        => pageSize == 0 ? 0 : pages > ulong.MaxValue / pageSize ? ulong.MaxValue : pages * pageSize;

    /// <summary>
    /// [S-2409] The page files in a <c>NtQuerySystemInformation(SystemPageFileInformation)</c> buffer:
    /// a chain of entries (<c>NextEntryOffset</c> 0, <c>TotalSize</c> 4, <c>TotalInUse</c> 8, in
    /// pages), summed. An empty buffer is "no page file" (zeros); a chain that runs off the buffer or
    /// backwards stops there with what it read, never reading past the end.
    /// </summary>
    public static PagefileUsage ParsePagefiles(ReadOnlySpan<byte> buffer, ulong pageSize)
    {
        ulong total = 0, used = 0;
        var offset = 0;

        // A backstop: no machine has more page files than this, so a longer chain is not one.
        for (var i = 0; i < 64 && offset >= 0 && buffer.Length - offset >= PagefileEntrySize; i++)
        {
            var entry = buffer[offset..];
            var next = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            var inUse = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);

            total += Bytes(size, pageSize);
            used += Bytes(Math.Min(inUse, size), pageSize);

            if (next == 0 || next < PagefileEntrySize || next > buffer.Length - offset)
            {
                break;
            }

            offset += (int)next;
        }

        return new PagefileUsage(total, used);
    }

    /// <summary>"12.1 GB / 36.5 GB".</summary>
    public static string UsedOfTotal(Locale locale, ulong used, ulong total)
        => Strings.Format(locale, Str.Details_UsedOfTotal, ("used", Formatting.Bytes(locale, used)), ("total", Formatting.Bytes(locale, total)));

    /// <summary>
    /// [S-2408] The sheet: memory in use as the hero, "29% of 31.7 GB" under it, then available,
    /// committed, cached, paged pool, non-paged pool and the page file ("None" without one).
    /// <paramref name="pagefile"/> null is "could not read", which leaves that fact out.
    /// </summary>
    public static DetailSheet Sheet(Locale locale, PerformanceFigures? figures, PagefileUsage? pagefile)
    {
        if (figures is not { PhysicalTotal: > 0 } f)
        {
            return DetailSheet.Waiting;
        }

        var available = Math.Min(f.PhysicalAvailable, f.PhysicalTotal);
        var used = f.PhysicalTotal - available;
        var caption = Strings.Format(locale, Str.Details_PercentOf,
            ("percent", Formatting.Percent(locale, (double)used / f.PhysicalTotal)),
            ("total", Formatting.Bytes(locale, f.PhysicalTotal)));

        var facts = new List<DetailFact>(6)
        {
            new(Strings.Get(locale, Str.Details_Memory_Available), Formatting.Bytes(locale, available)),
        };

        if (f.CommitLimit > 0)
        {
            facts.Add(new(Strings.Get(locale, Str.Details_Memory_Committed), UsedOfTotal(locale, Math.Min(f.CommitTotal, f.CommitLimit), f.CommitLimit)));
        }

        facts.Add(new(Strings.Get(locale, Str.Details_Memory_Cached), Formatting.Bytes(locale, f.SystemCache)));
        facts.Add(new(Strings.Get(locale, Str.Details_Memory_PagedPool), Formatting.Bytes(locale, f.KernelPaged)));
        facts.Add(new(Strings.Get(locale, Str.Details_Memory_NonPagedPool), Formatting.Bytes(locale, f.KernelNonpaged)));

        if (pagefile is { } p)
        {
            facts.Add(new(Strings.Get(locale, Str.Details_Memory_PageFile),
                p.Total == 0 ? Strings.Get(locale, Str.Details_None) : UsedOfTotal(locale, p.Used, p.Total)));
        }

        return new DetailSheet(Formatting.Bytes(locale, used), caption, facts, []);
    }
}
