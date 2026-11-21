#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1917] Debug only: draw one widget, in a real <see cref="WidgetCard"/> on a preview host, to a
/// PNG, off-screen. <c>Themia.exe --render widget &lt;Kind&gt; &lt;out.png&gt; [w h] [--theme light|dark]
/// [--state name] [--span s] [--style name] [--chart area|line|bars|candles] [--detail simple|detailed] [--scale f] [--align left|center|right] [--background fill] [--own-items fill] [--own-item-text #RRGGBB] [--transparent] [--background-image path] [--machine sample] [--config json]</c> ([S-1931]: the system widgets on the sample machine, not this PC) ([S-6006]: any styled kind, in one of its styles). Without a size the card is drawn at its kind's min, natural and max
/// (<see cref="SizeFit.CheckedAt"/>, [S-2448]), labelled, side by side on one page; with one, once at that size. The widget has its
/// kind's default config; the host is a preview, so nothing it does reaches the machine.
/// <c>--reference</c> adds, beside the first card, a stock WinUI card at the same size (a Border with
/// the framework's own CardBackgroundFillColorDefault / CardStrokeColorDefault over
/// SolidBackgroundFillColorBase, OverlayCornerRadius) to compare pixels against ([S-836]).
/// <c>--wallpaper &lt;path&gt;</c> ([S-2119]) draws the cards frosted over that picture, the page being
/// the screen: the page shows the picture itself behind them (placed as Windows' Fill) unless
/// <c>--transparent</c>.
/// </summary>
internal static class WidgetRender
{
    private const string Scope = "widget-render";

