using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Desktop;

/// <summary>[S-5575] Who else is at the desktop's windows, as far as Themia can tell from them.</summary>
public enum DesktopHolder
{
    /// <summary>Explorer's desktop alone: Themia's choice applies as it is.</summary>
    None,

    /// <summary>
    /// Another program's window sits in the desktop's own windows (a child of <c>Progman</c> or of a
    /// <c>WorkerW</c>): a wallpaper engine (Wallpaper Engine, Lively) drawing the wallpaper. Benign:
    /// it is under the surface, as the wallpaper is.
    /// </summary>
    Wallpaper,

    /// <summary>
    /// Another program has a desktop view of its own (a <c>SHELLDLL_DefView</c> not of Explorer's
    /// process): something has replaced the desktop's icons. Themia does not show its items over it.
    /// </summary>
    Replaced,

    /// <summary>
    /// Something showed Explorer's hidden icon list again past the re-hide cap ([S-5503]): it holds the
    /// icons shown, and Themia, which never fights, does not show its items over them.
    /// </summary>
    KeptShown,
}

/// <summary>
/// [S-5575] What Themia reads of the desktop's windows to tell whether another program holds them.
/// </summary>
/// <param name="ForeignView">A desktop view (<c>SHELLDLL_DefView</c>) under <c>Progman</c> or a <c>WorkerW</c> belongs to a process that is not the shell's.</param>
/// <param name="ForeignChild">A window of another process (neither the shell's nor Themia's) is a child of <c>Progman</c> or of a <c>WorkerW</c>.</param>
/// <param name="KeptShown">The re-hide cap was reached since the choice was last applied.</param>
public readonly record struct DesktopShellFacts(bool ForeignView, bool ForeignChild, bool KeptShown);

/// <summary>
/// [S-5575] Living beside other desktop programs, as pure rules: who holds the desktop, what the
/// choice then does, and what ⚙ › On the desktop says about it. Themia never fights another program
/// for the desktop: no extra re-hide, no re-parenting, no message to its windows.
/// </summary>
public static class DesktopCoexistence
{
    /// <summary>[S-5575] Who holds the desktop: the icons kept shown first (it is what the user sees), then a replaced view, then a wallpaper.</summary>
    public static DesktopHolder Classify(DesktopShellFacts facts)
        => facts.KeptShown ? DesktopHolder.KeptShown
            : facts.ForeignView ? DesktopHolder.Replaced
            : facts.ForeignChild ? DesktopHolder.Wallpaper
            : DesktopHolder.None;

    /// <summary>
    /// [S-5575] What the desktop shows given the user's <paramref name="choice"/> and who holds it:
    /// Themia's items stand down (no item widget, nothing of Explorer's touched) while another program
    /// replaced the icons or keeps them shown, so two sets of icons never pile up; a wallpaper changes
    /// nothing; Windows' icons and Nothing are as chosen.
    /// </summary>
    public static bool ShowsItems(DesktopShows choice, DesktopHolder holder)
        => choice == DesktopShows.ThemiaItems && holder is DesktopHolder.None or DesktopHolder.Wallpaper;

    /// <summary>
    /// [S-5575] Whether Themia may hide Explorer's icon list for <paramref name="choice"/>: not while
    /// another program replaced the view (there is nothing of Explorer's to hide) or keeps the list shown.
    /// </summary>
    public static bool MayHide(DesktopShows choice, DesktopHolder holder)
        => choice != DesktopShows.WindowsIcons && holder is DesktopHolder.None or DesktopHolder.Wallpaper;

    /// <summary>[S-5575] The note under ⚙ › On the desktop saying why the choice does not show, or null when it does.</summary>
    public static string? Note(DesktopShows choice, DesktopHolder holder)
        => choice == DesktopShows.WindowsIcons ? null : holder switch
        {
            DesktopHolder.Replaced => Str.Desktop_Coexist_Replaced.Text(),
            DesktopHolder.KeptShown => Str.Desktop_Coexist_KeptShown.Text(),
            _ => null,
        };
}
