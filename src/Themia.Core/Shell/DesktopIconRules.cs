using Themia.Core.Model;

namespace Themia.Core.Shell;

/// <summary>
/// One <c>SHELLDLL_DefView</c> the lookup found, as the rules see it.
/// </summary>
/// <param name="UnderProgman">Its parent is the shell's <c>Progman</c> (otherwise a <c>WorkerW</c>).</param>
/// <param name="OwnedByShell">It lives in the process that owns the shell window (<c>GetShellWindow</c>).</param>
/// <param name="HasList">It has its icon list (<c>SysListView32</c>).</param>
public readonly record struct DesktopViewCandidate(bool UnderProgman, bool OwnedByShell, bool HasList);

/// <summary>What one step of applying the switch does.</summary>
public enum DesktopIconStep
{
    /// <summary>The icons already are as asked: nothing to send.</summary>
    Done,

    /// <summary>Send the shell's toggle command once.</summary>
    Toggle,

    /// <summary>The live state cannot be read: send nothing (a blind toggle may flip the wrong way).</summary>
    Unreadable,

    /// <summary>The command was handled and the icons are still not as asked: stop, say so, show what is.</summary>
    DidNotTake,
}

/// <summary>
/// [S-930] [S-5503]..[S-5505] Explorer's desktop icons, as pure rules.
/// <para>
/// Windows owns its "Show desktop icons" setting, not Themia: the shell's own command flips it and
/// persists it, and the user can flip it back from the desktop's context menu at any time. Themia's
/// own choice (<see cref="DesktopShows"/>) therefore hides the icon list for the session only and
/// shows it again as Windows has it; the one write to Windows' setting is the Windows' icons pick
/// while Windows hides them.
/// </para>
/// </summary>
public static class DesktopIconRules
{
    /// <summary><c>WS_VISIBLE</c>.</summary>
    public const uint WsVisible = 0x10000000;

    /// <summary>
    /// [S-931] Whether the icons are shown, from the icon list's <i>own</i> style. Explorer hides that
    /// window itself, so its <c>WS_VISIBLE</c> bit is the switch. Not <c>IsWindowVisible</c>: that
    /// also answers false while an ancestor (<c>Progman</c>, a <c>WorkerW</c>) is hidden for a moment,
    /// and a "show" request read that way toggles shown icons off.
    /// </summary>
    public static bool IsShown(uint listStyle) => (listStyle & WsVisible) != 0;

    /// <summary>
    /// [S-930] Which found view gets the command, or -1 for none. Only one of the shell's own process
    /// (another program can register a window called <c>Progman</c> or <c>WorkerW</c>), only one with
    /// its icon list, and the one under <c>Progman</c> before one under a <c>WorkerW</c>.
    /// </summary>
    public static int ChooseView(IReadOnlyList<DesktopViewCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var fallback = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (!candidate.OwnedByShell || !candidate.HasList)
            {
                continue;
            }

            if (candidate.UnderProgman)
            {
                return i;
            }

            if (fallback < 0)
            {
                fallback = i;
            }
        }

        return fallback;
    }

    /// <summary>
    /// [S-931] [S-939] The next step towards <paramref name="wanted"/>, given the live state read now
    /// and whether the toggle was already sent for this request. The command has no "set" form, so it
    /// is only sent when the live state is known and differs: sending on an unreadable state has an
    /// even chance of hiding the icons of a user who just asked to show them. One command per request
    /// at most: the
    /// read after a handled send is the confirmation, and a second toggle on a state that did not
    /// move could just as well undo a late first one.
    /// </summary>
    public static DesktopIconStep Plan(bool? live, bool wanted, bool sent)
    {
        if (live is not bool current)
        {
            return DesktopIconStep.Unreadable;
        }

        if (current == wanted)
        {
            return DesktopIconStep.Done;
        }

        return sent ? DesktopIconStep.DidNotTake : DesktopIconStep.Toggle;
    }

    /// <summary>
    /// [S-5503] How often a held list may be shown again (by Explorer, a refresh, a third party)
    /// before Themia leaves it shown: a hide that never stays is someone else's, not a fight to win.
    /// </summary>
    public const int MaxRehides = 8;

    /// <summary>[S-5503] Whether <paramref name="shows"/> hides Explorer's icons for the session.</summary>
    public static bool Hides(DesktopShows shows) => shows is DesktopShows.ThemiaItems or DesktopShows.Nothing;

    /// <summary>
    /// [S-5503] Whether a held list that was just shown again is hidden again: while the hide is held
    /// and fewer than <see cref="MaxRehides"/> re-hides were spent since the choice was applied.
    /// </summary>
    public static bool Rehide(bool holding, bool? shown, int rehidesSoFar) => holding && shown == true && rehidesSoFar < MaxRehides;

    /// <summary>
    /// [S-5504] Whether Windows' own setting hides the desktop icons: Explorer's <c>HideIcons</c>
    /// value (absent: shown) or the <c>NoDesktop</c> policy. Read from the registry, which needs no
    /// Explorer reply, so a crash handler can ask it.
    /// </summary>
    public static bool WindowsHides(int? hideIcons, int? noDesktopPolicy) => hideIcons is int hide && hide != 0 || noDesktopPolicy is int policy && policy != 0;

    /// <summary>
    /// [S-5504] Whether a restore shows the list: only a list that is there and hidden, and only when
    /// Windows' own setting says shown. It never shows icons the user hid in Windows, and an
    /// unreadable shell gets nothing.
    /// </summary>
    public static bool Restore(bool? shown, bool windowsHides) => shown == false && !windowsHides;

    /// <summary>
    /// [S-5505] Whether picking <paramref name="picked"/> sends the shell's own command: only the
    /// Windows' icons pick, only while Windows' own setting hides them. Every other pick leaves
    /// Windows' setting alone.
    /// </summary>
    public static bool AsksWindows(DesktopShows picked, bool windowsHides) => picked == DesktopShows.WindowsIcons && windowsHides;
}

/// <summary>
/// [S-939] The switch's requests, single-flight and latest-wins. One worker applies them in order;
/// a request made while it works replaces any still waiting, so flicking the switch off and on
/// quickly ends with the icons as the switch was left, never with two toggles racing the shell.
/// Thread-safe.
/// </summary>
public sealed class DesktopIconRequests
{
    private readonly Lock _gate = new();
    private bool? _waiting;
    private bool _running;

    /// <summary>Whether a worker is applying requests now. The switch does not adopt the live state while it is.</summary>
    public bool Busy
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    /// <summary>Ask for <paramref name="wanted"/>. True when the caller must start the worker; false when one is already running and will pick it up.</summary>
    public bool Submit(bool wanted)
    {
        lock (_gate)
        {
            _waiting = wanted;
            if (_running)
            {
                return false;
            }

            _running = true;
            return true;
        }
    }

    /// <summary>The worker's next target, or null when there is none — the worker then stops, and <see cref="Busy"/> is false.</summary>
    public bool? Take()
    {
        lock (_gate)
        {
            if (_waiting is bool next)
            {
                _waiting = null;
                return next;
            }

            _running = false;
            return null;
        }
    }
}
