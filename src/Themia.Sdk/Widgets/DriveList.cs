using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-4655] The drives a settings page lists (the disk meter's and the Drive widget's drive choice):
/// the host's walk, installed by the host once at start, as the place lookup is.
/// </summary>
public static class DriveList
{
    private static Func<IReadOnlyList<DriveEntry>>? _now;

    /// <summary>The last walk while a widget watches, else one walk now (local drives at once, a share pending).</summary>
    public static IReadOnlyList<DriveEntry> Now()
        => (Volatile.Read(ref _now) ?? throw new InvalidOperationException("no drive list installed: the host installs it before any settings page is shown"))();

    /// <summary>The host's drive service. Once per process.</summary>
    internal static void Install(Func<IReadOnlyList<DriveEntry>> now)
    {
        ArgumentNullException.ThrowIfNull(now);
        if (Interlocked.CompareExchange(ref _now, now, null) is not null)
        {
            throw new InvalidOperationException("the drive list is installed once per process");
        }
    }
}
