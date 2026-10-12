#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Interop;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: draw the desktop view bar ([S-906]) on a stand-in display, at a set of anchors,
/// sizes and modes, one PNG each, off-screen.
/// <c>Themia.exe --viewbar-png &lt;outDir&gt; [--views n] [--theme light|dark]</c>.
/// <para>
/// The bar is the real <see cref="ViewBar"/>, fitted to the real room ([S-917]) and placed by the real
/// rule (<see cref="ViewBarRules.Place"/>) inside a work area that leaves a taskbar strip at the
/// bottom, so the pictures show both how a bar looks and where it lands. The edit-mode shots carry a
/// stand-in for the edit toolbar, to show a top bar moving out from under it.
/// </para>
/// <para>
/// [S-919] Without <c>--views</c> it draws four views at nine anchor/size/mode combinations
/// (<c>viewbar-&lt;case&gt;.png</c>). With <c>--views n</c> (1..60) it draws n views, the shown one
/// two thirds along, at every anchor with names and icon-only, and two edit-mode shots
/// (<c>viewbar-&lt;n&gt;v-&lt;theme&gt;-&lt;case&gt;.png</c>); where the bar overflows, the overflow
/// menu of the bottom-left shot is drawn too (<c>…-menu.png</c>, the popup alone).
/// </para>
/// </summary>
internal static class ViewBarRender
{
    private const string Scope = "viewbar-render";

    private const double ScreenWidth = 960;
    private const double ScreenHeight = 540;
    private const double TaskbarHeight = 48;

    private sealed record Case(string Name, ViewBarAnchor Anchor, ViewBarSize Size, bool IconOnly, bool Editing, bool Hidden = false);

    private static readonly Case[] Cases =
    [
        new("bottomleft-M", ViewBarAnchor.BottomLeft, ViewBarSize.Medium, IconOnly: false, Editing: false),
        new("bottomcenter-L", ViewBarAnchor.BottomCenter, ViewBarSize.Large, IconOnly: false, Editing: false),
        new("topright-XL", ViewBarAnchor.TopRight, ViewBarSize.ExtraLarge, IconOnly: false, Editing: false),
        new("topleft-M-icons", ViewBarAnchor.TopLeft, ViewBarSize.Medium, IconOnly: true, Editing: false),
        new("left-L-vertical", ViewBarAnchor.LeftCenter, ViewBarSize.Large, IconOnly: false, Editing: false),
        new("right-XL-vertical-icons", ViewBarAnchor.RightCenter, ViewBarSize.ExtraLarge, IconOnly: true, Editing: false),
        new("bottomright-M-edit", ViewBarAnchor.BottomRight, ViewBarSize.Medium, IconOnly: false, Editing: true),
        new("topcenter-L-edit-toolbar", ViewBarAnchor.TopCenter, ViewBarSize.Large, IconOnly: false, Editing: true),
        new("left-M-edit-hidden", ViewBarAnchor.LeftCenter, ViewBarSize.Medium, IconOnly: false, Editing: true, Hidden: true),
    ];

