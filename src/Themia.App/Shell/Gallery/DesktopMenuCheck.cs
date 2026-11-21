#if DEBUG
using Themia.App.Services;
using Themia.App.Services.Files;
using Themia.Core.Shell;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-5548] Debug only: <c>Themia.exe --check desktop-menu &lt;out.txt&gt;</c> writes the desktop's background menus
/// as a right-click on empty desktop would build them, never showing one on screen and never running a
/// command: first the Windows 11 menu's lines (<see cref="Themia.Core.Desktop.DesktopBackground.Menu"/>,
/// worded, with the shell's New file types), then, under "Show more options", the shell's classic menu
/// (the desktop root's own): each line's words, its canonical verb where the shell gives one, a
/// submenu's mark and a check mark ([x]). A Win32 menu cannot be drawn off-screen, so this text is its
/// render; the Windows 11 menu is also drawn by <c>--render menu</c>.
/// </summary>
internal static class DesktopMenuCheck
{
    private const string Scope = "desktop-menu-check";

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "desktop-menu",
        "<out.txt>",
        """
        the desktop's background menus as a right-click on empty desktop builds them, never shown, no command run: the
        Windows 11 menu's lines (worded, the shell's New file types) and, under Show more options, the shell's classic menu
        (words, verbs, submenus, check marks): a Win32 menu's render as text [S-5548]
        """,
        run => RunAsync(run.PathArg(0)));

    private static async Task<int> RunAsync(string outPath)
    {
        var report = new CheckReport(Scope, outPath);
        try
        {
            var templates = await ShellNewTemplates.GetAsync().ConfigureAwait(true);
            var modern = AppHost.DesktopBackgroundEntries(
                Themia.Core.Desktop.DesktopBackground.Menu(new(ClipboardHasFiles: true, ShellFileOps.TerminalPath() is not null)),
                templates,
                static _ => { },
                static _ => { });
            var lines = new List<string> { "Windows 11 menu (the clipboard holding files)" };
            Describe(modern, 1, lines);
            lines.Add("Show more options: the shell's classic menu");
            lines.AddRange(ShellContextMenu.DescribeDesktop(0).Select(line => "  " + line));
            lines.ForEach(report.Say);
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }

        return await report.FinishAsync().ConfigureAwait(true);
    }

    /// <summary>The lines of a Windows 11 menu as text: a check mark, the words, the key, a submenu's mark and its lines.</summary>
    private static void Describe(IReadOnlyList<Themia.App.Widgets.ItemMenuEntry> entries, int depth, List<string> lines)
    {
        var indent = new string(' ', depth * 2);
        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
            {
                lines.Add(indent + "----");
                continue;
            }

            // [S-5593] [S-5596] One of a set carries Windows' dot, a toggle its check mark.
            var check = entry.Checked != true ? string.Empty : entry.Group is null ? "[x] " : "(o) ";
            var keys = entry.Shortcut is { } chord ? "  (" + Themia.Core.Shell.KeyChordText.Format(chord) + ")" : string.Empty;
            lines.Add($"{indent}{check}{entry.Text}{keys}{(entry.Children is null ? string.Empty : "  ›")}");
            if (entry.Children is { } children)
            {
                Describe(children, depth + 1, lines);
            }
        }
    }
}
#endif
