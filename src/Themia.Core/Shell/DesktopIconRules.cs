namespace Themia.Core.Shell;

/// <summary>
/// One <c>SHELLDLL_DefView</c> the lookup found, as the rules see it.
/// </summary>
/// <param name="UnderProgman">Its parent is the shell's <c>Progman</c> (otherwise a <c>WorkerW</c>).</param>
/// <param name="OwnedByShell">It lives in the process that owns the shell window (<c>GetShellWindow</c>).</param>
/// <param name="HasList">It has its icon list (<c>SysListView32</c>).</param>
public readonly record struct DesktopViewCandidate(bool UnderProgman, bool OwnedByShell, bool HasList);

/// <summary>
/// [S-5503] [S-5504] Explorer's desktop icons, as pure rules.
/// <para>
/// Windows owns its "Show desktop icons" setting, not Themia: Themia never sends the shell's command
/// and never writes that setting. While it runs it hides Explorer's icon list for the session only
/// (the desktop is Themia's), and shows it again as Windows has it on quit.
/// </para>
/// </summary>
public static class DesktopIconRules
{
    /// <summary><c>WS_VISIBLE</c>.</summary>
    public const uint WsVisible = 0x10000000;

    /// <summary>
    /// [S-5503] Whether the icons are shown, from the icon list's <i>own</i> style. Explorer hides that
    /// window itself, so its <c>WS_VISIBLE</c> bit is the switch. Not <c>IsWindowVisible</c>: that
    /// also answers false while an ancestor (<c>Progman</c>, a <c>WorkerW</c>) is hidden for a moment.
    /// </summary>
    public static bool IsShown(uint listStyle) => (listStyle & WsVisible) != 0;

    /// <summary>
    /// [S-5503] Which found view is Explorer's desktop, or -1 for none. Only one of the shell's own process
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
    /// [S-5503] How often a held list may be shown again (by Explorer, a refresh, a third party)
    /// before Themia leaves it shown: a hide that never stays is someone else's, not a fight to win.
    /// </summary>
    public const int MaxRehides = 8;

    /// <summary>
    /// [S-5503] Whether a held list that was just shown again is hidden again: while the hide is held
    /// and fewer than <see cref="MaxRehides"/> re-hides were spent since the hold began.
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
}
