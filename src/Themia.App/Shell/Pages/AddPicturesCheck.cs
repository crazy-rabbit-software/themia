using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Graphics;

namespace Themia.App.Shell;

/// <summary>
/// [S-11854] The one check a shipped build runs instead of the app: <c>Themia.exe --add-pictures-check
/// &lt;out.png&gt;</c> draws the add page's Start tab off-screen, every picture through the shipped path
/// ([S-11710]), writes the page to the PNG, prints how many pictures, their bytes and the time, and exits
/// (0 when every picture asked for was drawn). So a Release or Store build is seen to draw its pictures
/// without the Debug render tooling. Only as the first argument, with a <c>.png</c> path; it reads and
/// writes no state, opens no window on screen and never reaches the single-instance hand-off.
/// </summary>
internal static class AddPicturesCheck
{
    public const string Flag = "--add-pictures-check";

    private const string Scope = "add-pictures-check";
    private const string Usage = "usage: Themia.exe --add-pictures-check <out.png>";

    /// <summary>The page's size: a 1080p display's bar at 100 %, a tall work area.</summary>
    private const double PageWidth = 1360;
    private const double PageHeight = 900;

    /// <summary>A check is bounded: pictures not drawn by then are said, and the exit code says it failed.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    /// <summary>True when <paramref name="args"/> (the process's, the executable first) ask for the check, which then runs and exits.</summary>
    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length < 2 || !string.Equals(args[1], Flag, StringComparison.Ordinal))
        {
            return false;
        }

        if (args.Length != 3
            || !args[2].EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Console.Error.WriteLine(Usage);
            Environment.Exit(2);
            return true;
        }

        var outPath = Path.GetFullPath(args[2]);
        Log.AddSink(static record =>
        {
            if (record.Level >= LogLevel.Warn || record.Scope == "add-pictures")
            {
                Console.WriteLine($"check: {record.Level} {record.Scope}: {record.Message} {record.Detail}");
            }
        });

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            var exit = 1;
            try
            {
                var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(d => d.Id)], ReadyMadeGroups.Offered());
                var flyout = new AddPageFlyout(page, new AddPageCommands(_ => { }, () => { }, _ => false)) { Width = PageWidth, Height = PageHeight };
                var screen = new Grid { Width = PageWidth, Height = PageHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Children = { flyout } };
                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia add pictures check" };
                UiLanguage.Apply(screen);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((PageWidth + 40) * scale), (int)((PageHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                var wanted = page.Sections(AddFolderId.Start).SelectMany(s => s.Tiles).Where(t => t is not AddItemTile).Select(t => t.Key).Distinct(StringComparer.Ordinal).Count();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                screen.UpdateLayout();

                // The page asks for its pictures once it is laid out; they are done when all are kept.
                while (AddPictures.Held.Count < wanted && clock.Elapsed < Bound)
                {
                    await Task.WhenAny(flyout.View.PicturesDrawn, Task.Delay(250)).ConfigureAwait(true);
                }

                var finished = AddPictures.Held.Count >= wanted;
                var took = clock.ElapsedMilliseconds;
                screen.UpdateLayout();

                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                await PngFile.SaveAsync(screen, outPath).ConfigureAwait(true);
                var (count, bytes) = AddPictures.Held;
                Console.WriteLine($"check: Start tab: {count} of {wanted} pictures in {took} ms, {bytes / 1024} KB of pixels{(finished ? string.Empty : $" (cut short after {Bound.TotalSeconds:0} s)")}");
                Console.WriteLine($"check: wrote {outPath}");
                exit = finished && count == wanted ? 0 : 1;
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the add pictures check failed", ex);
                Console.Error.WriteLine($"check: failed: {ex.Message}");
            }
            finally
            {
                window?.Close();
                Environment.Exit(exit);
            }
        });

        return true;
    }
}
