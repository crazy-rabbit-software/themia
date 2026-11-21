#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2328] Debug only: draw the real edit bar ([S-2320]) and its flyouts off-screen to PNGs.
/// <c>Themia.exe --render editbar &lt;outDir&gt; [--theme light|dark] [--only bar|pages|appearance|display|select|marks|unplaced|follow] [--raster 100]</c> writes the bar
/// (<c>editbar-&lt;theme&gt;.png</c>: nothing selected; <c>-widget</c>, one widget; <c>-many</c>, two of a kind;
/// <c>-mixed</c>, three of several; <c>-errors</c>, the problems' button; <c>-open</c>, General's entry lit;
/// <c>-narrow</c> and <c>-narrowest</c>, 900- and 640-wide displays; <c>-add</c>, the add menu; <c>-placing</c>,
/// the placement line), every page's flyout hung from its entry and its pickers opened
/// (<c>page-&lt;theme&gt;-&lt;page&gt;[-…].png</c>), and the selection's page beside real cards
/// (<c>select-&lt;theme&gt;-….png</c>), over a busy stand-in wallpaper; and the edit markers (<c>marks-…</c>).
/// Stand-in hosts: nothing reaches Windows or the user's state.
/// </summary>
internal static partial class EditBarRender
{
    private const string Scope = "editbar-render";

