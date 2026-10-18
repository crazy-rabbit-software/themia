namespace Themia.Core.Widgets;

/// <summary>
/// [S-1130] One read of the machine's memory, in bytes, as Windows reports it.
/// </summary>
/// <param name="Total">Installed physical memory the OS can use.</param>
/// <param name="Available">Physical memory available to a new allocation (standby + free).</param>
public readonly record struct MemorySnapshot(ulong Total, ulong Available);

/// <summary>
/// [S-1130] What the memory meter derives from a <see cref="MemorySnapshot"/>: the share in use.
/// </summary>
public static class MemoryFigures
{
    /// <summary>Bytes in use: the total less what is available.</summary>
    public static ulong Used(MemorySnapshot s) => s.Total > s.Available ? s.Total - s.Available : 0;

    /// <summary>Share in use, 0..1, or null when the total is unknown.</summary>
    public static double? UsedFraction(MemorySnapshot s) => s.Total == 0 ? null : (double)Used(s) / s.Total;
}
