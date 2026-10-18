namespace Themia.Core.Widgets;

/// <summary>
/// [S-3905] One toggle widget's clicks: what a click asks for, what the tile shows while Windows is
/// still switching, and that a click Windows refused says so. Pure and clocked by the caller, so the
/// debounce is tested without a desktop.
/// <para>
/// One switch at a time: a click while one is still on its way does nothing, so a burst of clicks
/// cannot queue up a row of flips behind a slow radio. A second click within
/// <see cref="DoubleClick"/> of the last one is the same intent (a double-click) and is ignored too,
/// so it never flips straight back. While a switch is on its way the tile shows where it is going at
/// once; when Windows answers, the source's own reading is what stands.
/// </para>
/// </summary>
public sealed class ToggleGate
{
    /// <summary>A second click this soon after one is the same click (as [S-1913] for the power widgets).</summary>
    public static readonly TimeSpan DoubleClick = TimeSpan.FromMilliseconds(400);

    private DateTimeOffset? _last;

    /// <summary>Where a switch on its way is going; null when none is.</summary>
    public bool? Pending { get; private set; }

    /// <summary>The last switch was refused by Windows; cleared by the next click.</summary>
    public bool Refused { get; private set; }

    /// <summary>
    /// A click at <paramref name="now"/> on a tile reading <paramref name="reading"/>: the state to ask
    /// Windows for, or null when the click does nothing (not switchable, a switch already on its way,
    /// or a double-click).
    /// </summary>
    public bool? Press(ToggleReading reading, DateTimeOffset now)
    {
        if (Pending is not null || !reading.CanSwitch)
        {
            return null;
        }

        if (_last is { } last && now >= last && now - last < DoubleClick)
        {
            return null;
        }

        _last = now;
        Refused = false;
        Pending = !reading.On!.Value;
        return Pending;
    }

    /// <summary>Windows answered the switch: <paramref name="succeeded"/> or refused.</summary>
    public void Settled(bool succeeded)
    {
        Pending = null;
        Refused = !succeeded;
    }

    /// <summary>What the tile shows now.</summary>
    public ToggleLook Look(ToggleReading reading) => QuickToggleRules.Look(reading, Pending);
}
