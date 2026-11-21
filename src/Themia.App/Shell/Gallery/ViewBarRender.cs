#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: draw the desktop view bar ([S-906]) on a stand-in display, at a set of anchors,
/// sizes and modes, one PNG each, off-screen.
/// <c>Themia.exe --render viewbar &lt;outDir&gt; [--views n] [--only &lt;prefix&gt;] [--theme light|dark]</c>.
/// <para>
/// The bar is the real <see cref="ViewBar"/>, fitted to the real room ([S-917]) and placed by the real
/// rule (<see cref="ViewBarRules.Place"/>) inside a work area that leaves a taskbar strip at the
/// bottom, so the pictures show both how a bar looks and where it lands. The edit-mode shots carry a
/// stand-in for the edit bar, to show a top bar moving out from under it.
/// </para>
/// <para>
/// [S-919] Without <c>--views</c> it draws four views at fifteen anchor/size/label/mode combinations
/// (<c>viewbar-&lt;case&gt;.png</c>). With <c>--views n</c> (1..60) it draws n views, the shown one
/// two thirds along, at every anchor with each of the three labels, and two edit-mode shots
/// (<c>viewbar-&lt;n&gt;v-&lt;theme&gt;-&lt;case&gt;.png</c>); where the bar overflows, the overflow
/// menu of the bottom-left shot is drawn too (<c>…-menu.png</c>, the popup alone).
/// </para>
/// <para>
/// [S-5404] Both sets draw the three labels ([S-5405]) and end with the select type ([S-5401]), each
/// shot closed and with its menu open (<c>…-select-…-menu.png</c>).
/// </para>
/// </summary>
internal static class ViewBarRender
{
    private const string Scope = "viewbar-render";

    private const double ScreenWidth = 960;
    private const double ScreenHeight = 540;
    private const double TaskbarHeight = 48;

    private sealed record Case(string Name, ViewBarAnchor Anchor, ViewBarSize Size, ViewBarLabels Labels, bool Editing, bool Hidden = false, ViewBarType Type = ViewBarType.Inline)
    {
        /// <summary>[S-5404] A select shot (and [S-13406] an arrows shot, its label's menu) is drawn closed and, beside it, with its menu open.</summary>
        public bool DrawsMenu => Type is ViewBarType.Select or ViewBarType.Arrows;
    }

    private const ViewBarLabels Both = ViewBarLabels.IconAndText;
    private const ViewBarLabels Icons = ViewBarLabels.IconOnly;
    private const ViewBarLabels Text = ViewBarLabels.TextOnly;

    /// <summary>[S-5404] The select bar with each of the three labels, a row and a side anchor, M to XL, and in edit mode.</summary>
    private static readonly Case[] SelectCases =
    [
        new("select-bottomleft-M", ViewBarAnchor.BottomLeft, ViewBarSize.Medium, Both, Editing: false, Type: ViewBarType.Select),
        new("select-topright-L-icons", ViewBarAnchor.TopRight, ViewBarSize.Large, Icons, Editing: false, Type: ViewBarType.Select),
        new("select-bottomcenter-XL-text", ViewBarAnchor.BottomCenter, ViewBarSize.ExtraLarge, Text, Editing: false, Type: ViewBarType.Select),
        new("select-left-XL", ViewBarAnchor.LeftCenter, ViewBarSize.ExtraLarge, Both, Editing: false, Type: ViewBarType.Select),
        new("select-right-M-text", ViewBarAnchor.RightCenter, ViewBarSize.Medium, Text, Editing: false, Type: ViewBarType.Select),
        new("select-topcenter-M-edit", ViewBarAnchor.TopCenter, ViewBarSize.Medium, Both, Editing: true, Type: ViewBarType.Select),
    ];