    private static readonly ViewBarItem[] Items =
    [
        new("work", "Work", null, Shown: true, IsDefault: false),
        new("home", "Home office", "", Shown: false, IsDefault: true, Color: "#FFB900"),
        new("games", "Gaming", "", Shown: false, IsDefault: false),
        new("focus", "Deep focus", null, Shown: false, IsDefault: false, Color: "#E74856"),
    ];

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--viewbar-png");
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "usage: --viewbar-png <outDir> [--views n] [--theme light|dark]");
            app.Exit();
            return true;
        }

        var outDir = System.IO.Path.GetFullPath(args[at + 1]);
        var viewsAt = Array.IndexOf(args, "--views");
        int? many = viewsAt >= 0 && viewsAt + 1 < args.Length && int.TryParse(args[viewsAt + 1], out var n) ? Math.Clamp(n, 1, 60) : null;
        var theme = app.RequestedTheme == ApplicationTheme.Light ? "light" : "dark";

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;

            try
            {
                System.IO.Directory.CreateDirectory(outDir);
                EditBarRender.ApplyDesignTheme(app);

                var screen = new Grid
                {
                    Width = ScreenWidth,
                    Height = ScreenHeight,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = DesignApplier.Brush("SolidBackgroundFillColorTertiaryBrush", Colors.DimGray),
                };

                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia view bar render" };
                UiLanguage.ApplyLanguageOnly(screen);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((ScreenWidth + 40) * scale), (int)((ScreenHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                IReadOnlyList<ViewBarItem> items = many is { } count ? ManyItems(count) : Items;
                IReadOnlyList<Case> cases = many is null ? Cases : ManyCases();
                var prefix = many is { } total ? $"viewbar-{total}v-{theme}-" : "viewbar-";

                foreach (var c in cases)
                {
                    var (bar, area) = Draw(screen, c, items);
                    await Task.Delay(400).ConfigureAwait(true);

                    var path = System.IO.Path.Combine(outDir, $"{prefix}{c.Name}.png");
                    await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
                    Console.WriteLine($"viewbar: wrote {path}");

                    if (many is not null && c.Name == "bottomleft-names" && bar.OverflowChip is { } chip)
                    {
                        await DrawMenuAsync(screen, bar, chip, area, c.Anchor, System.IO.Path.Combine(outDir, $"{prefix}{c.Name}-menu.png")).ConfigureAwait(true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the view bar failed", ex);
            }
            finally
            {
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    /// <summary>[S-919] Every anchor with names and icon-only, the sizes taken in turn, then two edit-mode shots under the toolbar.</summary>
    private static List<Case> ManyCases()
    {
        ViewBarAnchor[] anchors =
        [
            ViewBarAnchor.TopLeft, ViewBarAnchor.TopCenter, ViewBarAnchor.TopRight, ViewBarAnchor.LeftCenter,
            ViewBarAnchor.RightCenter, ViewBarAnchor.BottomLeft, ViewBarAnchor.BottomCenter, ViewBarAnchor.BottomRight,
        ];
        ViewBarSize[] sizes = [ViewBarSize.Medium, ViewBarSize.Large, ViewBarSize.ExtraLarge];

        var cases = new List<Case>();
        for (var i = 0; i < anchors.Length; i++)
        {
            var name = anchors[i].ToString().ToLowerInvariant();
            cases.Add(new($"{name}-names", anchors[i], sizes[i % 3], IconOnly: false, Editing: false));
            cases.Add(new($"{name}-icons", anchors[i], sizes[(i + 1) % 3], IconOnly: true, Editing: false));
        }

        cases.Add(new("topcenter-edit-names", ViewBarAnchor.TopCenter, ViewBarSize.Large, IconOnly: false, Editing: true));
        cases.Add(new("rightcenter-edit-icons", ViewBarAnchor.RightCenter, ViewBarSize.ExtraLarge, IconOnly: true, Editing: true));
        return cases;
    }

    /// <summary>n views, long and short names, some with icons and colours, the shown one two thirds along.</summary>
    private static ViewBarItem[] ManyItems(int count)
    {
        string[] names =
        [
            "Focus", "Gaming", "Morning routine and news", "Travel", "Home office", "Weekend",
            "Studio", "Deep work: no distractions at all", "Reading", "Family", "Music", "Finance",
            "Fitness", "Side project", "Calendar overview", "Night", "Kids", "Photos", "Build status",
            "Stocks", "Shopping", "Notes", "Inbox zero", "Presentation mode", "Q",
        ];
        string[] icons = [Glyphs.Calendar, Glyphs.Mail, Glyphs.Sun, Glyphs.Moon, Glyphs.Play];
        string[] colours = ["#E74856", "#0099BC", "#FFB900", "#8764B8", "#10893E"];
        var shown = count * 2 / 3;

        return
        [
            .. Enumerable.Range(0, count).Select(i => new ViewBarItem(
                $"v{i}",
                names[i % names.Length],
                i % 3 == 0 ? icons[i / 3 % icons.Length] : null,
                Shown: i == shown,
                IsDefault: i == 1,
                Color: i % 2 == 0 ? colours[i / 2 % colours.Length] : null)),
        ];
    }

    /// <summary>One shot: the work area, the taskbar strip, the toolbar stand-in in edit mode, and the bar at its anchor.</summary>
    private static (ViewBar Bar, PixelRect Area) Draw(Grid screen, Case c, IReadOnlyList<ViewBarItem> items)
    {
        screen.Children.Clear();

        var area = new PixelRect(0, 0, ScreenWidth, ScreenHeight - TaskbarHeight);
        var canvas = new Canvas();
        screen.Children.Add(canvas);

        var taskbar = new Rectangle
        {
            Width = ScreenWidth,
            Height = TaskbarHeight,
            Fill = DesignApplier.Brush("SolidBackgroundFillColorBaseBrush", Colors.Black),
        };
        Canvas.SetTop(taskbar, ScreenHeight - TaskbarHeight);
        canvas.Children.Add(taskbar);

        PixelRect? toolbar = null;
        if (c.Editing)
        {
            var stand = new PixelRect((ScreenWidth - 460) / 2, Fluent.Space2, 460, EditBar.ButtonSize + (2 * Fluent.Space1) + (2 * Fluent.Hairline));
            toolbar = stand;
            var box = new Border
            {
                Width = stand.Width,
                Height = stand.Height,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = DesignApplier.Brush("SurfaceStrokeColorDefaultBrush", Colors.Gray),
                Background = DesignApplier.Brush("AcrylicBackgroundFillColorDefaultBrush", Colors.Black),
                Child = new TextBlock { Text = "edit toolbar", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 },
            };
            Canvas.SetLeft(box, stand.X);
            Canvas.SetTop(box, stand.Y);
            canvas.Children.Add(box);
        }

        var bar = new ViewBar(new NoOwner());
        UiLanguage.ApplyFlow(bar);
        bar.Update(
            items,
            new ViewBarLook(c.Size, c.IconOnly, ViewBarRules.IsVertical(c.Anchor), c.Editing, c.Editing && c.Hidden),
            new DesignState(),
            ViewBarRules.Room(area, c.Anchor, toolbar));
        canvas.Children.Add(bar);

        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (x, y) = ViewBarRules.Place(area, bar.DesiredSize.Width, bar.DesiredSize.Height, c.Anchor, toolbar);
        Canvas.SetLeft(bar, x);
        Canvas.SetTop(bar, y);

        var outside = x < area.X || y < area.Y || x + bar.DesiredSize.Width > area.X + area.Width || y + bar.DesiredSize.Height > area.Y + area.Height;
        Console.WriteLine($"viewbar: {c.Name} bar {bar.DesiredSize.Width:0}x{bar.DesiredSize.Height:0} at {x:0},{y:0}{(outside ? " OUTSIDE THE WORK AREA" : string.Empty)}");
        return (bar, area);
    }

    /// <summary>[S-918] The overflow menu as the surface opens it, drawn inside the window; the popup's content alone is written.</summary>
    private static async Task DrawMenuAsync(Grid screen, ViewBar bar, FrameworkElement chip, PixelRect area, ViewBarAnchor anchor, string path)
    {
        var box = new PixelRect(Canvas.GetLeft(bar), Canvas.GetTop(bar), bar.ActualWidth, bar.ActualHeight);
        var side = ViewBarRules.MenuSide(anchor);
        var cap = ViewBarRules.MenuHeight(area, box, side);

        var menu = ViewBar.OverflowMenu(bar.Hidden, _ => { }, cap);
        menu.ShouldConstrainToRootBounds = true;
        menu.ShowAt(chip, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Standard, Placement = FlyoutPlacementMode.Top });
        await Task.Delay(900).ConfigureAwait(true);

        var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(screen.XamlRoot);
        if (popups.Count > 0 && popups[0].Child is FrameworkElement content)
        {
            await RenderPng.SaveAsync(content, path).ConfigureAwait(true);
            Console.WriteLine($"viewbar: wrote {path} ({content.ActualWidth:0}x{content.ActualHeight:0}, cap {cap:0}, {bar.Hidden.Count} views)");
        }
        else
        {
            Log.Error(Scope, $"no overflow menu opened for {path}");
        }

        menu.Hide();
        await Task.Delay(300).ConfigureAwait(true);
    }

    /// <summary>The render takes no clicks.</summary>
    private sealed class NoOwner : IViewBarOwner
    {
        public void SwitchTo(string viewId)
        {
        }

        public void CommitReorder(string draggedId, string targetId)
        {
        }

        public void ShowChipMenu(FrameworkElement chip, string viewId, RightTappedRoutedEventArgs args)
        {
        }

        public void BarDragMoved(Point at)
        {
        }

        public void BarDragEnded(Point at, bool moved)
        {
        }

        public void ShowOverflow(FrameworkElement chip, IReadOnlyList<ViewBarItem> hidden)
        {
        }
    }
}
#endif
