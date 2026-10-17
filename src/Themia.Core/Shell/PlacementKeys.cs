namespace Themia.Core.Shell;

/// <summary>What the placement session's keyboard watch does with one key event ([S-1594]).</summary>
public enum PlacementKeyVerdict
{
    /// <summary>Not the session's: it goes on to whichever window has the keyboard.</summary>
    PassOn = 0,

    /// <summary>Escape: cancel the placement, and the key goes nowhere else.</summary>
    Cancel,

    /// <summary>The rest of a press that cancelled (its auto-repeat, its release): kept from every other window.</summary>
    Swallow,
}

/// <summary>
/// [S-1594] The keys of a placement session, heard wherever the keyboard is: Escape cancels the
/// placement (as a right-click does, [S-1587]) whichever window or display has the keyboard. The
/// press that cancels is kept from every other window, its auto-repeat and its release included, so
/// the same press does not also back the surface out of edit mode ([S-1583]) or close another app's
/// dialog. Escape with Ctrl, Alt or the Windows key is the system's (Start, Task Manager, Alt+Esc)
/// and passes on.
/// <para>
/// One instance per watch, fed every key event in order on the watcher's thread; <see cref="Reset"/>
/// when the watch starts.
/// </para>
/// </summary>
public sealed class PlacementKeyFilter
{
    /// <summary>VK_ESCAPE.</summary>
    public const int Escape = 0x1B;

    /// <summary>An Escape press that cancelled is still down: its repeats and its release are ours.</summary>
    private bool _held;

    /// <summary>
    /// The cancelling Escape is still down. The watch stays on until it comes up: let go early, the
    /// key's auto-repeat would reach the surface as fresh Escapes and back it out of edit mode.
    /// </summary>
    public bool Holding => _held;

    /// <summary>A new watch: nothing held.</summary>
    public void Reset() => _held = false;

    /// <param name="virtualKey">The key's virtual-key code.</param>
    /// <param name="down">A press (or its auto-repeat); false for a release.</param>
    /// <param name="systemModifier">Ctrl, Alt or a Windows key is down.</param>
    public PlacementKeyVerdict Decide(int virtualKey, bool down, bool systemModifier)
    {
        if (virtualKey != Escape)
        {
            return PlacementKeyVerdict.PassOn;
        }

        if (!down)
        {
            var held = _held;
            _held = false;
            return held ? PlacementKeyVerdict.Swallow : PlacementKeyVerdict.PassOn;
        }

        if (_held)
        {
            return PlacementKeyVerdict.Swallow;
        }

        if (systemModifier)
        {
            return PlacementKeyVerdict.PassOn;
        }

        _held = true;
        return PlacementKeyVerdict.Cancel;
    }
}
