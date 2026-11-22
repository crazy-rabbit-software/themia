#if DEBUG
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services.Files;
using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Settings;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-13700]..[S-13715] The Unplaced icons page in a render (<c>--render editbar … [--only unplaced]</c>): the bar with its
/// entry's badge (<c>editbar-&lt;theme&gt;-unplaced.png</c>), and the page as wide as the bar, hung from its entry, under its title
/// ([S-13711]), its icons the desktop's own icon view with its corner marks on the page's ground over the desktop's grid ([S-13712])
/// (<c>page-&lt;theme&gt;-unplaced.png</c>: six, two selected; <c>-band</c>: fourteen, the desktop's band drawn from empty room
/// halfway, [S-13714]; <c>-drop</c>: a desktop icon being dropped on the page, its picture under the pointer and the page lit,
/// [S-13713];
/// <c>-long</c>: fourteen; <c>-search</c>: fourteen with the search box and a word typed; <c>-none</c>: every icon
/// placed; <c>-noview</c>: a view that shows no desktop icons); the six and the fourteen again on a narrower display
/// (<c>-narrow</c>, a bar of 884) and at large icons (<c>-large</c>, <c>-large-narrow</c>), so the rows follow the bar's
/// width and the desktop's icon size. The items are stand-ins (no desktop is read); their icons are the shell's for their types.
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

    /// <summary>The narrower stand-in display: the rows hold fewer icons.</summary>
    private const double NarrowWidth = 900;

    /// <summary>[S-13700]..[S-13707] The bar's badge, and the page in its states at two bar widths and two icon sizes.</summary>
    private static async Task UnplacedShots(Grid screen, string outDir, string theme)
    {
        var wide = ScreenWidth - (2 * ShellMetricsBarTop);
        await Shot(screen, Bar(new EditBarState(null, 0, null, false, wide) { Unplaced = 14 }), outDir, $"editbar-{theme}-unplaced.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(null, 0, null, false, 1600 - (2 * ShellMetricsBarTop)) { Unplaced = 3 }), outDir, $"editbar-{theme}-unplaced-wide.png", room: 1600).ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(null, 0, null, false, 900 - (2 * ShellMetricsBarTop)) { Unplaced = 120 }), outDir, $"editbar-{theme}-unplaced-narrow.png", room: 900).ConfigureAwait(true);

        var six = SampleItems.Take(6).ToArray();
        var held = DesktopCell.Current;
        try
        {
            foreach (var (size, suffix) in new[] { (DesktopIconSize.Medium, string.Empty), (DesktopIconSize.Large, "-large") })
            {
                // [S-5593] The desktop's icon size, put in force as the host puts it: the page's icons follow it.
                DesktopCell.Apply(size);
                foreach (var (width, narrow) in new[] { (ScreenWidth, string.Empty), (NarrowWidth, "-narrow") })
                {
                    await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced{suffix}{narrow}.png"), new UnplacedModel(true, DesktopItemOrder.Sort(six)),
                        view => view.DebugSelect([six[1].Key, six[4].Key]), width).ConfigureAwait(true);
                    await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced{suffix}{narrow}-long.png"), new UnplacedModel(true, DesktopItemOrder.Sort(SampleItems)),
                        null, width).ConfigureAwait(true);
                }
            }

            DesktopCell.Apply(DesktopIconSize.Medium);
            await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced-search.png"), new UnplacedModel(true, DesktopItemOrder.Sort(SampleItems)),
                view => view.DebugSearch("report"), ScreenWidth).ConfigureAwait(true);

            // [S-13714] The desktop's band from empty room, halfway over the icons: what it touches selected as it goes.
            await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced-band.png"), new UnplacedModel(true, DesktopItemOrder.Sort(SampleItems)),
                null, ScreenWidth, late: (view, _, _) =>
                {
                    view.DebugBand((0.005, 0.04), (0.33, 0.62));
                    return null;
                }).ConfigureAwait(true);

            // [S-13713] A desktop icon moved onto the page: the move's own picture of it under the pointer, the page lit as a drop target.
            var dropped = DesktopItemOrder.Sort(six);
            await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced-drop.png"), new UnplacedModel(true, dropped),
                null, ScreenWidth, late: (view, model, spot) =>
                {
                    var held = dropped[^1];
                    var picture = view.DebugMovePicture(held.Key);
                    if (picture is null)
                    {
                        Console.WriteLine("editbar: the held icon drew no picture");
                    }

                    view.DropOver(true);
                    return (model with { Items = [.. dropped.SkipLast(1)] }, picture, new Point(spot.X + (spot.Width * 0.42), spot.Y + (spot.Height * 0.55)));
                }).ConfigureAwait(true);
            await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced-none.png"), new UnplacedModel(true, []), null, ScreenWidth).ConfigureAwait(true);
            await UnplacedShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-unplaced-noview.png"), new UnplacedModel(false, []), null, ScreenWidth).ConfigureAwait(true);
        }
        finally
        {
            DesktopCell.Apply(held);
        }
    }

    /// <summary>
    /// One picture on a display <paramref name="width"/> wide: the page open from its entry as wide as the bar (the badge
    /// counting <paramref name="model"/>'s items), its icons in the desktop's box on that display, then <paramref name="then"/>.
    /// </summary>
    /// <param name="late">
    /// Once the icons are drawn: a further hand on the page (the band, a drop), given the model shown and where the page stands; it
    /// may hand back the model shown next, a drag's picture and the pointer it is held under.
    /// </param>
    private static async Task UnplacedShot(
        Grid screen,
        string path,
        UnplacedModel model,
        Action<UnplacedPageView>? then,
        double width,
        Func<UnplacedPageView, UnplacedModel, FlyoutSpot, (UnplacedModel Next, Themia.Core.Files.DragPicture? Picture, Point Pointer)?>? late = null)
    {
        screen.Children.Clear();
        screen.Height = OpenHeight;
        screen.Width = width;
        var flat = screen.Background;
        screen.Background = Wallpaper();
        var canvas = new Canvas { Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, OpenHeight) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        // [S-13707] The desktop's own box for an icon on this display at the icon size in force.
        var display = new ScreenMetrics(width, OpenHeight);
        model = model with
        {
            Box = UnplacedItems.Dips(DesktopGrid.IconSize(DesktopCell.Grid(DesktopCell.Current), display), display),
            Title = UnplacedItems.Title("Home"),
            Pitch = UnplacedItems.Pitch(display),
        };

        var bar = Bar(new EditBarState(null, 0, SettingsPageId.DesktopItems, false, width - (2 * ShellMetricsBarTop)) { Unplaced = model.Items.Count });
        var barRect = PlaceBar(canvas, bar, width);

        UnplacedPageView? part = null;
        var commands = new UnplacedCommands(() => model, _ => { }, _ => { }, HostDesktopItems.Instance.IconAsync);
        var view = new PageView(() => PageRows.Of(SettingsPages.DesktopItems(), () => part = new UnplacedPageView(commands)));
        UiLanguage.ApplyFlow(view);
        var area = new DipRect(0, 0, width, OpenHeight);
        var flyout = new PageFlyout(view);
        canvas.Children.Add(flyout);
        view.Show();
        if (part is not null)
        {
            then?.Invoke(part);
        }
        else
        {
            Console.WriteLine($"editbar: {System.IO.Path.GetFileName(path)}: the page built no list");
        }

        var name = System.IO.Path.GetFileName(path);
        await RenderHarness.SettleAsync(canvas, name).ConfigureAwait(true);
        canvas.UpdateLayout();
        var spot = FlyoutPlacement.Wide(bar.EntryBox(SettingsPageId.DesktopItems, canvas) ?? barRect, barRect, area, flyout.ContentHeightAt(barRect.Width));
        flyout.Place(spot);
        canvas.UpdateLayout();
        Console.WriteLine($"editbar: {System.IO.Path.GetFileName(path)}: flyout {spot.Width} wide (bar {barRect.Width}), icon box {model.Box.Width:0.#}x{model.Box.Height:0.#}");

        // The shell's icons come off the UI thread: each is a load the settle waits for (LoadWatch).
        await RenderHarness.SettleAsync(screen, name).ConfigureAwait(true);
        if (part is not null && late?.Invoke(part, model, spot) is { } next)
        {
            model = next.Next;
            part.Refresh();
            if (next.Picture is { } picture)
            {
                var image = new Image { IsHitTestVisible = false, Stretch = Stretch.Fill };
                DesktopWindow.Draw(image, picture);
                var (hotX, hotY) = picture.Anchor;
                Canvas.SetLeft(image, next.Pointer.X - hotX);
                Canvas.SetTop(image, next.Pointer.Y - hotY);
                canvas.Children.Add(image);
            }

            canvas.UpdateLayout();
            await RenderHarness.SettleAsync(screen, name).ConfigureAwait(true);
        }

        Probe(screen, flyout, path);
        await RenderHarness.SaveAsync(screen, path, "editbar").ConfigureAwait(true);
        view.Close();
        screen.Background = flat;
        screen.Width = ScreenWidth;
    }
}
#endif