    /// <summary>
    /// [S-13406] The styles of [S-13400]: the dots, Previous / Next and the one button, each at a row and a side
    /// anchor, M to XL, with each label where it applies, and in edit mode.
    /// </summary>
    private static readonly Case[] StyleCases =
    [
        new("dots-bottomcenter-M", ViewBarAnchor.BottomCenter, ViewBarSize.Medium, Both, Editing: false, Type: ViewBarType.Dots),
        new("dots-bottomleft-L", ViewBarAnchor.BottomLeft, ViewBarSize.Large, Both, Editing: false, Type: ViewBarType.Dots),
        new("dots-left-XL", ViewBarAnchor.LeftCenter, ViewBarSize.ExtraLarge, Both, Editing: false, Type: ViewBarType.Dots),
        new("dots-topright-M-edit", ViewBarAnchor.TopRight, ViewBarSize.Medium, Both, Editing: true, Type: ViewBarType.Dots),
        new("arrows-bottomcenter-M", ViewBarAnchor.BottomCenter, ViewBarSize.Medium, Both, Editing: false, Type: ViewBarType.Arrows),
        new("arrows-bottomleft-L-icons", ViewBarAnchor.BottomLeft, ViewBarSize.Large, Icons, Editing: false, Type: ViewBarType.Arrows),
        new("arrows-topright-XL-text", ViewBarAnchor.TopRight, ViewBarSize.ExtraLarge, Text, Editing: false, Type: ViewBarType.Arrows),
        new("arrows-right-L", ViewBarAnchor.RightCenter, ViewBarSize.Large, Both, Editing: false, Type: ViewBarType.Arrows),
        new("arrows-topcenter-M-edit", ViewBarAnchor.TopCenter, ViewBarSize.Medium, Both, Editing: true, Type: ViewBarType.Arrows),
        new("cycle-bottomleft-M", ViewBarAnchor.BottomLeft, ViewBarSize.Medium, Both, Editing: false, Type: ViewBarType.Cycle),
        new("cycle-bottomcenter-L-icons", ViewBarAnchor.BottomCenter, ViewBarSize.Large, Icons, Editing: false, Type: ViewBarType.Cycle),
        new("cycle-topright-XL-text", ViewBarAnchor.TopRight, ViewBarSize.ExtraLarge, Text, Editing: false, Type: ViewBarType.Cycle),
        new("cycle-left-L", ViewBarAnchor.LeftCenter, ViewBarSize.Large, Both, Editing: false, Type: ViewBarType.Cycle),
        new("cycle-topcenter-M-edit", ViewBarAnchor.TopCenter, ViewBarSize.Medium, Both, Editing: true, Type: ViewBarType.Cycle),
    ];

    private static readonly Case[] Cases =
    [
        new("bottomleft-M", ViewBarAnchor.BottomLeft, ViewBarSize.Medium, Both, Editing: false),
        new("bottomcenter-L", ViewBarAnchor.BottomCenter, ViewBarSize.Large, Both, Editing: false),
        new("topright-XL", ViewBarAnchor.TopRight, ViewBarSize.ExtraLarge, Both, Editing: false),
        new("topleft-M-icons", ViewBarAnchor.TopLeft, ViewBarSize.Medium, Icons, Editing: false),
        new("bottomleft-M-text", ViewBarAnchor.BottomLeft, ViewBarSize.Medium, Text, Editing: false),
        new("bottomcenter-L-text", ViewBarAnchor.BottomCenter, ViewBarSize.Large, Text, Editing: false),
        new("topright-XL-text", ViewBarAnchor.TopRight, ViewBarSize.ExtraLarge, Text, Editing: false),
        new("left-L-vertical", ViewBarAnchor.LeftCenter, ViewBarSize.Large, Both, Editing: false),
        new("right-XL-vertical-icons", ViewBarAnchor.RightCenter, ViewBarSize.ExtraLarge, Icons, Editing: false),
        new("left-M-vertical-text", ViewBarAnchor.LeftCenter, ViewBarSize.Medium, Text, Editing: false),
        new("right-XL-vertical-text", ViewBarAnchor.RightCenter, ViewBarSize.ExtraLarge, Text, Editing: false),
        new("bottomright-M-edit", ViewBarAnchor.BottomRight, ViewBarSize.Medium, Both, Editing: true),
        new("bottomright-M-edit-text", ViewBarAnchor.BottomRight, ViewBarSize.Medium, Text, Editing: true),
        new("topcenter-L-edit-editBar", ViewBarAnchor.TopCenter, ViewBarSize.Large, Both, Editing: true),
        new("left-M-edit-hidden", ViewBarAnchor.LeftCenter, ViewBarSize.Medium, Both, Editing: true, Hidden: true),
        .. SelectCases,
        .. StyleCases,
    ];

