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
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2328] Debug only: draw the real edit bar ([S-2320]) and its flyouts off-screen to PNGs.
/// <c>Themia.exe --editbar-png &lt;outDir&gt; [--theme light|dark] [--only bar|pages|select]</c> writes the bar
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
    private const string Flag = "--editbar-png";

    private const double ScreenWidth = 1280;
    private const double ScreenHeight = 64;
    private const double OpenHeight = 900;

    /// <summary>
    /// The <c>--theme</c> of this render or of <c>--picker-png</c> / <c>--viewbar-png</c> / <c>--menu-png</c>, read before the app's resources
    /// load: the bar's brushes come from the application theme.
    /// </summary>
    public static ApplicationTheme? RequestedTheme(string[] args)
    {
        if (Array.IndexOf(args, Flag) < 0 && Array.IndexOf(args, "--picker-png") < 0 && Array.IndexOf(args, "--viewbar-png") < 0
            && Array.IndexOf(args, "--menu-png") < 0)
        {
            return null;
        }

        return Option(args, "--theme")?.ToLowerInvariant() switch
        {
            "light" => ApplicationTheme.Light,
            "dark" => ApplicationTheme.Dark,
            _ => null,
        };
    }

    /// <summary>
    /// The themed brushes follow the design's theme, not the application's (the surface sets its
    /// root to the design's theme too): a render sets both, as the widget render does.
    /// </summary>
    internal static void ApplyDesignTheme(Application app) => DesignApplier.Apply(new DesignState
    {
        Theme = app.RequestedTheme == ApplicationTheme.Light ? AppTheme.Light : AppTheme.Dark,
    });

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, $"usage: {Flag} <outDir> [--theme light|dark] [--only bar|pages|select]");
            app.Exit();
            return true;
        }

        var outDir = System.IO.Path.GetFullPath(args[at + 1]);
        var theme = app.RequestedTheme == ApplicationTheme.Light ? "light" : "dark";
        var only = Option(args, "--only");

        // What went wrong while drawing is said with the pictures.
        Log.AddSink(static record =>
        {
            if (record.Level >= LogLevel.Warn)
            {
                Console.WriteLine($"editbar: {record.Level} {record.Scope}: {record.Message} {record.Detail}");
            }
        });

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;

            try
            {
                System.IO.Directory.CreateDirectory(outDir);
                ApplyDesignTheme(app);

                var screen = new Grid
                {
                    Width = ScreenWidth,
                    Height = ScreenHeight,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,

                    // A stand-in wallpaper: the bar over a flat tint.
                    Background = DesignApplier.Brush("SolidBackgroundFillColorTertiaryBrush", Colors.DimGray),
                };

                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia edit bar render" };
                UiLanguage.ApplyLanguageOnly(screen);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((ScreenWidth + 40) * scale), (int)((OpenHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                if (only is null or "bar")
                {
                    await BarShots(screen, outDir, theme).ConfigureAwait(true);
                }

                if (only is null or "pages")
                {
                    await SettingsShots(screen, outDir, theme).ConfigureAwait(true);
                    await BarMenuShots(screen, outDir, theme).ConfigureAwait(true);
                }

                if (only is null or "select")
                {
                    ticker = new Ticker(dispatcher);
                    await SelectionShots(screen, ticker, outDir, theme).ConfigureAwait(true);
                    if (only is null)
                    {
                        await MarkerShots(screen, ticker, outDir, theme).ConfigureAwait(true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the edit bar failed", ex);
            }
            finally
            {
                ticker?.Dispose();
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    /// <summary>[S-2320]..[S-2324] The bar in its states, and the add menu and the placement line.</summary>
    private static async Task BarShots(Grid screen, string outDir, string theme)
    {
        var clockKind = ModuleCatalog.Of(new WidgetId("clock"));
        var clock = new BarSelection(clockKind.Name, clockKind.Glyph);
        var two = new BarSelection(Core.Shell.SelectionPage.Words(new QuickSetOffer(2, null, null, null, null, null) { CommonKind = new WidgetId("clock") }, id => ModuleCatalog.Of(id).Name)!, clock.Glyph);
        var mixed = new BarSelection(Str.Shell_Views_WidgetCount.Plural(3), Glyphs.Apps);
        var wide = ScreenWidth - (2 * ShellMetricsBarTop);

        await Shot(screen, Bar(new EditBarState(null, 0, null, false, wide)), outDir, $"editbar-{theme}.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(clock, 0, null, true, wide)), outDir, $"editbar-{theme}-widget.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(two, 0, null, false, wide)), outDir, $"editbar-{theme}-many.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(mixed, 0, null, false, wide)), outDir, $"editbar-{theme}-mixed.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(clock, 2, null, false, wide)), outDir, $"editbar-{theme}-errors.png").ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(null, 0, SettingsPageId.General, false, wide)), outDir, $"editbar-{theme}-open.png").ConfigureAwait(true);
        var longName = new BarSelection("Weather in San Francisco and the Bay Area", ModuleCatalog.Of(new WidgetId("weatherNow")).Glyph);
        await Shot(screen, Bar(new EditBarState(longName, 0, null, true, 900 - (2 * ShellMetricsBarTop))), outDir, $"editbar-{theme}-narrow.png", room: 900).ConfigureAwait(true);
        await Shot(screen, Bar(new EditBarState(longName, 0, null, true, 640 - (2 * ShellMetricsBarTop))), outDir, $"editbar-{theme}-narrowest.png", room: 640).ConfigureAwait(true);

        // [S-11712] The add page: Start, two subjects, a search with results and one with none, a narrow display.
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add.png"), ScreenWidth, OpenHeight, null, null).ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-system.png"), ScreenWidth, OpenHeight, AddFolderId.System, null).ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-weather.png"), ScreenWidth, OpenHeight, AddFolderId.Weather, null).ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-search.png"), ScreenWidth, OpenHeight, null, "music").ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-none.png"), ScreenWidth, OpenHeight, null, "zzqx").ConfigureAwait(true);
        await AddPageShot(screen, System.IO.Path.Combine(outDir, $"editbar-{theme}-add-narrow.png"), 900, OpenHeight, null, null).ConfigureAwait(true);

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

        var path = System.IO.Path.Combine(outDir, name);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
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
        var area = new PixelRect(0, 0, ScreenWidth, OpenHeight);
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
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
        view.Close();
        screen.Background = flat;
    }

    /// <summary>One widget of a selection shot: its kind, where its card is, its header, its title, and whether it is selected.</summary>
    private sealed record PropsCard(WidgetId Kind, double X, double Y, double W, double H, HeaderPlacement Header, bool Selected, string? Label = null, string? Style = null, string? ItemColor = null, double Scale = 1.0, string ItemText = "", FillChoice? Background = null, FillChoice? Items = null, ContentAlign? Align = null, string? Config = null);

    /// <summary>[S-2340]..[S-2343] The selection's page beside real cards, each case on the stand-in display.</summary>
    private static async Task SelectionShots(Grid screen, Ticker ticker, string outDir, string theme)
    {
        PropsCard[] clock = [new(new WidgetId("clock"), 140, 160, 300, 200, HeaderPlacement.Top, true, "Zürich", "flip", "#2A3248", 1.25, "#FFFFFF")];
        PropsCard[] many =
        [
            new(new WidgetId("clock"), 120, 160, 220, 160, HeaderPlacement.Top, true),
            new(new WidgetId("countdown"), 120, 360, 260, 180, HeaderPlacement.Bottom, true, Scale: 0.9),
            new(new WidgetId("clock"), 120, 580, 200, 140, HeaderPlacement.Hidden, true),
        ];
        PropsCard[] two = [new(new WidgetId("clock"), 120, 160, 220, 160, HeaderPlacement.Top, true), new(new WidgetId("clock"), 120, 360, 220, 160, HeaderPlacement.Bottom, true)];
        PropsCard[] toggle = [new(new WidgetId("wifiToggle"), 200, 260, 120, 120, HeaderPlacement.Hidden, true)];
        PropsCard[] weather = [new(new WidgetId("weatherNow"), 140, 160, 300, 200, HeaderPlacement.Top, true)];
        PropsCard[] shortcuts = [new(new WidgetId("shortcuts"), 40, 200, 300, 400, HeaderPlacement.Top, true)];
        PropsCard[] countdown = [new(new WidgetId("countdown"), 140, 160, 280, 180, HeaderPlacement.Top, true)];

        // [S-2847] The location searches answer from fixture places; [S-3323] the screen-time lines read the sample history.
        PlaceRender.UseFixture();
        AppServices.ScreenTime ??= new SampleScreenTime();
        UseSamplePools();

        (PropsCard[] Layout, string Name, string? Click, string? Type, Action<WidgetMenuLines>? Lines)[] cases =
        [
            (clock, "one", null, null, null),
            (clock, "style", "style", null, null),
            (clock, "header", "header", null, null),
            (clock, "background", "background", null, null),
            (clock, "appearance-end", null, null, null),
            (two, "two", null, null, null),
            (many, "many", null, null, null),
            (toggle, "toggle", null, null, null),
            (weather, "weather", null, null, null),
            (weather, "weather-location", "w/" + Str.Shell_Place_Location.Text(), "zur", null),
            (weather, "setup-weather", SetupOpen, null, null),
            (shortcuts, "shortcuts", null, null, null),
            (countdown, "vocabulary", null, null, Vocabulary),
            (countdown, "vocabulary-colour", "w/Accent", null, Vocabulary),
            (countdown, "vocabulary-feeds", "w/Feeds", null, Vocabulary),
            (countdown, "vocabulary-confirm", "w/Clear history", null, Vocabulary),
        ];

        foreach (var (layout, name, click, type, lines) in cases)
        {
            await SelectionShot(screen, ticker, layout, System.IO.Path.Combine(outDir, $"select-{theme}-{name}.png"), click, type, lines, scrollToEnd: name.EndsWith("-end", StringComparison.Ordinal)).ConfigureAwait(true);
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
        menu.Lines.Add(menu.Number(Glyphs.Count, "Rows", 4, 1, 10, 1, n => LocaleFormat.Integer(n), _ => { }));
        menu.Lines.Add(menu.Number(Glyphs.Timer, "Refresh every", 30, 5, 600, 5, n => LocaleFormat.Integer(n) + " min", _ => { }));
        menu.Lines.Add(menu.Slider(Glyphs.Volume, "Volume", 40, 0, 100, 5, n => LocaleFormat.Percent(Strings.Current, n), _ => { }));
        menu.Lines.Add(menu.Slider(Glyphs.Scale, "Gap between widgets", 12, 0, 48, 2, n => LocaleFormat.Integer(n) + " px", _ => { }));
        menu.Lines.Add(menu.Toggle(Glyphs.Bell, "Sound on finish", true, _ => { }));
        menu.Lines.Add(menu.Toggle(null, "Hide when idle", false, _ => { }));
        menu.Lines.Add(menu.PickFolder(Glyphs.Folder, "Folder", @"C:\Users\Me\Pictures\Wallpapers", _ => { }));
        menu.Lines.Add(menu.List(Glyphs.Rss, "Feeds", feeds, new MenuList<string>(f => f, "No feeds yet")
        {
            Rename = new MenuListRename<string>((_, _) => false),
            Add = new MenuListAdd<string>("Add a feed", "https://…", _ => null, f => f),
        }));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(new WidgetMenuLines(() => { }, _ => { }) { OpenSetting = _ => { } }.AccountsLine(Glyphs.Contact, "Manage accounts"));
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
            model.Header = spec.Header;
            model.Label = spec.Label;
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
            card.ApplyGap(0);
            card.ApplyEditMode(true);
            card.ApplySelection();
            card.ApplyContentScale(WidgetScale.Effective(1, model.Scale));
            Canvas.SetLeft(card, spec.X);
            Canvas.SetTop(card, spec.Y);
            canvas.Children.Add(card);
            markers.Add(card);
            cards.Add(card);
        }

        canvas.Children.Add(markers.Halos);
        canvas.Children.Add(markers.Inks);

        var page = SelectionPageOf(view, selected, lines);
        var area = new PixelRect(0, 0, ScreenWidth, OpenHeight);
        var flyout = HangFlyout(canvas, page, bar, barRect, null, area);
        page.Show();
        if (click is not null)
        {
            if (click == SetupOpen ? !page.OpenSetup() : !page.DebugClick(click))
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
        canvas.UpdateLayout();
        if (scrollToEnd)
        {
            flyout.Scroll(Windows.System.VirtualKey.End);
        }

        await Task.Delay(700).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
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
    /// (three selected beside one that is not) and <c>-neighbours.png</c> (cells meeting on their edges,
    /// at the usual 8 DIP gap: their marks are one line where they meet).
    /// </summary>
    private static async Task MarkerShots(Grid screen, Ticker ticker, string outDir, string theme)
    {
        PropsCard[] mixed =
        [
            new(new WidgetId("clock"), 120, 120, 280, 180, HeaderPlacement.Top, true),
            new(new WidgetId("countdown"), 460, 140, 240, 160, HeaderPlacement.Top, false),
            new(new WidgetId("wifiToggle"), 760, 160, 64, 64, HeaderPlacement.Hidden, true),
            new(new WidgetId("wifiToggle"), 860, 160, 64, 64, HeaderPlacement.Hidden, false),
        ];
        PropsCard[] multi =
        [
            new(new WidgetId("clock"), 120, 120, 240, 160, HeaderPlacement.Top, true),
            new(new WidgetId("countdown"), 380, 120, 240, 160, HeaderPlacement.Top, true),
            new(new WidgetId("clock"), 640, 120, 200, 160, HeaderPlacement.Hidden, true),
            new(new WidgetId("countdown"), 880, 120, 240, 160, HeaderPlacement.Top, false),
        ];

        await MarkerShot(screen, ticker, mixed, 4, System.IO.Path.Combine(outDir, $"marks-{theme}-r4.png")).ConfigureAwait(true);
        await MarkerShot(screen, ticker, mixed, 16, System.IO.Path.Combine(outDir, $"marks-{theme}-r16.png")).ConfigureAwait(true);
        await MarkerShot(screen, ticker, multi, 8, System.IO.Path.Combine(outDir, $"marks-{theme}-multi.png")).ConfigureAwait(true);

        PropsCard[] neighbours =
        [
            new(new WidgetId("clock"), 120, 100, 240, 150, HeaderPlacement.Top, true),
            new(new WidgetId("countdown"), 360, 100, 240, 150, HeaderPlacement.Top, false),
            new(new WidgetId("countdown"), 120, 250, 240, 150, HeaderPlacement.Top, false),
            new(new WidgetId("clock"), 360, 250, 240, 150, HeaderPlacement.Hidden, true),
            new(new WidgetId("wifiToggle"), 600, 100, 120, 120, HeaderPlacement.Hidden, false),
        ];
        await MarkerShot(screen, ticker, neighbours, 8, System.IO.Path.Combine(outDir, $"marks-{theme}-neighbours.png"), gap: 8).ConfigureAwait(true);
    }

    private static async Task MarkerShot(Grid screen, Ticker ticker, PropsCard[] layout, double radius, string path, double gap = 0)
    {
        screen.Children.Clear();
        screen.Height = 420;

        var canvas = new Canvas();
        screen.Children.Add(canvas);

        var models = layout.Select(spec => (Model: WidgetInstance.Create(spec.Kind, new FracRect(0, 0, 0.1, 0.1)), Spec: spec)).ToList();
        foreach (var (model, spec) in models)
        {
            model.Header = spec.Header;
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
            card.ApplyGap(gap);
            card.ApplyEditMode(true);
            Canvas.SetLeft(card, spec.X);
            Canvas.SetTop(card, spec.Y);
            canvas.Children.Add(card);
            markers.Add(card);
            cards.Add(card);
        }

        canvas.Children.Add(markers.Halos);
        canvas.Children.Add(markers.Inks);

        await Task.Delay(800).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");

        foreach (var card in cards)
        {
            card.Detach();
        }
    }

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
