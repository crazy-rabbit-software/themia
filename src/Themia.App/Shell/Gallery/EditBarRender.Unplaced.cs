#if DEBUG
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services.Files;
using Themia.Core.Geometry;
using Themia.Core.Settings;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-13700]..[S-13706] The Unplaced icons page in a render (<c>--editbar-png … [--only unplaced]</c>): the bar with its
/// entry's badge (<c>editbar-&lt;theme&gt;-unplaced.png</c>), and the page hung from its entry over sample items
/// (<c>page-&lt;theme&gt;-unplaced.png</c>: six, two selected, two fences; <c>-long</c>: fourteen with the search box and a
/// word typed; <c>-none</c>: every icon placed; <c>-noview</c>: a view that shows no desktop icons). The items are
/// stand-ins (no desktop is read); their icons are the shell's for their types.
/// </summary>
internal static partial class EditBarRender
{
    private const string SampleDesk = @"C:\Users\Sample\Desktop\";

    private static readonly DesktopItem[] SampleItems =
    [
        new("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "This PC", DesktopItemKind.ShellItem),
        new(SampleDesk + "Budget 2026.xlsx", "Budget 2026.xlsx", DesktopItemKind.File),
        new(SampleDesk + "Holiday photos", "Holiday photos", DesktopItemKind.Folder),
        new(SampleDesk + "Mission Planner.lnk", "Mission Planner", DesktopItemKind.Shortcut),
        new(SampleDesk + "Quarterly report.docx", "Quarterly report.docx", DesktopItemKind.File),
        new(SampleDesk + "notes.txt", "notes.txt", DesktopItemKind.File),
        new(SampleDesk + "Invoice 0412.pdf", "Invoice 0412.pdf", DesktopItemKind.File),
        new(SampleDesk + "Project Atlas", "Project Atlas", DesktopItemKind.Folder),
        new(SampleDesk + "Report draft 2.docx", "Report draft 2.docx", DesktopItemKind.File),
        new(SampleDesk + "Screenshot 2026-11-19.png", "Screenshot 2026-11-19.png", DesktopItemKind.File),
        new(SampleDesk + "Spotify.lnk", "Spotify", DesktopItemKind.Shortcut),
        new(SampleDesk + "Tax return.pdf", "Tax return.pdf", DesktopItemKind.File),
        new(SampleDesk + "Travel.xlsx", "Travel.xlsx", DesktopItemKind.File),
        new(SampleDesk + "Weekly report.pptx", "Weekly report.pptx", DesktopItemKind.File),
    ];

    /// <summary>[S-13700]..[S-13706] The bar's badge and the page in its four states.</summary>
    private static async Task UnplacedShots(Grid screen, string outDir, string theme)
    {
        var wide = ScreenWidth - (2 * ShellMetricsBarTop);
        await Shot(screen, Bar(new EditBarState(null, 0, null, false, wide) { Unplaced = 14 }), outDir, $"editbar-{theme}-unplaced.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(null, 0, null, false, 1600 - (2 * ShellMetricsBarTop)) { Unplaced = 3 }), outDir, $"editbar-{theme}-unplaced-wide.png", room: 1600).ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(null, 0, null, false, 900 - (2 * ShellMetricsBarTop)) { Unplaced = 120 }), outDir, $"editbar-{theme}-unplaced-narrow.png", room: 900).ConfigureAwait(true);

        (string Id, string Name)[] fences = [("f1", "Documents"), ("f2", "Projects")];
        var six = SampleItems.Take(6).ToArray();
        await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced.png"), new UnplacedModel("Home", true, DesktopItemOrder.Sort(six), fences),
            view => view.DebugSelect([six[1].Key, six[4].Key])).ConfigureAwait(true);
        await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced-long.png"), new UnplacedModel("Work", true, DesktopItemOrder.Sort(SampleItems), fences),
            view => view.DebugSearch("report")).ConfigureAwait(true);
        await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced-none.png"), new UnplacedModel("Home", true, [], []), null).ConfigureAwait(true);
        await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced-noview.png"), new UnplacedModel("Gaming", false, [], []), null).ConfigureAwait(true);
    }

    /// <summary>One picture: the page open from its entry (the badge counting <paramref name="model"/>'s items), then <paramref name="then"/>.</summary>
    private static async Task UnplacedShot(Grid screen, string path, UnplacedModel model, Action<UnplacedPageView>? then)
    {
        screen.Children.Clear();
        screen.Height = OpenHeight;
        var flat = screen.Background;
        screen.Background = Wallpaper();
        var canvas = new Canvas { Clip = new RectangleGeometry { Rect = new Rect(0, 0, ScreenWidth, OpenHeight) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        var bar = Bar(new EditBarState(null, 0, SettingsPageId.DesktopItems, false, ScreenWidth - (2 * ShellMetricsBarTop)) { Unplaced = model.Items.Count });
        var barRect = PlaceBar(canvas, bar, ScreenWidth);

        UnplacedPageView? part = null;
        var commands = new UnplacedCommands(() => model, _ => { }, (_, _) => { }, _ => { }, HostDesktopItems.Instance.IconAsync);
        var view = new PageView(() => PageRows.Of(SettingsPages.DesktopItems(), _ => { }, () => part = new UnplacedPageView(commands)));
        UiLanguage.ApplyFlow(view);
        var area = new PixelRect(0, 0, ScreenWidth, OpenHeight);
        var flyout = HangFlyout(canvas, view, bar, barRect, SettingsPageId.DesktopItems, area);
        view.Show();
        if (part is not null)
        {
            then?.Invoke(part);
        }
        else
        {
            Console.WriteLine($"editbar: {System.IO.Path.GetFileName(path)}: the page built no list");
        }

        await Task.Delay(300).ConfigureAwait(true);
        canvas.UpdateLayout();
        flyout.Place(FlyoutPlacement.Place(bar.EntryBox(SettingsPageId.DesktopItems, canvas) ?? barRect, barRect, area, flyout.ContentHeight));
        canvas.UpdateLayout();

        // The shell's icons come off the UI thread: given time to land before the picture.
        await Task.Delay(1500).ConfigureAwait(true);
        Probe(screen, flyout, path);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
        view.Close();
        screen.Background = flat;
    }
}
#endif