    private static readonly ViewBarItem[] Items =
    [
        new("work", "Work", null, Shown: true, IsDefault: false),
        new("home", "Home office", Glyphs.Home, Shown: false, IsDefault: true, Color: "#FFB900"),
        new("games", "Gaming", Glyphs.Play, Shown: false, IsDefault: false),
        new("focus", "Deep focus", null, Shown: false, IsDefault: false, Color: "#E74856"),
    ];

    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "viewbar",
        "<outDir> [--views n] [--only <prefix>] [--theme light|dark]",
        """
        the desktop view bar [S-906] on a stand-in display, fitted to the real room [S-917] and placed by the real rule:
        four views at every anchor, size, label [S-5405] and mode (viewbar-<case>.png); --views n (1..60): n views at every
        anchor with each label, its overflow menu where it overflows [S-919] (viewbar-<n>v-<theme>-<case>.png); both end with the
        select type [S-5401] [S-5404] and the styles [S-13400] [S-13406], each with its menu open (…-menu.png);
        --only: the shots whose name starts with it (dots, arrows, cycle, select, ...)
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outDir = run.PathArg(0);
        int? many = int.TryParse(run.Option("--views"), out var n) ? Math.Clamp(n, 1, 60) : null;
        var theme = run.App.RequestedTheme == ApplicationTheme.Light ? "light" : "dark";

        System.IO.Directory.CreateDirectory(outDir);
        using var harness = RenderHarness.Open(run, "Themia view bar render", null, ScreenWidth, ScreenHeight);
        var screen = new Grid
        {
            Width = ScreenWidth,
            Height = ScreenHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = DesignApplier.Brush("SolidBackgroundFillColorTertiaryBrush", Colors.DimGray),
        };

        harness.Window.Window.Content = new Grid { Children = { screen } };
        UiLanguage.ApplyLanguageOnly(screen);

        IReadOnlyList<ViewBarItem> items = many is { } count ? ManyItems(count) : Items;
        IReadOnlyList<Case> cases = many is null ? Cases : ManyCases();

        // [S-13406] --only <prefix>: the shots whose name starts with it (dots, arrows, cycle, select, ...).
        if (run.Option("--only") is { } only)
        {
            cases = [.. cases.Where(c => c.Name.StartsWith(only, StringComparison.OrdinalIgnoreCase))];
        }

        var prefix = many is { } total ? $"viewbar-{total}v-{theme}-" : "viewbar-";

        foreach (var c in cases)
        {
            var (bar, area) = Draw(screen, c, items);
            await Task.Delay(400).ConfigureAwait(true);
            await RenderHarness.SaveAsync(screen, System.IO.Path.Combine(outDir, $"{prefix}{c.Name}.png"), "viewbar").ConfigureAwait(true);

            if (((many is not null && c.Name is "bottomleft-names" or "bottomleft-text") || c.DrawsMenu) && bar.MenuButton is { } chip)
            {
                await DrawMenuAsync(screen, bar, chip, area, c, System.IO.Path.Combine(outDir, $"{prefix}{c.Name}-menu.png")).ConfigureAwait(true);
            }
        }

        return RenderCommandLine.Passed;
    }

    /// <summary>[S-919] Every anchor with each of the three labels, the sizes taken in turn, then two edit-mode shots under the edit bar.</summary>
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
            cases.Add(new($"{name}-names", anchors[i], sizes[i % 3], Both, Editing: false));
            cases.Add(new($"{name}-icons", anchors[i], sizes[(i + 1) % 3], Icons, Editing: false));
            cases.Add(new($"{name}-text", anchors[i], sizes[(i + 2) % 3], Text, Editing: false));
        }

        cases.Add(new("topcenter-edit-names", ViewBarAnchor.TopCenter, ViewBarSize.Large, Both, Editing: true));
        cases.Add(new("rightcenter-edit-icons", ViewBarAnchor.RightCenter, ViewBarSize.ExtraLarge, Icons, Editing: true));
        cases.AddRange(SelectCases);
        cases.AddRange(StyleCases);
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

    /// <summary>One shot: the work area, the taskbar strip, the edit bar stand-in in edit mode, and the bar at its anchor.</summary>
    private static (ViewBar Bar, DipRect Area) Draw(Grid screen, Case c, IReadOnlyList<ViewBarItem> items)
    {
        screen.Children.Clear();

        var area = new DipRect(0, 0, ScreenWidth, ScreenHeight - TaskbarHeight);
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

        DipRect? editBar = null;
        if (c.Editing)
        {
            var stand = new DipRect((ScreenWidth - 460) / 2, Fluent.Space2, 460, EditBar.ButtonSize + (2 * Fluent.Space1) + (2 * Fluent.Hairline));
            editBar = stand;
            var box = new Border
            {
                Width = stand.Width,
                Height = stand.Height,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = DesignApplier.Brush("SurfaceStrokeColorDefaultBrush", Colors.Gray),
                Background = DesignApplier.Brush("AcrylicBackgroundFillColorDefaultBrush", Colors.Black),
                Child = new TextBlock { Text = "edit editBar", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 },
            };
            Canvas.SetLeft(box, stand.X);
            Canvas.SetTop(box, stand.Y);
            canvas.Children.Add(box);
        }

        var bar = new ViewBar(new NoOwner());
        UiLanguage.ApplyFlow(bar);
        bar.Update(
            items,
            new ViewBarLook(c.Size, c.Labels, ViewBarRules.IsVertical(c.Anchor, c.Type), c.Editing, c.Editing && c.Hidden, c.Type),
            new DesignState(),
            ViewBarRules.Room(area, c.Anchor, editBar, c.Type));
        canvas.Children.Add(bar);

        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (x, y) = ViewBarRules.Place(area, bar.DesiredSize.Width, bar.DesiredSize.Height, c.Anchor, editBar);
        Canvas.SetLeft(bar, x);
        Canvas.SetTop(bar, y);

        var outside = x < area.X || y < area.Y || x + bar.DesiredSize.Width > area.X + area.Width || y + bar.DesiredSize.Height > area.Y + area.Height;
        Console.WriteLine($"viewbar: {c.Name} bar {bar.DesiredSize.Width:0}x{bar.DesiredSize.Height:0} at {x:0},{y:0}{(outside ? " OUTSIDE THE WORK AREA" : string.Empty)}");
        return (bar, area);
    }

    /// <summary>[S-918] / [S-5402] The bar's menu as the surface opens it, drawn inside the window; the popup's content alone is written.</summary>
    private static async Task DrawMenuAsync(Grid screen, ViewBar bar, FrameworkElement chip, DipRect area, Case c, string path)
    {
        var box = new DipRect(Canvas.GetLeft(bar), Canvas.GetTop(bar), bar.ActualWidth, bar.ActualHeight);
        var side = ViewBarRules.MenuSide(c.Anchor);
        var cap = ViewBarRules.MenuHeight(area, box, side);

        var menu = ViewBar.ViewMenu(bar.MenuViews, c.Labels, _ => { }, cap);
        menu.ShouldConstrainToRootBounds = true;
        menu.ShowAt(chip, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Standard, Placement = ViewBar.MenuPlacement(side) });
        await Task.Delay(900).ConfigureAwait(true);

        var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(screen.XamlRoot);
        if (popups.Count > 0 && popups[0].Child is FrameworkElement content)
        {
            await RenderPng.SaveOnGroundAsync(content, path, RenderPng.MenuGround).ConfigureAwait(true);
            Console.WriteLine($"viewbar: wrote {path} ({content.ActualWidth:0}x{content.ActualHeight:0}, cap {cap:0}, {bar.MenuViews.Count} views)");
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

        public void ShowViewMenu(FrameworkElement button, IReadOnlyList<ViewBarItem> views, ViewBarLabels labels)
        {
        }
    }
}
#endif