    /// <summary>What follows <c>--render widget</c>.</summary>
    internal const string Usage = "<Kind> <out.png> [w h] [--theme light|dark] [--state name] [--style name] [--sizes WxH,..|range|steps] [--keys k1,k2|text:abc] [--design k=v,..] [--chart area|line|bars|candles] [--detail simple|detailed] [--trend none|arrow|line] [--span s] [--config json] [--scale f] [--align left|center|right] [--background fill] [--own-items fill] [--own-item-text #RRGGBB] [--background-image path] [--wallpaper path] [--transparent] [--place-cleared] [--machine sample] [--reference] [--tree] [--layout-json out.json] [--hover fx,fy] [--pressed fx,fy]";

    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "widget",
        Usage,
        """
        one widget in a real card on a preview host [S-1917]: at its min, natural and max side by side, or at w h, or --sizes (range, steps: cell by cell round its natural)
        --state: a preview state (a wrong one prints the kind's states; --render states <Kind>); --style: a styled kind's look [S-6006]
        --design border=dashed,radius=4,preset=Midnight,vibrancy=1,...: any look token by its key [S-2142]; --chart/--detail: a graph's chart look [S-6752]
        --trend/--span: a single metric drawing that trend, its graph over that span [S-14712]; --config <json>: a config fragment over the kind's default [S-14962]
        --transparent: no page behind the cards (the PNG keeps their alpha); --wallpaper: the page is a screen with that wallpaper [S-2119]
        --machine sample: the system widgets read the sample laptop, not this PC [S-1931]; --place-cleared: a place widget with its place cleared [S-2848]
        --layout-json: every card's item boxes, grid pitch and ink outside its items [S-12831]; --hover/--pressed fx,fy: the item at that point under the pointer
        scripts/item-check.py and scripts/gap-check.py judge the kinds against docs/WIDGET-RULES.md (exit 1 on a finding)
        """,
        async run => await Render(run, run.Args).ConfigureAwait(true) ? RenderCommandLine.Passed : RenderCommandLine.Failed);

    /// <summary>[S-6569] The cards' own background (<c>--background</c>), Default to follow the design.</summary>
    private static FillChoice _background = new();

    /// <summary>[S-6590] The cards' own background image (<c>--background-image</c>), null for none.</summary>
    private static string? _backgroundImage;

    /// <summary>[S-6535] The cards' own content scale (<c>--scale 1.5</c>), as stored.</summary>
    private static double _scale = 1.0;

    /// <summary>[S-6586] The cards' own alignment, or null for the kind's default.</summary>
    private static ContentAlign? _align;
    private static int? _span;

    // [S-6752] --chart area|line|bars|candles and --detail simple|detailed: a graph kind in that chart look.
    private static ChartType? _chart;
    private static ChartDetail? _detail;
    private static MetricTrend? _trend;

    // [S-14962] --config <json>: a fragment of the kind's config merged over its default (a network card's adapter).
    private static string? _fragment;

    /// <summary>[S-6547] The cards' own item colour and item text colour (<c>--own-items</c>, <c>--own-item-text</c>), empty to follow the design.</summary>
    private static FillChoice _ownItems = new();

    private static string _ownItemText = string.Empty;

    /// <summary>[S-14340] The arguments of the render being drawn (a batch job's, or the process's), for what reads them per card.</summary>
    private static string[] _args = Environment.GetCommandLineArgs();

    /// <summary>Space between frames and around the page, in DIPs.</summary>
    private const double Gap = 24;

    /// <summary>Height reserved above each frame for its size label.</summary>
    private const double LabelHeight = 22;

    /// <summary>
    /// [S-1917] One render, on the UI thread: the widget drawn to its PNG (and what else <paramref name="args"/> ask for),
    /// then its window closed and its services disposed. Never throws: a failure is logged and printed. True when the
    /// PNG was written. [S-14340] The batch draws each of its jobs through it, one after another in one process, so a
    /// job's picture is the one its own <c>--widget-png</c> process draws.
    /// </summary>
    internal static async Task<bool> Render(RenderRun run, IReadOnlyList<string> jobArgs)
    {
        string[] args = [.. jobArgs];
        if (args.Length < 2 || StartArguments.LooksLikeFlag(args[0]) || StartArguments.LooksLikeFlag(args[1]))
        {
            throw run.Usage("a kind and a PNG path");
        }

        if (!ModuleCatalog.TryFind(args[0], out var picked))
        {
            throw run.Usage($"no kind '{args[0]}' (Themia.exe --render catalog <out.json> lists them)");
        }

        // [S-14340] What a render reads per card, and the stand-in device location a job before may have set, are this render's.
        _args = args;
        AppServices.DebugDeviceLocation = null;

        var kind = picked.Id;
        var outPath = System.IO.Path.GetFullPath(args[1]);
        var state = RenderCommandLine.Option(args, "--state");

        // [S-6569] --background default|transparent|#RRGGBB[@percent]: the cards' own background.
        _background = DesignText.Fill(RenderCommandLine.Option(args, "--background")) ?? new FillChoice();

        // [S-6535] --scale 0.75|1.5: the widget's own content scale, through the same path as on the desktop.
        _scale = RenderCommandLine.Option(args, "--scale") is { } scaleText
            && double.TryParse(scaleText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scaleValue)
                ? WidgetScale.Normalise(scaleValue)
                : 1.0;

        // [S-6586] --align left|center|right: where the cards draw their content (a kind or style
        // that does not align draws its default, as on the desktop).
        _align = RenderCommandLine.Option(args, "--align") is { } alignText && Enum.TryParse<ContentAlign>(alignText, ignoreCase: true, out var alignValue)
            && Enum.IsDefined(alignValue)
                ? alignValue
                : null;

        // [S-6547] The widget's own item layer, over the app-wide one --design sets: the per-widget override.
        _ownItems = DesignText.Fill(RenderCommandLine.Option(args, "--own-items")) ?? new FillChoice();
        _ownItemText = HexColor.Normalise(RenderCommandLine.Option(args, "--own-item-text"));

        // [S-6590] --background-image <path>: the cards' own background image, a picture file's full path.
        _backgroundImage = RenderCommandLine.Option(args, "--background-image") is { } imagePath && BackgroundImage.IsImagePath(imagePath) ? imagePath : null;

        // [S-6006] Any styled kind in one of its styles; a wrong name prints the kind's styles.
        int? style = null;
        if (RenderCommandLine.Option(args, "--style") is { } styleName)
        {
            if (WidgetStyles.Find(kind, styleName) is { } found)
            {
                style = found.Value;
            }
            else
            {
                Console.WriteLine($"widget: {kind} has no style '{styleName}' (has: {string.Join(", ", WidgetStyles.For(kind).Select(s => s.Name))})");
            }
        }
        // [S-1829] --span seconds: a meter's chart span, to see a folded chart (an hour on a tile).
        _span = RenderCommandLine.Option(args, "--span") is { } spanText && int.TryParse(spanText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var spanValue)
            ? spanValue
            : null;

        // [S-2448] A kind is drawn at its range (min, natural, max) unless sizes are asked for:
        // --sizes range|WxH,.. picks.
        _chart = RenderCommandLine.Option(args, "--chart") is { } chartText && Enum.TryParse<ChartType>(chartText, ignoreCase: true, out var chartValue) && Enum.IsDefined(chartValue) ? chartValue : null;
        _detail = RenderCommandLine.Option(args, "--detail") is { } detailText && Enum.TryParse<ChartDetail>(detailText, ignoreCase: true, out var detailValue) && Enum.IsDefined(detailValue) ? detailValue : null;
        _trend = RenderCommandLine.Option(args, "--trend") is { } trendText && Enum.TryParse<MetricTrend>(trendText, ignoreCase: true, out var trendValue) && Enum.IsDefined(trendValue) ? trendValue : null;
        _fragment = RenderCommandLine.Option(args, "--config");
        var sized = Fragment(WidgetConfig.CreateDefault(kind));
        if (style is { } styled)
        {
            WidgetStyles.Set(sized, styled);
        }

        ChartLook(sized);
        var sizing = SizeFit.Of(sized);
        List<CheckedSize> tiers = Size(args, 2) is { } size
            ? [new CheckedSize($"{size.Width:0}x{size.Height:0}", size.Width, size.Height)]
            : Sizes(RenderCommandLine.Option(args, "--sizes"), sizing) is { Count: > 0 } sizes ? sizes : [.. SizeFit.CheckedAt(sizing)];

        // The themed brushes follow the design's theme, not the application's: the harness sets both.
        // [S-6480] --design sets the look the cards are drawn with (a border, a radius, a fill).
        using var harness = RenderHarness.Open(run, "Themia widget render", null, 0, 0, args);
        try
        {
            var reference = Array.IndexOf(args, "--reference") >= 0;
            var (width, height) = PageSize(tiers);
            if (reference)
            {
                width += tiers[0].Width + Gap;
            }
            // [S-1917] --transparent: the page draws nothing, so the PNG keeps the cards' own alpha
            // (a transparent card, the corners) for compositing a render onto a picture. [S-14300] It paints a
            // clear brush, never none: a page with no background is drawn from its first child's corner, so the PNG
            // would not start where the layout dump's card places (the page's DIPs) start.
            var transparent = Array.IndexOf(args, "--transparent") >= 0;
            var page = new Canvas
            {
                Width = width,
                Height = height,
                Background = transparent
                    ? new SolidColorBrush(Colors.Transparent)
                    : DesignApplier.Brush("SolidBackgroundFillColorSecondaryBrush", Colors.DimGray),
            };

            // [S-2119] --wallpaper <path>: the page is the screen, the picture its wallpaper.
            if (RenderCommandLine.Option(args, "--wallpaper") is { } wallpaperPath)
            {
                harness.Own(RenderWallpaper.Use(wallpaperPath, new ScreenMetrics(width, height)));
                if (!transparent)
                {
                    page.Background = new ImageBrush
                    {
                        ImageSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(wallpaperPath))),
                        Stretch = Stretch.UniformToFill,
                    };
                }
            }

            harness.Window.Window.Content = new Grid { Children = { page } };

            // As on the desktop: the page is screen geometry, each card flows with the language itself.
            UiLanguage.ApplyLanguageOnly(page);
            harness.Window.Resize(width, height);

            // [S-1500] A confirmation a state opens is drawn inside this window, as a popup of
            // its own window does not render to a bitmap. Set before any view builds one.
            ConfirmFlyout.RenderInWindow = true;

            // [S-2500] The render's own hub: the system widgets read through it as on the desktop.
            // [S-1931] --machine sample: they read the sample machine instead of this PC. [S-15744] So does a system kind's
            // "sample" state (a meter's own: its reading and items are the sample machine's, never this PC's moving ones).
            harness.UseServices(sampleMachine: string.Equals(RenderCommandLine.Option(args, "--machine"), "sample", StringComparison.OrdinalIgnoreCase)
                || (state == "sample" && WidgetConfig.CreateDefault(kind) is Themia.Core.SystemData.IReadsSystem));

            // [S-9929] A kit service widget's preview states are its provider's recordings.
            var host = harness.CardHost(state);

            // [S-11601] The cards are drawn at the boxes their configs take: the page and window follow.
            var cards = new List<WidgetCard>();
            var labels = Place(page, host, kind, style, tiers, cards);
            cards.ForEach(card => harness.Track(card));
            if (reference)
            {
                page.Width += tiers[0].Width + Gap;
                PlaceReference(page, tiers[0], page.Width - Gap - tiers[0].Width);
            }

            harness.Window.Resize(page.Width, page.Height);

            await Task.Delay(800).ConfigureAwait(true);
            await RenderWallpaper.WhenLoaded().ConfigureAwait(true);

            if (state is not null)
            {
                foreach (var card in cards)
                {
                    if (card.View is IWidgetPreviewState preview)
                    {
                        // [S-2711] A preview state draws over fixture data: no account is needed.
                        if (card.ShowsSetup)
                        {
                            card.RunForPreview();
                            await Task.Delay(200).ConfigureAwait(true);
                        }

                        if (!preview.PreviewStates.Contains(state, StringComparer.Ordinal))
                        {
                            Console.WriteLine($"widget: {kind} has no state '{state}' (has: {string.Join(", ", preview.PreviewStates)})");
                        }

                        await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);

                        // [S-15809] A state drawing a config the card would set up stands under its Set up line (WR-10).
                        if (preview.DrawnConfig is { } drawn)
                        {
                            card.SetupForPreview(drawn);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"widget: {kind} has no preview states");
                    }
                }

                await Task.Delay(400).ConfigureAwait(true);

                // [S-11603] A state that holds its own items is drawn at their size.
                if (!reference && cards.Any(c => c.View is IWidgetPreviewState { DrawnConfig: not null }))
                {
                    tiers = Refit(page, cards, labels, tiers);
                    harness.Window.Resize(page.Width, page.Height);
                    await Task.Delay(400).ConfigureAwait(true);
                }
            }

            // [S-1980] Keys handed to every card before the shot, to look at a widget's keyboard
            // focus ("--keys Down,Down" or "--keys text:be" for type-ahead) without the lease.
            if (RenderCommandLine.Option(args, "--keys") is { } keys)
            {
                foreach (var card in cards)
                {
                    SendKeys(card, keys);
                }

                await Task.Delay(400).ConfigureAwait(true);
            }

            // [S-12831] --hover fx,fy / --pressed fx,fy: each card's item (or control) under that point of it (fractions
            // of the card) shows its look under the pointer, as the owner's hover rules are judged (WR-12, WR-13).
            foreach (var (flag, pressed) in new[] { ("--hover", false), ("--pressed", true) })
            {
                if (RenderCommandLine.Option(args, flag) is { } where && Fraction(where) is { } point)
                {
                    foreach (var card in cards)
                    {
                        PointAt(card, point, pressed);
                    }

                    await Task.Delay(400).ConfigureAwait(true);
                }
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath)!);
            await RenderPng.SaveAsync(page, outPath).ConfigureAwait(true);
            Console.WriteLine($"widget: wrote {outPath}");

            // [S-6732] --tree: each card's visual tree beside the PNG, for the match against a mockup.
            if (Array.IndexOf(args, "--tree") >= 0)
            {
                TreeDump.Write(cards, tiers, outPath + ".tree.json");
                Console.WriteLine($"widget: wrote {outPath}.tree.json");
            }

            // [S-12831] --layout-json: each card's item boxes, its grid pitch and the ink outside its items, for the item check.
            if (RenderCommandLine.Option(args, "--layout-json") is { } layoutPath)
            {
                var full = System.IO.Path.GetFullPath(layoutPath);
                ItemDump.Write(cards, tiers, kind.ToString(), state, page, full);
                Console.WriteLine($"widget: wrote {full}");
            }

            // A state that opens a popup (a confirmation) is not part of the page's bitmap: each
            // open one is written beside it as <name>-popup<n>.png.
            var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(page.XamlRoot);
            for (var i = 0; i < popups.Count; i++)
            {
                if (popups[i].Child is not UIElement content)
                {
                    continue;
                }

                var target = System.IO.Path.ChangeExtension(outPath, null) + $"-popup{i}.png";
                try
                {
                    // A flyout's presenter carries a system backdrop, which a bitmap cannot draw:
                    // draw what it presents.
                    var drawn = content is ContentControl { Content: UIElement presented } ? presented : content;
                    await RenderPng.SaveAsync(drawn, target).ConfigureAwait(true);
                    Console.WriteLine($"widget: wrote {target}");
                }
                catch (Exception ex)
                {
                    // A popup with nothing laid out (a closing tooltip) cannot be drawn; the page already was.
                    Log.Warn(Scope, $"popup {i} could not be drawn", ex);
                    Console.WriteLine($"widget: popup {i} could not be drawn: {ex.Message}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "rendering the widget failed", ex);
            Console.WriteLine($"widget: failed: {ex.Message}");
            return false;
        }
    }

    private static void SendKeys(WidgetCard card, string keys)
    {
        foreach (var token in keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.StartsWith("text:", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var character in token[5..])
                {
                    card.SendText(character);
                }
            }
            else if (Enum.TryParse<WidgetKey>(token, ignoreCase: true, out var key) && Enum.IsDefined(key))
            {
                card.SendKey(key);
            }
            else
            {
                Console.WriteLine($"widget: no key '{token}'");
            }
        }
    }

    /// <summary>
    /// One labelled card per size, side by side.
    /// [S-11601] Each size is drawn as a resize to it leaves the widget: the box fitted into its sizing, which replaces
    /// the size in <paramref name="tiers"/> (and sizes the page). Returns the labels, one per card, for
    /// <see cref="Refit"/>.
    /// </summary>
    internal static List<TextBlock> Place(Canvas page, IWidgetCardHost host, WidgetId kind, int? style, List<CheckedSize> tiers, List<WidgetCard> cards)
    {
        var labels = new List<TextBlock>();
        for (var index = 0; index < tiers.Count; index++)
        {
            var tier = tiers[index];
            var label = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
            page.Children.Add(label);
            labels.Add(label);

            var model = WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1));
            model.Background = _background.Clone();
            model.BackgroundImage = _backgroundImage;
            model.Scale = _scale;
            model.ItemBackground = _ownItems.Clone();
            model.ItemTextColor = _ownItemText;
            model.Align = _align;
            if (style is { } chosen)
            {
                WidgetStyles.Set(model.Config, chosen);
            }

            model.Config = Fragment(model.Config);

            // [S-2848] --place-cleared: a widget about a place, with its place cleared.
            PlaceRender.Apply(model.Config, _args);
            ChartLook(model.Config);

            if (_span is { } seconds && model.Config is HistoryConfig gauge)
            {
                gauge.GraphSpanSeconds = seconds;
            }

            // [S-11601] The box is a size the widget was resized to, fitted into its sizing as a resize on the desktop
            // leaves it; [S-14502] what it shows of a count that follows its card it reads from that box.
            var box = SizeFit.Of(model.Config).Fit(new SizeDip(tier.Width, tier.Height));
            tiers[index] = new CheckedSize(tier.Name, box.Width, box.Height);

            var card = new WidgetCard(host, model);

            card.ApplyContentScale(WidgetScale.For(model, 1));
            page.Children.Add(card);
            cards.Add(card);
        }

        Arrange(page, cards, labels, tiers);
        return labels;
    }

    /// <summary>[S-14962] The config with --config's fragment merged over it (as a state file's would load), or as it is.</summary>
    private static WidgetConfig Fragment(WidgetConfig config)
        => _fragment is { Length: > 0 } fragment ? Themia.Core.Storage.StateJson.Merged(config, fragment) : config;

    /// <summary>
    /// [S-6752] The chart look --chart and --detail ask for, on a graph kind (a type it does not offer is its own, as on load);
    /// [S-14712] a single metric's --trend (its graph drawn as a placed one draws it, sized as its config is) and --span.
    /// </summary>
    private static void ChartLook(WidgetConfig config)
    {
        if (_trend is { } trend && config is IMetricTrend metric)
        {
            metric.Trend = trend;
            _ = MetricRules.Repair(metric);
        }

        if (_span is { } span && config is IMetricGraph graph)
        {
            graph.Span = span;
            _ = MetricGraphs.RepairSpan(graph, config.Kind.ToString());
        }

        if (config is not IChartLooks looks)
        {
            return;
        }

        looks.Chart = _chart ?? looks.Chart;
        looks.Detail = _detail ?? looks.Detail;
        _ = ChartLooks.Repair(looks, config.Kind.ToString());
    }

    /// <summary>Each card at its size under its label, side by side, and the page round them.</summary>
    private static void Arrange(Canvas page, List<WidgetCard> cards, List<TextBlock> labels, List<CheckedSize> tiers)
    {
        var at = Positions(tiers);
        for (var i = 0; i < cards.Count && i < at.Count; i++)
        {
            labels[i].Text = $"{tiers[i].Name}  {tiers[i].Width:0} x {tiers[i].Height:0}";
            cards[i].Width = tiers[i].Width;
            cards[i].Height = tiers[i].Height;
            Canvas.SetLeft(labels[i], at[i].Left);
            Canvas.SetTop(labels[i], at[i].Top);
            Canvas.SetLeft(cards[i], at[i].Left);
            Canvas.SetTop(cards[i], at[i].Top + LabelHeight);
        }

        (page.Width, page.Height) = PageSize(tiers);
    }

    /// <summary>Where each size's label stands (its card under it), left to right.</summary>
    private static List<(double Left, double Top)> Positions(List<CheckedSize> tiers)
    {
        var at = new List<(double, double)>();
        var x = Gap;
        foreach (var tier in tiers)
        {
            at.Add((x, Gap));
            x += tier.Width + Gap;
        }

        return at;
    }

    /// <summary>
    /// [S-11603] After the preview states: a card whose state swapped in a config of its own that is
    /// sized otherwise (<see cref="IWidgetPreviewState.DrawnConfig"/>) is drawn at that config's size for
    /// its tier (its min, natural or max; another box fitted into it), and the page laid out again. Returns the tiers as
    /// drawn.
    /// </summary>
    internal static List<CheckedSize> Refit(Canvas page, List<WidgetCard> cards, List<TextBlock> labels, List<CheckedSize> tiers)
    {
        var drawn = new List<CheckedSize>(tiers);
        for (var i = 0; i < cards.Count && i < tiers.Count; i++)
        {
            if (cards[i].View is not IWidgetPreviewState { DrawnConfig: { } config } || cards[i].View is not IWidgetView)
            {
                continue;
            }

            var sizing = SizeFit.Of(config);
            var tier = tiers[i];
            var asked = tier.Name switch
            {
                CheckedSize.Min => sizing.Min,
                CheckedSize.Natural => sizing.Natural,
                CheckedSize.Max => SizeStretch.Large(sizing),
                _ => new SizeDip(tier.Width, tier.Height),
            };

            var box = sizing.Fit(asked);
            drawn[i] = new CheckedSize(tier.Name, box.Width, box.Height);
        }

        Arrange(page, cards, labels, drawn);
        return drawn;
    }

    /// <summary>
    /// [S-836] A stock WinUI card, every brush the framework's own in the application theme (which a
    /// render sets from <c>--theme</c> before the resources load), and the colours it should composite
    /// to, printed for the pixel comparison.
    /// </summary>
    private static void PlaceReference(Canvas page, CheckedSize tier, double left)
    {
        var resources = Application.Current.Resources;
        var radius = resources["OverlayCornerRadius"] is CornerRadius r ? r : new CornerRadius(ShellMetrics.OverlayRadius);

        var label = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
        label.Text = "WinUI reference";
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, Gap);
        page.Children.Add(label);

        var card = new Grid
        {
            Width = tier.Width,
            Height = tier.Height,
            CornerRadius = radius,
            Background = (Brush)resources["SolidBackgroundFillColorBaseBrush"],
            Children =
            {
                new Border
                {
                    Background = (Brush)resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = radius,
                },
            },
        };

        Canvas.SetLeft(card, left);
        Canvas.SetTop(card, Gap + LabelHeight);
        page.Children.Add(card);

        if (resources["SolidBackgroundFillColorBase"] is Windows.UI.Color b
            && resources["CardBackgroundFillColorDefault"] is Windows.UI.Color fill
            && resources["CardStrokeColorDefault"] is Windows.UI.Color stroke)
        {
            var under = new RgbColor(b.R, b.G, b.B);
            var body = DesignLook.Over(fill.A, new RgbColor(fill.R, fill.G, fill.B), under);
            var edge = DesignLook.Over(stroke.A, new RgbColor(stroke.R, stroke.G, stroke.B), under);
            Console.WriteLine($"widget: reference base {HexColor.From(b.R, b.G, b.B)} card {HexColor.From(body.R, body.G, body.B)} edge {HexColor.From(edge.R, edge.G, edge.B)}");
        }
    }

    internal static (double Width, double Height) PageSize(List<CheckedSize> tiers)
        => (tiers.Sum(t => t.Width + Gap) + Gap, Gap + LabelHeight + tiers.Max(t => t.Height) + Gap);

    /// <summary>An optional "w h" pair right after the output path.</summary>
    private static (double Width, double Height)? Size(string[] args, int at)
    {
        if (at + 1 < args.Length
            && double.TryParse(args[at], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)
            && double.TryParse(args[at + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h)
            && w >= Themia.Sdk.WidgetSizing.Floor && h >= Themia.Sdk.WidgetSizing.Floor && w <= 2000 && h <= 2000)
        {
            return (w, h);
        }

        return null;
    }

    /// <summary>
    /// [S-1917] <c>--sizes 100x100,160x100</c>: several in-between sizes on one page, beside or instead of
    /// the range. [S-2448] <c>range</c> is the kind's min, natural and max; [S-14500] <c>steps</c> the sizes round its natural cell by cell.
    /// </summary>
    private static List<CheckedSize>? Sizes(string? list, WidgetSizing sizing)
    {
        if (list is null)
        {
            return null;
        }

        var tiers = new List<CheckedSize>();
        foreach (var item in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (item.Equals("range", StringComparison.OrdinalIgnoreCase))
            {
                tiers.AddRange(SizeFit.CheckedAt(sizing));
                continue;
            }

            // [S-14500] The kind cell by cell round its natural (the item check's "every cell"): the sizes whole-item steps forbade.
            if (item.Equals("steps", StringComparison.OrdinalIgnoreCase))
            {
                tiers.AddRange(SizeFit.CheckedCells(sizing));
                continue;
            }

            var parts = item.Split('x');
            if (parts.Length == 2 && Size(parts, 0) is { } size)
            {
                tiers.Add(new CheckedSize(item, size.Width, size.Height));
            }
            else
            {
                Console.WriteLine($"widget: '{item}' is not a size (WxH, {Themia.Sdk.WidgetSizing.Floor} to 2000)");
            }
        }

        return tiers;
    }

    /// <summary>"0.5,0.25": a point of a card as fractions of its width and height.</summary>
    private static Windows.Foundation.Point? Fraction(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return parts.Length == 2
            && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, inv, out var x)
            && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out var y)
            && x is >= 0 and <= 1 && y is >= 0 and <= 1
                ? new Windows.Foundation.Point(x, y)
                : null;
    }

    /// <summary>[S-12831] The topmost item or control of <paramref name="card"/> under <paramref name="fraction"/> shows its pointer look.</summary>
    private static void PointAt(WidgetCard card, Windows.Foundation.Point fraction, bool pressed)
    {
        var local = new Windows.Foundation.Point(fraction.X * card.ActualWidth, fraction.Y * card.ActualHeight);
        Windows.Foundation.Point host;
        try
        {
            host = card.TransformToVisual(null).TransformPoint(local);
        }
        catch (ArgumentException)
        {
            // ignore: the card is not in the window's tree; nothing to point at
            return;
        }

        foreach (var element in VisualTreeHelper.FindElementsInHostCoordinates(host, card))
        {
            if (PointerPreview.Show(element, pressed))
            {
                return;
            }

            if (element is Control control && VisualStateManager.GoToState(control, pressed ? "Pressed" : "PointerOver", false))
            {
                return;
            }
        }
    }
}
#endif