    private const double ScreenWidth = 1280;
    private const double ScreenHeight = 64;
    private const double OpenHeight = 900;

    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "editbar",
        "<outDir> [--theme light|dark] [--only bar|pages|appearance|display|select|marks|unplaced|follow] [--raster 100]",
        """
        the edit bar (editbar-*: nothing, one, several selected, problems, an entry open, narrow), the add page (-add*), placing;
        every page's flyout with its pickers open (page-*-general*, -accounts*, -display*, -views*, -appearance*, -unplaced*);
        the selection's page beside real cards (select-*) [S-2328]; the edit marks (marks-*) and a block resized by the
        selection's frame (marks-*-block*, [S-2460]); a flyout following its page as a card opens and closes (-follow-*, [S-2350]);
        each flyout's hit probe and press check said beside its picture [S-2346]. There is no settings window [S-3585]
        --only: one group of pictures; --raster 100: drawn at 100 % whatever the display's scale [S-2377]
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outDir = run.PathArg(0);
        var theme = run.App.RequestedTheme == ApplicationTheme.Light ? "light" : "dark";
        var only = run.Option("--only");

        // What went wrong while drawing is said with the pictures.
        Log.AddSink(static record =>
        {
            if (record.Level >= LogLevel.Warn)
            {
                Console.WriteLine($"editbar: {record.Level} {record.Scope}: {record.Message} {record.Detail}");
            }
        });

        System.IO.Directory.CreateDirectory(outDir);
        using var harness = RenderRaster.Open(run, "Themia edit bar render", ScreenWidth, OpenHeight);
        var screen = new Grid
        {
            Width = ScreenWidth,
            Height = ScreenHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,

            // A stand-in wallpaper: the bar over a flat tint.
            Background = DesignApplier.Brush("SolidBackgroundFillColorTertiaryBrush", Colors.DimGray),
        };

        harness.Window.Window.Content = new Grid { Children = { screen } };
        UiLanguage.ApplyLanguageOnly(screen);

        if (only is null or "bar")
        {
            await BarShots(screen, outDir, theme).ConfigureAwait(true);
        }

        if (only is null or "pages")
        {
            await SettingsShots(screen, outDir, theme).ConfigureAwait(true);
            await BarMenuShots(screen, outDir, theme).ConfigureAwait(true);
        }

        if (only is "appearance")
        {
            await SettingsShots(screen, outDir, theme, appearanceOnly: true).ConfigureAwait(true);
        }

        if (only is "display")
        {
            await DisplayShots(screen, outDir, theme).ConfigureAwait(true);
        }

        // [S-13700] The Unplaced icons page and its entry's badge.
        if (only is null or "pages" or "unplaced")
        {
            await UnplacedShots(screen, outDir, theme).ConfigureAwait(true);
        }

        // [S-2350] A page's flyout following its page as a card opens and closes again.
        if (only is null or "pages" or "follow")
        {
            await FollowShots(screen, outDir, theme).ConfigureAwait(true);
        }

        if (only is null or "select")
        {
            await SelectionShots(screen, harness.Ticker, outDir, theme).ConfigureAwait(true);
        }

        // [S-31b] The edit markers; [S-2460] the selection's frame and a block resized by it.
        if (only is null or "marks")
        {
            await MarkerShots(screen, harness.Ticker, outDir, theme).ConfigureAwait(true);
        }

        return RenderCommandLine.Passed;
    }

    /// <summary>[S-2320]..[S-2324] The bar in its states, and the add menu and the placement line.</summary>
    private static async Task BarShots(Grid screen, string outDir, string theme)
    {
        var clockKind = ModuleCatalog.Of(new WidgetId("clock"));
        var clock = new BarSelection(clockKind.Name, clockKind.Glyph);
        var two = new BarSelection(Core.Shell.SelectionPage.Words(new QuickSetOffer(2, null, null, null) { CommonKind = new WidgetId("clock") }, id => ModuleCatalog.Of(id).Name)!, clock.Glyph);
        var mixed = new BarSelection(Str.Shell_Views_WidgetCount.Plural(3), Glyphs.Apps);
        var wide = ScreenWidth - (2 * ShellMetricsBarTop);

        await Shot(screen, Bar(new EditBarState(null, 0, null, false, wide)), outDir, $"editbar-{theme}.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(clock, 0, null, true, wide)), outDir, $"editbar-{theme}-widget.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(two, 0, null, false, wide)), outDir, $"editbar-{theme}-many.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(mixed, 0, null, false, wide)), outDir, $"editbar-{theme}-mixed.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(clock, 2, null, false, wide)), outDir, $"editbar-{theme}-errors.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(null, 0, SettingsPageId.General, false, wide)), outDir, $"editbar-{theme}-open.png").ConfigureAwait(true);
        var longName = new BarSelection("Weather in San Francisco and the Bay Area", ModuleCatalog.Of(new WidgetId("weatherCondition")).Glyph);
        await Shot(screen, Bar(new EditBarState(longName, 0, null, true, 900 - (2 * ShellMetricsBarTop))), outDir, $"editbar-{theme}-narrow.png", room: 900).ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(longName, 0, null, true, 640 - (2 * ShellMetricsBarTop))), outDir, $"editbar-{theme}-narrowest.png", room: 640).ConfigureAwait(true);

        // [S-11712] The add page: Start, two subjects, a search with results and one with none, a narrow display, a tile pointed at.
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add.png"), ScreenWidth, OpenHeight, null, null).ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-system.png"), ScreenWidth, OpenHeight, AddFolderId.System, null).ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-weather.png"), ScreenWidth, OpenHeight, AddFolderId.Weather, null).ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-family.png"), ScreenWidth, OpenHeight, AddFolderId.System, null, "family:Processor", "family:Processor").ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-search.png"), ScreenWidth, OpenHeight, null, "music").ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-none.png"), ScreenWidth, OpenHeight, null, "zzqx").ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-narrow.png"), 900, OpenHeight, null, null).ConfigureAwait(true);

        // [S-13602] The pointer resting on a tile: its preview beside the tiles (a widget, then a ready-made group).
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-hover.png"), ScreenWidth, OpenHeight, null, null, "kind:weatherForecast").ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-hover-group.png"), ScreenWidth, OpenHeight, AddFolderId.System, null, "group:thisPc").ConfigureAwait(true);

        var hint = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Text = Str.Shell_Place_Hint.Format(("action", Str.Shell_Place_PlaceOne.Format(("name", clockKind.Label.Text())))),
        };
        var line = new Border { Child = hint, MinHeight = EditBar.BarHeight, Padding = new Thickness(Fluent.Space4, 0, Fluent.Space4, 0) };
        ChromeFrame.Apply(line);
        await Shot(screen, line, outDir, $"editbar-{theme}-placing.png").ConfigureAwait(true);
    }

    /// <summary>[S-2320] The real bar, built and showing <paramref name="state"/>, its commands doing nothing.</summary>
    private static EditBar Bar(EditBarState state)
    {
        var commands = new EditBarCommands(
            Add: () => { },
            Page: _ => { },
            Selection: () => { },
            IntoFlyout: () => { },
            Done: () => { });
        var bar = new EditBar(commands);
        bar.Fill(commands);
        bar.Show(state);
        UiLanguage.ApplyFlow(bar);
        return bar;
    }

    /// <summary>One picture: the bar centred 8 DIPs under the display's top.</summary>
    private static async Task Shot(Grid screen, FrameworkElement bar, string outDir, string name, double? room = null)
    {
        screen.Children.Clear();
        screen.Height = ScreenHeight;
        screen.Width = room ?? ScreenWidth;

        var canvas = new Canvas();
        screen.Children.Add(canvas);
        canvas.Children.Add(bar);
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var barX = Math.Round(((room ?? ScreenWidth) - bar.DesiredSize.Width) / 2);
        Canvas.SetLeft(bar, barX);
        Canvas.SetTop(bar, ShellMetricsBarTop);

        await Task.Delay(500).ConfigureAwait(true);
        await RenderHarness.SaveAsync(screen, System.IO.Path.Combine(outDir, name), "editbar").ConfigureAwait(true);
        screen.Width = ScreenWidth;
    }

    /// <summary>
    /// [S-2325] One page's flyout open from its entry on the stand-in display over the busy wallpaper: the
    /// page opened at <paramref name="key"/> (its row, as a deep link opens it; none for its top), then
    /// <paramref name="then"/> (typing, a click), then <paramref name="after"/> on the flyout (a scroll).
    /// </summary>
    private static async Task PageShot(Grid screen, string path, SettingsPageId page, PageView view, Func<SettingsPage>? core, string? key, Action<PageView>? then = null, Action<PageFlyout>? after = null)
    {
        screen.Children.Clear();
        screen.Height = OpenHeight;
        var flat = screen.Background;
        screen.Background = Wallpaper();
        var canvas = new Canvas { Clip = new RectangleGeometry { Rect = new Rect(0, 0, ScreenWidth, OpenHeight) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        var bar = Bar(new EditBarState(null, 0, page, false, ScreenWidth - (2 * ShellMetricsBarTop)));
        var barRect = PlaceBar(canvas, bar, ScreenWidth);
        UiLanguage.ApplyFlow(view);
        var area = new DipRect(0, 0, ScreenWidth, OpenHeight);
        var flyout = HangFlyout(canvas, view, bar, barRect, page, area);
        view.Show();
        if (key is not null && core?.Invoke() is { } built && (SettingsPages.PathTo(built, key) is not { } at || !view.Open(at)))
        {
            Console.WriteLine($"editbar: no row {key} on {page} for {path}");
        }

        then?.Invoke(view);
        await Task.Delay(300).ConfigureAwait(true);
        canvas.UpdateLayout();
        flyout.Place(FlyoutPlacement.Place(bar.EntryBox(page, canvas) ?? barRect, barRect, area, flyout.ContentHeight));
        canvas.UpdateLayout();
        after?.Invoke(flyout);

        await Task.Delay(500).ConfigureAwait(true);
        Probe(screen, flyout, path);
        await PressCheck(view, path).ConfigureAwait(true);
        await RenderHarness.SaveAsync(screen, path, "editbar").ConfigureAwait(true);
        view.Close();
        screen.Background = flat;
    }

    /// <summary>[S-2346] Every press over the flyout is the flyout's: said with the picture, loudly when one is not.</summary>
    private static void Probe(Grid screen, PageFlyout flyout, string path)
    {
        if (screen.XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        try
        {
            var (points, missed, first) = FlyoutHitProbe.Run(root, flyout, Themia.Sdk.Kit.ShellMetrics.Flyout.BeakHeight, Themia.Sdk.Kit.ShellMetrics.OverlayRadius);
            Console.WriteLine(missed == 0
                ? $"editbar: hit probe {System.IO.Path.GetFileName(path)}: the page takes all {points} points"
                : $"editbar: hit probe {System.IO.Path.GetFileName(path)}: {missed} of {points} points not the page's, first {first}");
        }
        catch (Exception ex)
        {
            // The probe only reports; the picture is drawn all the same.
            Log.Warn(Scope, $"the hit probe of {System.IO.Path.GetFileName(path)} failed", ex);
        }
    }

    /// <summary>
    /// [S-2346] A press on the page, a change while it is held, and its release, through the surface's own
    /// <see cref="FlyoutRedraws"/>: the page is not drawn under the press nor inside the release, but once after
    /// it, and nothing throws. Said with the picture, loudly when it does not hold.
    /// </summary>
    private static async Task PressCheck(PageView view, string path)
    {
        var name = System.IO.Path.GetFileName(path);
        var failed = false;
        void Fail(string why)
        {
            failed = true;
            Console.WriteLine($"editbar: press check {name}: FAILED, {why}");
        }

        if (!_countingErrors)
        {
            Log.AddSink(static record =>
            {
                if (record.Level >= LogLevel.Error)
                {
                    Interlocked.Increment(ref _errors);
                }
            });
            _countingErrors = true;
        }

        var errorsBefore = Volatile.Read(ref _errors);
        var redraws = new FlyoutRedraws(view.DispatcherQueue, view.Show);
        var before = view.Drawings;
        redraws.Pressed();
        redraws.Ask();
        if (view.Drawings != before)
        {
            Fail("drawn under the press");
        }

        redraws.LetGo();
        redraws.LetGo();
        redraws.Ask();
        if (view.Drawings != before)
        {
            Fail("drawn inside the release");
        }

        // Same priority, in order: this runs after the drawing the release posted.
        var turn = new TaskCompletionSource();
        if (!view.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => turn.SetResult()))
        {
            Fail("the dispatcher took nothing");
            return;
        }

        await turn.Task.ConfigureAwait(true);
        if (view.Drawings != before + 1)
        {
            Fail($"drawn {view.Drawings - before} times after the release, not once");
        }

        if (Volatile.Read(ref _errors) - errorsBefore is > 0 and var logged)
        {
            Fail($"{logged} errors logged while drawing");
        }

        if (!failed)
        {
            Console.WriteLine($"editbar: press check {name}: drawn once, after the release");
        }
    }

    /// <summary>[S-2346] The errors logged in this render, for <see cref="PressCheck"/>; counted from its first check.</summary>
    private static int _errors;

    private static bool _countingErrors;

    /// <summary>One widget of a selection shot: its kind, where its card is, its name, and whether it is selected.</summary>
    private sealed record PropsCard(WidgetId Kind, double X, double Y, double W, double H, bool Selected, string? Style = null, string? ItemColor = null, double Scale = 1.0, string ItemText = "", FillChoice? Background = null, FillChoice? Items = null, ContentAlign? Align = null, string? Config = null);

    /// <summary>[S-2340]..[S-2343] The selection's page beside real cards, each case on the stand-in display.</summary>
    private static async Task SelectionShots(Grid screen, Ticker ticker, string outDir, string theme)
    {
        PropsCard[] clock = [new(new WidgetId("clock"), 140, 160, 300, 200, true, "flip", "#2A3248", 1.25, "#FFFFFF")];
        PropsCard[] covered = [new(new WidgetId("clock"), 700, 140, 300, 200, true, "flip", "#2A3248", 1.0, "#FFFFFF")];
        PropsCard[] coveredGroup =
        [
            new(new WidgetId("clock"), 620, 200, 220, 160, true),
            new(new WidgetId("countdown"), 840, 200, 240, 160, true),
            new(new WidgetId("clock"), 760, 420, 220, 160, false),
        ];
        PropsCard[] many =
        [
            new(new WidgetId("clock"), 120, 160, 220, 160, true),
            new(new WidgetId("countdown"), 120, 360, 260, 180, true, Scale: 0.9),
            new(new WidgetId("clock"), 120, 580, 200, 140, true),
        ];
        PropsCard[] two = [new(new WidgetId("clock"), 120, 160, 220, 160, true), new(new WidgetId("clock"), 120, 360, 220, 160, true)];
        PropsCard[] toggle = [new(new WidgetId("wifiToggle"), 200, 260, 120, 120, true)];
        PropsCard[] weather = [new(new WidgetId("weatherCondition"), 140, 160, 300, 200, true)];
        PropsCard[] icon = [new(new WidgetId("desktopItem"), 200, 260, 76, 100, true)];

        // [S-5611] A desktop file, an own icon on a Windows location, and two files: each is a "Desktop icon".
        PropsCard[] iconFile = [new(new WidgetId("desktopItem"), 200, 260, 76, 100, true, Config: """{"item":"C:\\Users\\Sample\\Desktop\\Report.docx"}""")];
        PropsCard[] iconOwn = [new(new WidgetId("desktopItem"), 200, 260, 76, 100, true, Config: """{"target":"shell:Personal"}""")];
        PropsCard[] iconTwo =
        [
            new(new WidgetId("desktopItem"), 200, 260, 76, 100, true, Config: """{"item":"C:\\Users\\Sample\\Desktop\\Report.docx"}"""),
            new(new WidgetId("desktopItem"), 200, 360, 76, 100, true, Config: """{"item":"C:\\Users\\Sample\\Desktop\\Budget.xlsx"}"""),
        ];
        PropsCard[] countdown = [new(new WidgetId("countdown"), 140, 160, 280, 180, true)];

        // [S-2351] A fence: its name and its rules' texts asked in place, in their own cards.
        PropsCard[] fence = [new(new WidgetId("fence"), 140, 160, 300, 240, true, Config: """{"name":"Work"}""")];

        // [S-2332] [S-9602] A header: its title and caption typed in place, its place its Location alone.
        PropsCard[] header = [new(new WidgetId("header"), 140, 160, 300, 52, true, Config: """{"text":"Weather","caption":"This week"}""")];
        PropsCard[] alarm = [new(new WidgetId("alarm"), 140, 160, 280, 160, true)];
        // [S-2240] Two touching cards, selected together: they join as any touching cards do, no group.
        PropsCard[] station =
        [
            new(new WidgetId("weatherCondition"), 140, 160, 300, 200, true),
            new(new WidgetId("weatherForecast"), 140, 360, 300, 200, true),
        ];

        // [S-1597] A selected card under another: its handles stand over the card above it.
        PropsCard[] under =
        [
            new(new WidgetId("clock"), 20, 540, 240, 160, true),
            new(new WidgetId("weatherCondition"), 140, 610, 200, 160, false),
        ];

        // [S-3722] Every lookup of a widget's page, side by side: a stock's symbol, a rate's currency, a clock's zone.
        PropsCard[] stock = [new(new WidgetId("stock"), 140, 160, 300, 200, true)];
        PropsCard[] rate = [new(new WidgetId("exchangeRate"), 140, 160, 300, 200, true)];

        // [S-2847] The location searches answer from fixture places, [S-1307] the symbol searches from fixture
        // hits; [S-3323] the screen-time lines read the sample history.
        PlaceRender.UseFixture();
        SymbolRender.UseFixture();
        AppServices.ScreenTime ??= new SampleScreenTime();
        UseSamplePools();

        (PropsCard[] Layout, string Name, string? Click, string? Type, Action<WidgetMenuLines>? Lines)[] cases =
        [
            (clock, "one", null, null, null),

            // [S-2344] A selected widget under its own page: seen through it, live as its settings change.
            (covered, "covered", null, null, null),
            (coveredGroup, "covered-two", null, null, null),
            (under, "under-another", null, null, null),
            (clock, "style", "style", null, null),
            (clock, "background", "background", null, null),
            (clock, "appearance-end", null, null, null),
            (two, "two", null, null, null),
            (many, "many", null, null, null),

            // [S-2345] The Appearance tab; [S-11754] several touching: For all of them and the look, no widget alone.
            (clock, "one-appearance", "tab:appearance", null, null),
            (many, "many-appearance", "tab:appearance", null, null),
            (station, "touching", null, null, null),
            (station, "touching-appearance", "tab:appearance", null, null),
            (toggle, "toggle", null, null, null),
            (weather, "weather", null, null, null),
            (weather, "weather-location", "w/" + Str.Shell_Place_Location.Text(), "zur", null),
            (weather, "weather-location-open", "w/" + Str.Shell_Place_Location.Text(), null, null),
            (weather, "weather-location-wait", "w/" + Str.Shell_Place_Location.Text(), PlaceRender.Unanswered, null),
            (weather, "weather-location-none", "w/" + Str.Shell_Place_Location.Text(), "qqqq", null),
            (stock, "stock-symbol", "w/" + Str.Stocks_Tab_Symbols.Text(), "micro", null),
            (stock, "stock-symbol-open", "w/" + Str.Stocks_Tab_Symbols.Text(), null, null),
            (stock, "stock-symbol-wait", "w/" + Str.Stocks_Tab_Symbols.Text(), SymbolRender.Unanswered, null),
            (stock, "stock-symbol-offline", "w/" + Str.Stocks_Tab_Symbols.Text(), SymbolRender.Unreached, null),
            (stock, "stock-symbol-none", "w/" + Str.Stocks_Tab_Symbols.Text(), "qqqq", null),
            (rate, "rate-currency", "w/" + Str.Online_Rates_Settings_Base_Label.Text(), "e", null),
            (clock, "clock-zone", "w/Timezone", "york", null),
            (weather, "setup-weather", SetupOpen, null, null),
            (icon, "desktop-icon", null, null, null),
            (iconFile, "desktop-icon-file", null, null, null),
            (iconOwn, "desktop-icon-own", null, null, null),
            (iconTwo, "desktop-icon-two", null, null, null),
            (header, "header", null, null, null),
            (alarm, "alarm", null, null, null),
            (countdown, "countdown", null, null, null),
            (countdown, "vocabulary", null, null, Vocabulary),
            (countdown, "vocabulary-colour", "w/Accent", null, Vocabulary),
            (countdown, "vocabulary-feeds", "w/Feeds", null, Vocabulary),
            (countdown, "vocabulary-confirm", "w/Clear history", null, Vocabulary),
            (countdown, "vocabulary-confirm-end", "w/Clear history", null, Vocabulary),

            // [S-2351] What a line asks, asked in place in its own card: the fence's name shut and open, typed into, a rule's
            // text inside its card, a list's Add… refused and given, a list item's Rename….
            (fence, "fence", null, null, null),
            (fence, "fence-name", "w/" + FenceName, null, null),
            (fence, "fence-name-typed", "w/" + FenceName, "Projects 2026", null),
            (fence, "fence-rules-name", "w/" + FenceRules + "/" + FenceRulesName, null, null),
            (countdown, "vocabulary-add", "w/Feeds/" + Str.Menu_Add.Text(), null, Vocabulary),
            (countdown, "vocabulary-add-refused", "w/Feeds/" + Str.Menu_Add.Text(), "feed", Vocabulary),
            (countdown, "vocabulary-add-typed", "w/Feeds/" + Str.Menu_Add.Text(), "https://example.com/feed", Vocabulary),
            (countdown, "vocabulary-rename", "w/Feeds/Blog feed/" + Str.Menu_Rename.Text(), null, Vocabulary),
        ];

        foreach (var (layout, name, click, type, lines) in cases)
        {
            await SelectionShot(screen, ticker, layout, System.IO.Path.Combine(outDir, $"select-{theme}-{name}.png"), click, type, lines, scrollToEnd: name.EndsWith("-end", StringComparison.Ordinal)).ConfigureAwait(true);
        }

        await AdapterShots(screen, ticker, outDir, theme).ConfigureAwait(true);
    }

    /// <summary>
    /// [S-14962] The network kinds' Adapter line over the sample PC ([S-1931]: its Wi-Fi and its dock's unplugged Ethernet):
    /// a local address's page with the line open, one on an adapter that is gone (kept, "not connected"), and several
    /// network widgets set at once.
    /// </summary>
    private static async Task AdapterShots(Grid screen, Ticker ticker, string outDir, string theme)
    {
        using var machine = RenderMachine.Install(ticker, screen.DispatcherQueue);
        var adapter = "w/" + Str.NetworkAdapter_Label.Text();
        const string Gone = """{"adapter":"{99999999-8888-7777-6666-555555555555}","adapterName":"USB Ethernet"}""";
        var ethernet = $$"""{"adapter":"{{Themia.Core.SystemData.SamplePc.EthernetId}}","adapterName":"Ethernet"}""";
        PropsCard[] local = [new(new WidgetId("networkLocalAddress"), 140, 160, 300, 84, true)];
        PropsCard[] chosen = [new(new WidgetId("networkLocalAddress"), 140, 160, 300, 84, true, Config: $$"""{"adapter":"{{Themia.Core.SystemData.SamplePc.WifiId}}","adapterName":"Wi-Fi"}""")];
        PropsCard[] unplugged = [new(new WidgetId("networkDownload"), 140, 160, 300, 84, true, Config: ethernet)];
        PropsCard[] gone = [new(new WidgetId("networkLocalAddress"), 140, 160, 300, 84, true, Config: Gone)];
        PropsCard[] several =
        [
            new(new WidgetId("networkLocalAddress"), 20, 160, 344, 84, true),
            new(new WidgetId("networkDownload"), 20, 244, 344, 84, true),
            new(new WidgetId("network"), 20, 328, 344, 84, true),
            new(new WidgetId("networkUpload"), 20, 412, 344, 120, true, Config: """{"trend":"Line"}"""),
        ];

        (PropsCard[] Layout, string Name, string? Click)[] cases =
        [
            (local, "network-adapter", null),
            (local, "network-adapter-open", adapter),
            (chosen, "network-adapter-wifi", adapter),
            (unplugged, "network-adapter-unplugged", adapter),
            (gone, "network-adapter-gone", adapter),
            (several, "network-adapter-several", null),
            (several, "network-adapter-several-open", "shared/" + SharedSettings.NetworkAdapter.Key + "/" + Str.NetworkAdapter_Label.Text()),
        ];

        foreach (var (layout, name, click) in cases)
        {
            await SelectionShot(screen, ticker, layout, System.IO.Path.Combine(outDir, $"select-{theme}-{name}.png"), click).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// [S-1429] [S-1349] The habit lists' and the notes' menus over pools of a render's own, kept in a
    /// folder under %TEMP% (never the user's), seeded once with two of each.
    /// </summary>
    private static void UseSamplePools()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "themia-editbar-render");
        System.IO.Directory.CreateDirectory(folder);

        if (AppServices.Habits is null)
        {
            var habits = new Services.Habits.HabitPool(new Themia.Core.Habits.HabitListStore(System.IO.Path.Combine(folder, "habits.json")));
            foreach (var (name, items) in new[] { ("Morning", new[] { "Stretch", "Water", "Read" }), ("Fitness", new[] { "Run" }) })
            {
                if (habits.Lists.Any(l => l.Name == name) || habits.Create(name) is not { } id)
                {
                    continue;
                }

                habits.Update(id, _ => [.. items.Select((item, i) => new Themia.Core.Habits.Habit($"{id}-{i}", item, []))]);
            }

            AppServices.Habits = habits;
        }

        if (AppServices.Notes is null)
        {
            var notes = new Services.Notes.NotePool(new Themia.Core.Notes.NoteStore(System.IO.Path.Combine(folder, "notes")));
            if (notes.Notes.Count == 0)
            {
                notes.Create("Shopping", "Milk, eggs");
                notes.Create("Ideas");
            }

            AppServices.Notes = notes;
        }
    }

    /// <summary>[S-3724]..[S-3730] A stand-in widget's lines: one of each kind the SDK builds, as a ported widget would add them.</summary>
    private static void Vocabulary(WidgetMenuLines menu)
    {
        List<string> feeds = ["Blog feed", "News feed"];
        menu.Lines.Add(menu.Colour("Accent", "#3A6EA5", _ => { }));
        menu.Lines.Add(menu.Fill("Accent fill", FillChoice.Of("#3A6EA5", 0.6), _ => { }));
        menu.Lines.Add(menu.Number(Glyphs.Count, "Rows", 4, 1, 10, 1, Themia.Sdk.WinUI.Kit.KitMenuLines.Rows, _ => { }));
        menu.Lines.Add(menu.Number(Glyphs.Timer, "Refresh every", 30, 5, 600, 5, n => LocaleFormat.Integer(n) + " min", _ => { }));
        menu.Lines.Add(menu.Slider(Glyphs.Volume, "Volume", 40, 0, 100, 5, n => LocaleFormat.Percent(Strings.Current, n), _ => { }));
        menu.Lines.Add(menu.Slider(Glyphs.Scale, "Gap between widgets", 12, 0, 48, 2, n => LocaleFormat.Integer(n) + " px", _ => { }));
        menu.Lines.Add(menu.Toggle(Glyphs.Bell, "Sound on finish", true, _ => { }));
        menu.Lines.Add(menu.Toggle(null, "Hide when idle", false, _ => { }));
        menu.Lines.Add(menu.PickFolder(Glyphs.Folder, "Folder", @"C:\Users\Me\Pictures\Wallpapers", _ => { }));
        menu.Lines.Add(menu.List(Glyphs.Rss, "Feeds", feeds, new MenuList<string>(f => f, "No feeds yet")
        {
            Rename = new MenuListRename<string>((_, _) => false),

            // [S-2351] A check that refuses: why under the field, the accept greyed.
            Add = new MenuListAdd<string>("Add a feed", "https://…", t => t.Contains("://", StringComparison.Ordinal) ? null : "Type a web address, such as https://example.com/feed", f => f),
        }));
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.Add(new WidgetMenuLines(() => { }) { OpenSetting = _ => { } }.AccountsLine(Glyphs.Contact, "Manage accounts"));
        menu.Lines.Add(menu.Confirm(Glyphs.Delete, "Clear history", "Clear the whole history?", "Clear", () => { }, detail: "Every entry goes, and it cannot be undone."));
        menu.Lines.Add(WidgetMenuLines.Note("Weather data by Open-Meteo.com, under CC BY 4.0, with a long credit that wraps onto a second line"));
    }

    /// <summary>
    /// [S-3715] A stand-in wallpaper: a bright, many-coloured photo-like wash with soft shapes, so the
    /// panel's surface is judged against something as busy as a real desktop, not a flat tint. A
    /// render fixture, not a design colour.
    /// </summary>
    private static Brush Wallpaper()
    {
        var wash = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        foreach (var (offset, colour) in new[]
        {
            (0.0, Windows.UI.Color.FromArgb(255, 0x1E, 0x5B, 0x8C)),
            (0.35, Windows.UI.Color.FromArgb(255, 0xE0, 0x8A, 0x3C)),
            (0.6, Windows.UI.Color.FromArgb(255, 0x6B, 0xA3, 0x5A)),
            (1.0, Windows.UI.Color.FromArgb(255, 0x8C, 0x3E, 0x7A)),
        })
        {
            wash.GradientStops.Add(new GradientStop { Offset = offset, Color = colour });
        }

        return wash;
    }

    /// <summary>Soft light and dark blobs over the wash, at fixed places.</summary>
    private static void Blobs(Canvas canvas)
    {
        foreach (var (x, y, d, a, light) in new[] { (900.0, 60.0, 260.0, 90, true), (620.0, 300.0, 340.0, 70, false), (120.0, 380.0, 220.0, 80, true), (1060.0, 340.0, 180.0, 90, false) })
        {
            var blob = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = d,
                Height = d,
                Fill = new SolidColorBrush(light ? Windows.UI.Color.FromArgb((byte)a, 255, 255, 255) : Windows.UI.Color.FromArgb((byte)a, 0, 0, 0)),
            };
            Canvas.SetLeft(blob, x);
            Canvas.SetTop(blob, y);
            canvas.Children.Add(blob);
        }
    }

    /// <summary>[S-1862] The click that opens the page as the card's Set up button does.</summary>
    private const string SetupOpen = "<setup>";

    /// <summary>[S-2351] The fence's lines a render clicks, by their words (the fence module's own: English in a render).</summary>
    private const string FenceName = "Fence name";

    private const string FenceRules = "New items";

    private const string FenceRulesName = "Name contains";

    /// <summary>
    /// [S-2340] [S-2343] The selection's page as edit mode shows it: real cards in edit mode, the bar with the
    /// selection's entry lit, its flyout hung from that entry; a row clicked (a picker opened), a lookup typed
    /// into, the page scrolled to its end.
    /// </summary>
    private static async Task SelectionShot(Grid screen, Ticker ticker, PropsCard[] layout, string path, string? click = null, string? type = null, Action<WidgetMenuLines>? lines = null, bool scrollToEnd = false)
    {
        screen.Children.Clear();
        screen.Height = OpenHeight;
        var flat = screen.Background;
        screen.Background = Wallpaper();

        var canvas = new Canvas { Clip = new RectangleGeometry { Rect = new Rect(0, 0, ScreenWidth, screen.Height) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        var view = ViewState.Create("render");
        var widgets = new List<(WidgetInstance Model, PropsCard Spec)>();
        foreach (var spec in layout)
        {
            var model = WidgetInstance.Create(spec.Kind, new FracRect(0, 0, 0.1, 0.1));
            model.ItemBackground = spec.Items?.Clone() ?? (spec.ItemColor is { } items ? FillChoice.Of(items, 0.8) : new FillChoice());
            model.Background = spec.Background?.Clone() ?? new FillChoice();
            model.Scale = spec.Scale;
            model.ItemTextColor = spec.ItemText;
            model.Align = spec.Align;
            if (spec.Config is { } fragment)
            {
                model.Config = Themia.Core.Storage.StateJson.Merged(model.Config, fragment);
            }

            if (spec.Style is { } style && WidgetStyles.Find(spec.Kind, style) is { } option)
            {
                WidgetStyles.Set(model.Config, option.Value);
            }

            view.Widgets.Add(model);
            widgets.Add((model, spec));
        }

        var chosen = widgets.Where(w => w.Spec.Selected).Select(w => w.Model).ToList();
        var bar = SelectionBar(SelectionEntry(chosen), open: true);
        var barRect = PlaceBar(canvas, bar, ScreenWidth);

        var selected = chosen.Select(w => w.Id).ToList();
        var host = new NoticeRender.RenderCardHost(ticker) { EditMode = true, Selected = new HashSet<string>(selected, StringComparer.Ordinal) };

        var cards = new List<WidgetCard>();
        var markers = new EditMarkerLayer();
        foreach (var (model, spec) in widgets)
        {
            var card = new WidgetCard(host, model) { Width = spec.W, Height = spec.H };
            card.ApplyEditMode(true);
            card.ApplySelection();
            card.ApplyContentScale(WidgetScale.For(model, 1));
            Canvas.SetLeft(card, spec.X);
            Canvas.SetTop(card, spec.Y);
            canvas.Children.Add(card);
            markers.Add(card);
            cards.Add(card);
        }

        canvas.Children.Add(markers.Halos);
        canvas.Children.Add(markers.Inks);

        // [S-2460] Several selected: the frame round them, [S-14310] and no marks of their own.
        RenderFrame.Round(markers, [.. cards.Zip(widgets).Select(c => (c.First, new DipRect(c.Second.Spec.X, c.Second.Spec.Y, c.Second.Spec.W, c.Second.Spec.H)))], host.Selected, editing: true, screen.XamlRoot?.RasterizationScale ?? 1, [host]);

        var page = SelectionPageOf(view, selected, lines);
        var area = new DipRect(0, 0, ScreenWidth, OpenHeight);
        var flyout = HangFlyout(canvas, page, bar, barRect, null, area);
        page.Show();
        if (click is not null)
        {
            // [S-2345] "tab:<key>" chooses a tab of the page.
            if (click.StartsWith("tab:", StringComparison.Ordinal))
            {
                page.DebugTab(click["tab:".Length..]);
            }
            else if (click == SetupOpen ? !page.OpenSetup() : !page.DebugClick(click))
            {
                Console.WriteLine($"editbar: no row {click} for {path}");
            }

            if (type is not null)
            {
                page.DebugType(type);
                await Task.Delay(900).ConfigureAwait(true);
            }
        }

        await Task.Delay(300).ConfigureAwait(true);
        canvas.UpdateLayout();

        flyout.Place(FlyoutPlacement.Place(bar.EntryBox(null, canvas) ?? barRect, barRect, area, flyout.ContentHeight));

        // [S-2344] The selected cards it covers seen through it, as the surface shows them.
        flyout.ShineThrough([.. widgets.Where(w => w.Spec.Selected).Select(w => new DipRect(w.Spec.X, w.Spec.Y, w.Spec.W, w.Spec.H))]);
        canvas.UpdateLayout();
        if (scrollToEnd)
        {
            flyout.Scroll(Windows.System.VirtualKey.End);
        }

        await Task.Delay(700).ConfigureAwait(true);
        Probe(screen, flyout, path);
        await RenderHarness.SaveAsync(screen, path, "editbar").ConfigureAwait(true);
        page.Close();
        screen.Background = flat;

        foreach (var card in cards)
        {
            card.Detach();
        }
    }

    /// <summary>
    /// [S-31b]/[S-31c] The edit markers on real cards: <c>marks-&lt;theme&gt;-r4.png</c> and <c>-r16.png</c>
    /// (a selected card, an unselected one and an XS tile at that corner radius), <c>-multi.png</c>
    /// (three selected beside one that is not: the selection's frame round them, no marks of their own, [S-14310]) and
    /// <c>-neighbours.png</c> (cells meeting on their edges:
    /// their marks are one line where they meet).
    /// </summary>
    private static async Task MarkerShots(Grid screen, Ticker ticker, string outDir, string theme)
    {
        PropsCard[] mixed =
        [
            new(new WidgetId("clock"), 120, 120, 280, 180, true),
            new(new WidgetId("countdown"), 460, 140, 240, 160, false),
            new(new WidgetId("wifiToggle"), 760, 160, 64, 64, true),
            new(new WidgetId("wifiToggle"), 860, 160, 64, 64, false),
        ];
        PropsCard[] multi =
        [
            new(new WidgetId("clock"), 120, 120, 240, 160, true),
            new(new WidgetId("countdown"), 380, 120, 240, 160, true),
            new(new WidgetId("clock"), 640, 120, 200, 160, true),
            new(new WidgetId("countdown"), 880, 120, 240, 160, false),
        ];

        await MarkerShot(screen, ticker, mixed, 4, System.IO.Path.Combine(outDir, $"marks-{theme}-r4.png")).ConfigureAwait(true);
        await MarkerShot(screen, ticker, mixed, 16, System.IO.Path.Combine(outDir, $"marks-{theme}-r16.png")).ConfigureAwait(true);
        await MarkerShot(screen, ticker, multi, 8, System.IO.Path.Combine(outDir, $"marks-{theme}-multi.png")).ConfigureAwait(true);

        PropsCard[] neighbours =
        [
            new(new WidgetId("clock"), 120, 100, 240, 150, true),
            new(new WidgetId("countdown"), 360, 100, 240, 150, false),
            new(new WidgetId("countdown"), 120, 250, 240, 150, false),
            new(new WidgetId("clock"), 360, 250, 240, 150, false), // [S-14310] one selected: two would be framed, no accent marks of their own
            new(new WidgetId("wifiToggle"), 600, 100, 120, 120, false),
        ];
        await MarkerShot(screen, ticker, neighbours, 8, System.IO.Path.Combine(outDir, $"marks-{theme}-neighbours.png")).ConfigureAwait(true);
        await BlockShots(screen, ticker, outDir, theme).ConfigureAwait(true);
    }

    /// <summary>
    /// [S-2460] A block resized by the selection's frame, through Core's <see cref="BlockResize"/> on the render host's
    /// screen (1920 × 1080: a cell is 10 DIPs): <c>marks-&lt;theme&gt;-block.png</c> as placed (a clock and a date side by
    /// side over a countdown as wide as both, each at its natural size in whole cells, one card beside it not selected),
    /// <c>-block-corner.png</c> after its bottom-right corner is dragged 130 × 70 DIPs out, <c>-block-left.png</c> after
    /// its left edge is dragged 90 DIPs in, <c>-block-keys.png</c> after three Shift+Right presses. The cells each lands
    /// on are written to the console.
    /// </summary>
    private static async Task BlockShots(Grid screen, Ticker ticker, string outDir, string theme)
    {
        var stand = new ScreenMetrics(1920, 1080);
        static double Up(double dip) => Math.Ceiling(dip / 10) * 10;
        WidgetId[] kinds = [new("clock"), new("date"), new("countdown")];
        var natural = kinds.Select(k => SizeFit.Of(k).Natural).Select(n => (W: Up(n.Width), H: Up(n.Height))).ToArray();
        var top = Math.Max(natural[0].H, natural[1].H);
        var wide = Math.Max(natural[0].W + natural[1].W, natural[2].W);
        const double X = 120, Y = 60;
        PropsCard[] block =
        [
            new(kinds[0], X, Y, natural[0].W, top, true),
            new(kinds[1], X + natural[0].W, Y, wide - natural[0].W, top, true),
            new(kinds[2], X, Y + top, wide, natural[2].H, true),
        ];
        var aside = new PropsCard(new WidgetId("wifiToggle"), X + wide + 260, Y, 120, 120, false);

        var rects = block.Select(s => new FracRect(s.X / stand.Width, s.Y / stand.Height, s.W / stand.Width, s.H / stand.Height)).ToArray();
        var limits = kinds.Select(k => SizeFit.Limits(SizeFit.Of(k), stand, 1.0)).ToArray();
        PropsCard[] Laid(IReadOnlyList<FracRect> laid) => [.. block.Select((s, i) => s with { X = laid[i].X * stand.Width, Y = laid[i].Y * stand.Height, W = laid[i].W * stand.Width, H = laid[i].H * stand.Height }), aside];

        var keys = (IReadOnlyList<FracRect>)rects;
        for (var press = 0; press < 3; press++)
        {
            keys = BlockResize.Nudge(keys, limits, Core.Shell.NudgeDirection.Right, Core.Shell.NudgeStep.Cell, stand);
        }

        foreach (var (name, laid) in new (string, IReadOnlyList<FracRect>)[]
        {
            ("block", rects),
            ("block-corner", BlockResize.Resize(rects, limits, ResizeHandle.BottomRight, 130 / stand.Width, 70 / stand.Height, stand)),
            ("block-left", BlockResize.Resize(rects, limits, ResizeHandle.Left, 90 / stand.Width, 0, stand)),
            ("block-keys", keys),
        })
        {
            var cells = string.Join("  ", laid.Select((r, i) => $"{kinds[i].Value} {r.X * 192:0.##},{r.Y * 108:0.##} {r.W * 192:0.##}x{r.H * 108:0.##}"));
            Console.WriteLine($"editbar: {name} cells (x,y wxh): {cells}");
            await MarkerShot(screen, ticker, Laid(laid), 8, System.IO.Path.Combine(outDir, $"marks-{theme}-{name}.png"), height: 520).ConfigureAwait(true);
        }
    }

    private static async Task MarkerShot(Grid screen, Ticker ticker, PropsCard[] layout, double radius, string path, double height = 420)
    {
        screen.Children.Clear();
        screen.Height = height;

        var canvas = new Canvas();
        screen.Children.Add(canvas);

        var models = layout.Select(spec => (Model: WidgetInstance.Create(spec.Kind, new FracRect(0, 0, 0.1, 0.1)), Spec: spec)).ToList();
        foreach (var (model, spec) in models)
        {
        }

        var host = new NoticeRender.RenderCardHost(ticker)
        {
            EditMode = true,
            Design = new DesignState { CornerRadiusPx = radius },
            Selected = new HashSet<string>(models.Where(m => m.Spec.Selected).Select(m => m.Model.Id), StringComparer.Ordinal),
        };

        var cards = new List<WidgetCard>();
        var markers = new EditMarkerLayer();
        foreach (var (model, spec) in models)
        {
            var card = new WidgetCard(host, model) { Width = spec.W, Height = spec.H };
            card.ApplyEditMode(true);
            Canvas.SetLeft(card, spec.X);
            Canvas.SetTop(card, spec.Y);
            canvas.Children.Add(card);
            markers.Add(card);
            cards.Add(card);
        }

        canvas.Children.Add(markers.Halos);
        canvas.Children.Add(markers.Inks);

        // [S-2460] Several selected: the frame round them, [S-14310] and no marks of their own.
        RenderFrame.Round(markers, [.. cards.Zip(models).Select(c => (c.First, new DipRect(c.Second.Spec.X, c.Second.Spec.Y, c.Second.Spec.W, c.Second.Spec.H)))], host.Selected, editing: true, screen.XamlRoot?.RasterizationScale ?? 1, [host]);

        await Task.Delay(800).ConfigureAwait(true);
        await RenderHarness.SaveAsync(screen, path, "editbar").ConfigureAwait(true);

        foreach (var card in cards)
        {
            card.Detach();
        }
    }
}
#endif
