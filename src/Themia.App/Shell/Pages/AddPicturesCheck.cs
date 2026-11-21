using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>
/// [S-11854] The one check a shipped build runs instead of the app: <c>Themia.exe --check add-pictures
/// &lt;out.png&gt;</c> draws the add page's Start off-screen, every picture through the shipped path
/// ([S-11710]), writes the page to the PNG, prints how many pictures, their bytes and the time, and exits
/// (0 when every picture asked for was drawn). So a Release or Store build is seen to draw its pictures
/// without the Debug render tooling. Only as the first argument, with a <c>.png</c> path; it reads and
/// writes no state, opens no window on screen and never reaches the single-instance hand-off.
/// </summary>
internal static class AddPicturesCheck
{
    private const string Scope = "add-pictures-check";

    /// <summary>The page's size: a 1080p display's bar at 100 %, a tall work area.</summary>
    private const double PageWidth = 1360;
    private const double PageHeight = 900;

    /// <summary>A check is bounded: pictures not drawn by then are said, and the exit code says it failed.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "add-pictures",
        "<out.png>",
        "the add page's Start drawn through the shipped path, every picture counted, PASS when all were drawn [S-11854]; every build",
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outPath = run.PathArg(0);
        if (run.Args.Count != 1 || !outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw run.Usage("one .png path");
        }

        Log.AddSink(static record =>
        {
            if (record.Level >= LogLevel.Warn || record.Scope == "add-pictures")
            {
                Console.WriteLine($"check: {record.Level} {record.Scope}: {record.Message} {record.Detail}");
            }
        });

        var report = new CheckReport(Scope, null);
        try
        {
            var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(d => d.Id)], ReadyMadeGroups.Offered());
            var flyout = new AddPageFlyout(page, new AddPageCommands(_ => { }, () => { }, _ => false)) { Width = PageWidth, Height = PageHeight };
            var screen = new Grid { Width = PageWidth, Height = PageHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Children = { flyout } };
            using var window = OffscreenWindow.Open("Themia add pictures check", new Grid { Children = { screen } }, PageWidth, PageHeight);
            UiLanguage.Apply(screen);

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
            report.Expect(finished && count == wanted, $"Start: {count} of {wanted} pictures in {took} ms, {bytes / 1024} KB of pixels{(finished ? string.Empty : $" (cut short after {Bound.TotalSeconds:0} s)")}");
            report.Say($"check: wrote {outPath}");
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }

        return await report.FinishAsync().ConfigureAwait(true);
    }
}
