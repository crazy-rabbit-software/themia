#if DEBUG
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.Files;
using Themia.Core.Diagnostics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-5551] Debug only: <c>--desktop-menu-check &lt;out.txt&gt;</c> builds the desktop's background menu
/// as a right-click on empty desktop would (the desktop root's own menu with Themia's lines put in) and
/// writes its lines, never showing it on screen and never running a command: each line's words, its
/// canonical verb where the shell gives one, a submenu's mark, and which lines are Themia's. A Win32
/// menu cannot be drawn off-screen, so this text is its render.
/// </summary>
internal static class DesktopMenuCheck
{
    public const string Flag = "--desktop-menu-check";

    private const string Scope = "desktop-menu-check";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, $"usage: {Flag} <out.txt>");
            app.Exit();
            return true;
        }

        var outPath = Path.GetFullPath(args[at + 1]);
        _ = dispatcher.TryEnqueue(async () =>
        {
            IReadOnlyList<string> lines;
            try
            {
                lines = ShellContextMenu.DescribeDesktop(0, AppHost.DesktopMenuLines());
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the desktop menu check threw", ex);
                lines = ["threw: " + ex];
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error(Scope, $"could not write {outPath}", ex);
            }

            app.Exit();
        });

        return true;
    }
}
#endif
