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
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-3506] Debug only: draw the real edit bar ([S-3500]) at the top centre of a stand-in display,
/// off-screen, to PNGs. <c>Themia.exe --editbar-png &lt;outDir&gt; [--theme light|dark]</c> writes
/// <c>editbar-&lt;theme&gt;.png</c> (snap on), <c>editbar-&lt;theme&gt;-snapoff.png</c>,
/// <c>editbar-&lt;theme&gt;-add.png</c> (the add menu open under Add widget, as the flyout hangs) and
/// <c>editbar-&lt;theme&gt;-placing.png</c> (the placement line in the bar's place), and [S-3713]
/// the properties panel on a stand-in display with real cards in edit mode:
/// <c>props-&lt;theme&gt;-right.png</c> (a widget in the middle, of its own item colour),
/// <c>-left.png</c> (a widget at the right edge), <c>-below.png</c> (a widget spanning the width),
/// <c>-many.png</c> (three of mixed headers), <c>-toggle.png</c> (no Header choice) and
/// <c>-hidden.png</c> (header hidden, beside the same widget with one), [S-6569] <c>-fills.png</c>
/// (backgrounds Default, Transparent and a colour at 50 %, item backgrounds Default, Transparent and
/// a colour), <c>-noitems.png</c> (a stopwatch's Appearance: no item lines) and
/// <c>-picker.png</c> (Appearance › Background open: Transparent and the opacity), [S-6586]
/// <c>-align.png</c> (a clock aligned right), <c>-alignmany.png</c> (a clock and a quote of different
/// alignments) and <c>-alignstyle.png</c> (the moon's month: no Alignment line), and the submenus
/// open along a path (<c>-countdown-appearance.png</c>, a countdown's Appearance;
/// <c>-many-appearance.png</c>, several; the searches with few and many answers; [S-1862]
/// <c>-setup-*.png</c>, the menu as the Set up button opens it; [S-3724]..[S-3730]
/// <c>-vocabulary*.png</c>, one line of each kind on a stand-in widget) — all over a busy
/// stand-in wallpaper, so the panel's surface is judged against one ([S-3715]).
/// </summary>
internal static partial class EditBarRender
{
    private const string Scope = "editbar-render";
    private const string Flag = "--editbar-png";

    private const double ScreenWidth = 1280;
    private const double ScreenHeight = 140;
    private const double OpenHeight = 900;

    /// <summary>[S-3713] The stand-in display the strip is drawn on.</summary>
    private const double PropsHeight = 560;

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
            Log.Error(Scope, $"usage: {Flag} <outDir> [--theme light|dark]");
            app.Exit();
            return true;
        }

        var outDir = System.IO.Path.GetFullPath(args[at + 1]);
        var theme = app.RequestedTheme == ApplicationTheme.Light ? "light" : "dark";

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

                    // A stand-in wallpaper: the bar's acrylic over a flat tint.
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

                // [S-3535] --only settings: the settings menu's pictures alone; [S-3548] --only bar the View and Display menus'.
                switch (Option(args, "--only"))
                {
                    case "settings":
                        await SettingsShots(screen, outDir, theme).ConfigureAwait(true);
                        return;
                    case "bar":
                        await BarMenuShots(screen, outDir, theme).ConfigureAwait(true);
                        return;
                }

                // [S-3507] [S-3509] The bar: nothing selected (no widget crumb), a step to undo and none
                // to redo; snap off; one widget selected (its crumb, lit while its menu stands beside
                // it); several; both steps live and none; long names on a narrow display, every crumb
                // giving up the same.
                await Shot(screen, Bar(), null, outDir, $"editbar-{theme}.png").ConfigureAwait(true);
                await Shot(screen, Bar(Plain(snapOn: false)), null, outDir, $"editbar-{theme}-snapoff.png").ConfigureAwait(true);
                var clockName = ModuleCatalog.Of(new WidgetId("clock"));
                await Shot(screen, Bar(Plain(widget: new BarCrumb("Zürich", clockName.Glyph))), null, outDir, $"editbar-{theme}-widget.png").ConfigureAwait(true);
                await Shot(screen, Bar(Plain(widget: new BarCrumb(Str.Shell_Views_WidgetCount.Plural(3), Glyphs.Apps))), null, outDir, $"editbar-{theme}-many.png").ConfigureAwait(true);
                await Shot(screen, Bar(Plain(canUndo: true, canRedo: true)), null, outDir, $"editbar-{theme}-history.png").ConfigureAwait(true);
                await Shot(screen, Bar(Plain(canUndo: false, canRedo: false)), null, outDir, $"editbar-{theme}-nohistory.png").ConfigureAwait(true);
                var longNames = new EditBarState(
                    new BarCrumb("Samsung Odyssey Neo G9 Ultrawide", Glyphs.Display),
                    new BarCrumb("Evening and weekend at home", Glyphs.Moon),
                    new BarCrumb("Weather in San Francisco", ModuleCatalog.Of(new WidgetId("weatherNow")).Glyph),
                    WidgetOpen: true, CanUndo: true, CanRedo: false, SnapOn: true);
                await Shot(screen, Bar(longNames), null, outDir, $"editbar-{theme}-long.png").ConfigureAwait(true);
                await Shot(screen, Bar(longNames), null, outDir, $"editbar-{theme}-narrow.png", room: 900).ConfigureAwait(true);

                var menu = PickerRender.Presenter(new WidgetPicker(_ => { }, () => { }, new PixelRect(0, 0, PickerRender.WorkWidth, PickerRender.WorkHeight)));
                UiLanguage.ApplyFlow(menu);
                await Shot(screen, Bar(), menu, outDir, $"editbar-{theme}-add.png").ConfigureAwait(true);

                var hint = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = Str.Shell_Place_Hint.Format(("action", Str.Shell_Place_PlaceOne.Format(("name", ModuleCatalog.Of(new WidgetId("clock")).Label.Text())))),
                };
                var line = new Border { Child = hint, MinHeight = EditBar.ButtonSize + (2 * Fluent.Space1), Padding = new Thickness(Fluent.Space4, 0, Fluent.Space4, 0) };
                ChromeFrame.Apply(line);
                await Shot(screen, line, null, outDir, $"editbar-{theme}-placing.png").ConfigureAwait(true);

                ticker = new Ticker(dispatcher);
                await PropsShots(screen, ticker, outDir, theme).ConfigureAwait(true);
                await MarkerShots(screen, ticker, outDir, theme).ConfigureAwait(true);
                await SettingsShots(screen, outDir, theme).ConfigureAwait(true);
                await BarMenuShots(screen, outDir, theme).ConfigureAwait(true);
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

    /// <summary>[S-3507] The stand-in display and view the bar's trail names: a real monitor's name, a view with its mark.</summary>
    private static readonly BarCrumb RenderDisplay = new("DELL U2723QE", Glyphs.Display, Detail: "DELL U2723QE — 3840 × 2160 (primary)");

    private static readonly BarCrumb RenderView = new("Work", Glyphs.Mail, "#0099BC");

    /// <summary>[S-3500] The bar as a render shows it: nothing selected, a step to undo and none to redo, snap on.</summary>
    private static EditBarState Plain(bool snapOn = true, BarCrumb? widget = null, bool canUndo = true, bool canRedo = false)
        => new(RenderDisplay, RenderView, widget, WidgetOpen: widget is not null, canUndo, canRedo, snapOn);

    /// <summary>[S-3500] The real bar, built and showing <paramref name="state"/>, its commands doing nothing.</summary>
    private static EditBar Bar(EditBarState? state = null)
    {
        var commands = new EditBarCommands(
            AddFlyout: () => new Flyout(),
            Menu: _ => { },
            Widget: () => { },
            MenuOpening: () => { },
            Undo: () => { },
            Redo: () => { },
            SetSnap: _ => { },
            Done: () => { });
        var bar = new EditBar(commands);
        bar.Fill(commands);
        bar.Show(state ?? Plain());
        return Flowing(bar);
    }

    /// <summary>[S-1113] The bar flows with the language, as the desktop sets it; the screen around it does not.</summary>
    private static EditBar Flowing(EditBar bar)
    {
        UiLanguage.ApplyFlow(bar);
        return bar;
    }

    /// <summary>One picture: the bar centred 8 DIPs under the display's top, and the menu hanging under its Add button.</summary>
    private static async Task Shot(Grid screen, FrameworkElement bar, FrameworkElement? menu, string outDir, string name, double? room = null)
    {
        screen.Children.Clear();
        screen.Height = menu is null ? ScreenHeight : OpenHeight;
        screen.Width = room ?? ScreenWidth;

        var canvas = new Canvas();
        screen.Children.Add(canvas);

        // [S-3507] On a narrow display the bar has the display's width (less 8 DIPs either side, its
        // own margin), centred in it as the surface centres it in the work area.
        FrameworkElement placed = bar;
        if (room is { } narrow)
        {
            placed = new Grid { Width = narrow, Children = { bar } };
        }

        canvas.Children.Add(placed);
        placed.Measure(new Size(room ?? double.PositiveInfinity, double.PositiveInfinity));
        var barX = room is null ? Math.Round((ScreenWidth - bar.DesiredSize.Width) / 2) : 0;
        Canvas.SetLeft(placed, barX);
        Canvas.SetTop(placed, Fluent.Space2);

        if (menu is not null)
        {
            canvas.Children.Add(menu);
            menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // A flyout opened from Add widget: centred under the button (the bar's first, 4 DIPs in), a gap below the bar.
            screen.UpdateLayout();
            var addWidth = bar is EditBar edit ? edit.AddWidth : 0;
            var addCentre = barX + (bar is EditBar hung ? hung.AddOffset : 0) + (addWidth / 2);
            Canvas.SetLeft(menu, Math.Clamp(addCentre - (menu.DesiredSize.Width / 2), Fluent.Space2, ScreenWidth - menu.DesiredSize.Width - Fluent.Space2));
            Canvas.SetTop(menu, Fluent.Space2 + bar.DesiredSize.Height + Fluent.Space1);
        }

        await Task.Delay(500).ConfigureAwait(true);

        var path = System.IO.Path.Combine(outDir, name);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
        screen.Width = ScreenWidth;
    }

    /// <summary>One widget of a properties shot: its kind, where its card is, its header, its title, and whether it is selected.</summary>
    private sealed record PropsCard(WidgetId Kind, double X, double Y, double W, double H, HeaderPlacement Header, bool Selected, string? Label = null, string? Style = null, string? ItemColor = null, double Scale = 1.0, string ItemText = "", FillChoice? Background = null, FillChoice? Items = null, ContentAlign? Align = null, string? Config = null);

    /// <summary>[S-3713] The panel beside real cards in edit mode, each case on the stand-in display.</summary>
    private static async Task PropsShots(Grid screen, Ticker ticker, string outDir, string theme)
    {
        PropsCard[][] cases =
        [
            // [S-6521] A widget of its own item colour: the Item opacity row shows. [S-6531] At 125 %.
            // [S-6544] Its own item text colour too: white figures on its dark flip cards in either theme.
            [new(new WidgetId("clock"), 500, 160, 280, 180, HeaderPlacement.Top, true, "Zürich", "flip", "#2A3248", 1.25, "#FFFFFF")],
            [new(new WidgetId("countdown"), 980, 160, 280, 180, HeaderPlacement.Top, true)],
            [new(new WidgetId("shortcuts"), 16, 72, 1248, 150, HeaderPlacement.Top, true)],
            [
                new(new WidgetId("clock"), 300, 200, 220, 160, HeaderPlacement.Top, true),
                new(new WidgetId("countdown"), 540, 240, 260, 180, HeaderPlacement.Bottom, true, Scale: 0.9),
                new(new WidgetId("clock"), 820, 200, 200, 140, HeaderPlacement.Hidden, true),
            ],
            [new(new WidgetId("wifiToggle"), 580, 260, 120, 120, HeaderPlacement.Hidden, true)],
            [
                new(new WidgetId("countdown"), 360, 260, 280, 180, HeaderPlacement.Hidden, true),
                new(new WidgetId("countdown"), 680, 260, 280, 180, HeaderPlacement.Top, false),
            ],

            // [S-6569] Backgrounds Default, Transparent and a colour at 50 %; items Default,
            // Transparent and a colour. The selected one (a colour at 50 %) has the panel.
            [
                new(new WidgetId("clock"), 40, 80, 300, 200, HeaderPlacement.Top, false, "Default", "flip"),
                new(new WidgetId("clock"), 40, 320, 300, 200, HeaderPlacement.Top, false, "Transparent", "flip", Background: FillChoice.Transparent(), Items: FillChoice.Transparent()),
                new(new WidgetId("clock"), 380, 200, 300, 220, HeaderPlacement.Top, true, "50 %", "flip", Background: FillChoice.Of("#1B2030", 0.5), Items: FillChoice.Of("#3A6EA5", 0.8)),
            ],

            // [S-6584] A clock aligned right: Right lit. Two of different alignments: none lit. The
            // moon's month style: no Alignment line.
            [new(new WidgetId("clock"), 500, 200, 300, 180, HeaderPlacement.Top, true, "Right", Align: ContentAlign.Right)],
            [
                new(new WidgetId("clock"), 300, 200, 280, 160, HeaderPlacement.Top, true, "Left", Align: ContentAlign.Left),
                new(new WidgetId("quote"), 620, 200, 320, 180, HeaderPlacement.Top, true, Align: ContentAlign.Right),
            ],
            [new(new WidgetId("moon"), 500, 200, 280, 220, HeaderPlacement.Top, true, Style: "phaseMonth")],
        ];
        string[] names = ["right", "left", "below", "many", "toggle", "hidden", "fills", "align", "alignmany", "alignstyle"];

        for (var i = 0; i < cases.Length; i++)
        {
            await PropsShot(screen, ticker, cases[i], System.IO.Path.Combine(outDir, $"props-{theme}-{names[i]}.png")).ConfigureAwait(true);
        }

        // [S-3718] [S-3723] [S-3725] The submenus along a path open (and a search typed into).
        var appearance = Str.Settings_Section_Appearance.Text();
        var header = Str.App_ConfigHeaderLabel.Text();
        PropsCard[] picker = [new(new WidgetId("clock"), 200, 160, 300, 200, HeaderPlacement.Top, true, Style: "flip", Background: FillChoice.Of("#1B2030", 0.5))];
        PropsCard[] stopwatch = [new(new WidgetId("stopwatch"), 200, 160, 280, 180, HeaderPlacement.Top, true)];
        PropsCard[] countdown = [new(new WidgetId("countdown"), 200, 160, 280, 180, HeaderPlacement.Top, true)];
        PropsCard[] many =
        [
            new(new WidgetId("clock"), 120, 160, 220, 160, HeaderPlacement.Top, true),
            new(new WidgetId("countdown"), 360, 160, 260, 180, HeaderPlacement.Bottom, true, Scale: 0.9),
        ];
        PropsCard[] shortcuts = [new(new WidgetId("shortcuts"), 120, 140, 520, 220, HeaderPlacement.Top, true)];
        PropsCard[] clock = [new(new WidgetId("clock"), 200, 160, 300, 200, HeaderPlacement.Top, true, "Zürich", "digital")];
        PropsCard[] weather = [new(new WidgetId("weatherNow"), 200, 160, 300, 200, HeaderPlacement.Top, true)];
        (PropsCard[] Layout, string Name, string? Open, string? Type)[] submenus =
        [
            (picker, "picker", appearance + "/" + Str.Settings_Style_BgLabel.Text(), null),
            (stopwatch, "noitems", appearance, null),
            (countdown, "countdown-appearance", appearance, null),
            (many, "many-appearance", appearance + "/" + Str.Shell_Items_TextShort.Text(), null),
            (shortcuts, "shortcuts-style", Str.Styles_Label.Text(), null),
            (shortcuts, "shortcuts-appearance", Str.Settings_Section_Appearance.Text(), null),
            (shortcuts, "shortcuts-background", Str.Settings_Section_Appearance.Text() + "/" + Str.Settings_Style_BgLabel.Text(), null),
            (shortcuts, "shortcuts-add", Str.Common_Add.Text(), null),
            (shortcuts, "shortcuts-items", "Shortcuts", null),
            (clock, "clock-style", Str.Styles_Label.Text(), null),

            // [S-3735] The Header level: the Title row and the positions for one, the positions for several;
            // [S-3736] the Title's own level, the field alone.
            (clock, "header", header, null),
            (clock, "header-title", header + "/" + Str.Group_Title_Label.Text(), null),
            (many, "many-header", header, null),

            // [S-3734] The one rectangle wherever it stands: beside a widget at the work area's right
            // edge (on its left), one at its bottom (slid up inside the area), and a wide one.
            ([new(new WidgetId("clock"), 940, 160, 320, 220, HeaderPlacement.Top, true, Style: "flip")], "flip-right", appearance + "/" + Str.Settings_Style_BgLabel.Text(), null),
            ([new(new WidgetId("clock"), 200, 700, 300, 180, HeaderPlacement.Top, true, Style: "flip")], "flip-bottom", appearance + "/" + Str.Settings_Style_BgLabel.Text(), null),
            ([new(new WidgetId("clock"), 400, 120, 400, 300, HeaderPlacement.Top, true, Style: "flip")], "flip-wide", appearance + "/" + Str.Settings_Style_BgLabel.Text(), null),
            (clock, "clock-zone", "Timezone", "tok"),
            (clock, "clock-zone-many", "Timezone", "a"),
            (clock, "clock-zone-none", "Timezone", "qqqq"),
            (weather, "weather-location", Str.Shell_Place_Location.Text(), null),

            // [S-1862] What the Set up button opens: the line the module names, through its submenus.
            (weather, "setup-weather", SetupOpen, null),
            ([new(new WidgetId("rss"), 200, 160, 340, 260, HeaderPlacement.Top, true)], "setup-rss", SetupOpen, null),
            ([new(new WidgetId("stocks"), 200, 160, 300, 200, HeaderPlacement.Top, true)], "setup-stocks", SetupOpen, null),
            ([new(new WidgetId("gallery"), 200, 160, 320, 240, HeaderPlacement.Top, true)], "setup-gallery", SetupOpen, null),

            // The meters' and details' lines: the span under Style, a drive, the ping host, a choice.
            ([new(new WidgetId("cpu"), 200, 160, 280, 180, HeaderPlacement.Top, true)], "cpu-span", Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text(), null),
            ([new(new WidgetId("ping"), 200, 160, 280, 180, HeaderPlacement.Top, true)], "ping-style", Str.Styles_Label.Text(), null),
            ([new(new WidgetId("disk"), 200, 160, 280, 180, HeaderPlacement.Top, true)], "disk-drive", "Drive", null),
            ([new(new WidgetId("pingDetails"), 200, 160, 300, 220, HeaderPlacement.Top, true)], "pingdetails-window", "Statistics cover", null),
            ([new(new WidgetId("topProcesses"), 200, 160, 300, 240, HeaderPlacement.Top, true)], "processes-rows", "Rows", null),
            ([new(new WidgetId("networkDetails"), 200, 160, 300, 240, HeaderPlacement.Top, true)], "networkdetails-appearance", appearance, null),

            // The sky's and the rain's location searches, typed into against the fixture geocoder; the
            // screen-time lines over the sample history.
            ([new(new WidgetId("sun"), 200, 160, 320, 240, HeaderPlacement.Top, true)], "sun-location", Str.Shell_Place_Location.Text(), "zur"),
            ([new(new WidgetId("sun"), 200, 160, 320, 240, HeaderPlacement.Top, true)], "sun-style", Str.Styles_Label.Text(), null),
            ([new(new WidgetId("moon"), 200, 160, 280, 220, HeaderPlacement.Top, true, Style: "phaseMonth")], "moon-location", Str.Shell_Place_Location.Text(), null),
            ([new(new WidgetId("rainNextHour"), 200, 160, 300, 180, HeaderPlacement.Top, true)], "rain-location", Str.Shell_Place_Location.Text(), "spring"),
            ([new(new WidgetId("screenTimeApps"), 200, 160, 300, 240, HeaderPlacement.Top, true)], "screentimeapps-limits", Str.WidgetsC_ScreenTime_Settings_Limits.Text(), null),
            ([new(new WidgetId("screenTimeChart"), 200, 160, 320, 200, HeaderPlacement.Top, true)], "screentimechart-limits", Str.WidgetsC_ScreenTime_Settings_Limits.Text(), null),
            ([new(new WidgetId("screenTimeDonut"), 200, 160, 240, 240, HeaderPlacement.Top, true)], "screentimedonut-period", Str.Screentime_Cfg_Period.Text(), null),

            // The time and the local stores' widgets: their first levels and a submenu each.
            ([new(new WidgetId("alarm"), 200, 160, 280, 180, HeaderPlacement.Top, true)], "alarm-repeat", "Repeats on", null),
            ([new(new WidgetId("alarm"), 200, 160, 280, 180, HeaderPlacement.Top, true)], "alarm-sound", "Sound", null),
            ([new(new WidgetId("countdown"), 200, 160, 280, 180, HeaderPlacement.Top, true)], "countdown-format", "Format", null),
            ([new(new WidgetId("pomodoro"), 200, 160, 280, 200, HeaderPlacement.Top, true)], "pomodoro-volume", "Alarm volume", null),
            ([new(new WidgetId("focusSession"), 200, 160, 240, 200, HeaderPlacement.Top, true)], "focussession", appearance, null),
            ([new(new WidgetId("clipboard"), 200, 160, 300, 240, HeaderPlacement.Top, true)], "clipboard", appearance, null),
            ([new(new WidgetId("scratchpad"), 200, 160, 300, 240, HeaderPlacement.Top, true)], "scratchpad-pen", "Pen color", null),
            ([new(new WidgetId("habitTracker"), 200, 160, 300, 240, HeaderPlacement.Top, true)], "habits-list", "Habit list", null),
            ([new(new WidgetId("habitTracker"), 200, 160, 300, 240, HeaderPlacement.Top, true)], "habits-style", Str.Styles_Label.Text(), null),
            ([new(new WidgetId("notes"), 200, 160, 300, 240, HeaderPlacement.Top, true)], "notes-note", "Note", null),
            ([new(new WidgetId("quote"), 200, 160, 320, 180, HeaderPlacement.Top, true)], "quote-quotes", "Quotes", null),

            // The files', media's, devices' and markets' widgets: what is under Style, a path, a device, a pair.
            ([new(new WidgetId("folder"), 200, 160, 320, 240, HeaderPlacement.Top, true, Style: "list")], "folder-style", "Sort by", null),
            ([new(new WidgetId("folder"), 200, 160, 320, 240, HeaderPlacement.Top, true)], "folder-folder", "Folder", null),
            ([new(new WidgetId("gallery"), 200, 160, 320, 240, HeaderPlacement.Top, true)], "gallery-source", "Source", null),
            // [S-1236] With one output device the line is there while a device is chosen, so the choice can go back to the default.
            ([new(new WidgetId("mixer"), 200, 160, 320, 240, HeaderPlacement.Top, true, Config: """{"deviceId":"{0.0.0.00000000}.{render-headphones}","deviceName":"Headphones (Render)"}""")], "mixer-device", "Output device", null),
            ([new(new WidgetId("music"), 200, 160, 340, 160, HeaderPlacement.Top, true)], "music-hide", "Hide when idle", null),
            ([new(new WidgetId("bluetooth"), 200, 160, 300, 240, HeaderPlacement.Top, true)], "bluetooth", appearance, null),
            ([new(new WidgetId("videoStream"), 200, 160, 320, 200, HeaderPlacement.Top, true)], "videostream-rotation", "Rotation", null),
            ([new(new WidgetId("stocks"), 200, 160, 300, 200, HeaderPlacement.Top, true)], "stocks-period", "Timeframe", null),
            ([new(new WidgetId("exchangeRates"), 200, 160, 300, 200, HeaderPlacement.Top, true)], "rates-base", "Base currency", "c"),

            // The account and list widgets: the feeds, the accounts (none signed in to a render), a
            // style's own lines, where tasks come from and their filters.
            ([new(new WidgetId("rss"), 200, 160, 340, 260, HeaderPlacement.Top, true)], "rss-feeds", "Feeds", null),
            ([new(new WidgetId("email"), 200, 160, 340, 260, HeaderPlacement.Top, true, Style: "table")], "email-columns", "Columns", null),
            ([new(new WidgetId("email"), 200, 160, 340, 260, HeaderPlacement.Top, true)], "email-accounts", Str.WidgetUi_AccountsLabel.Text(), null),
            ([new(new WidgetId("calendar"), 200, 160, 340, 260, HeaderPlacement.Top, true, Style: "agenda")], "calendar-range", "Date range", null),
            ([new(new WidgetId("contacts"), 200, 160, 320, 240, HeaderPlacement.Top, true)], "contacts-layout", "Layout", null),
            // [S-5324] No task account is signed in to a render: a chosen one since signed out keeps the line, listed by its id.
            ([new(new WidgetId("weekPlanner"), 200, 160, 400, 240, HeaderPlacement.Top, true, Config: """{"taskAccountId":"signed-out-account"}""")], "weekplanner-tasks", "Tasks from", null),

            // [S-7516] No task service runs in a render: the first level says so, over the lines that do not need one.
            ([new(new WidgetId("tasks"), 200, 160, 300, 260, HeaderPlacement.Top, true)], "tasks-lines", null, null),
            ([new(new WidgetId("tasks"), 200, 160, 300, 260, HeaderPlacement.Top, true)], "tasks-filter", "Filter", null),

            // [S-6075] A gallery of one picture: no Style line, its Picture size on top. [S-728] A weather
            // widget under a typed title. [S-6573] [S-6574] A meter's graph: no item lines; its cores:
            // Item background without Item text. The chart's styles ([S-3336]).
            ([new(new WidgetId("gallery"), 200, 160, 320, 240, HeaderPlacement.Top, true, Config: """{"source":"Bing"}""")], "gallery-one", null, null),
            ([new(new WidgetId("weatherNow"), 200, 160, 300, 200, HeaderPlacement.Top, true, "Home")], "weather-titled", Str.Shell_Place_Location.Text(), null),
            ([new(new WidgetId("cpu"), 200, 160, 280, 180, HeaderPlacement.Top, true)], "cpu-appearance", appearance, null),
            ([new(new WidgetId("cpu"), 200, 160, 280, 180, HeaderPlacement.Top, true, Style: "cores")], "cores-appearance", appearance, null),
            ([new(new WidgetId("screenTimeChart"), 200, 160, 320, 200, HeaderPlacement.Top, true)], "screentimechart-style", Str.Styles_Label.Text(), null),

            // [S-3749] [S-3590] Link rows to what a widget follows and does not set: the weather's unit and
            // interval, the clock's time format, a month's start of the week, the feeds' interval, the
            // mail's accounts (none signed in to a render: Add account).
            (weather, "links-weather", null, null),
            (clock, "links-clock", null, null),
            ([new(new WidgetId("calendar"), 200, 160, 340, 260, HeaderPlacement.Top, true, Style: "month")], "links-calendar", null, null),
            ([new(new WidgetId("rss"), 200, 160, 340, 260, HeaderPlacement.Top, true)], "links-rss", null, null),
            ([new(new WidgetId("email"), 200, 160, 340, 260, HeaderPlacement.Top, true)], "links-email", Str.WidgetUi_AccountsLabel.Text(), null),
        ];

        // [S-2847] The location searches answer from fixture places; [S-3323] the screen-time lines read the sample history.
        PlaceRender.UseFixture();
        AppServices.ScreenTime ??= new SampleScreenTime();
        UseSamplePools();

        foreach (var (layout, name, path, type) in submenus)
        {
            await PropsShot(screen, ticker, layout, System.IO.Path.Combine(outDir, $"props-{theme}-{name}.png"), open: path, type: type).ConfigureAwait(true);
        }

        // [S-3722] [S-3734] A search in the one rectangle: nothing typed, a question out, two answers,
        // five, six; the zone's 41 answers scrolled to their end under the pinned head and field.
        (PropsCard[] Layout, string Name, string Open, string? Type, bool End)[] heights =
        [
            (weather, "search-empty", Str.Shell_Place_Location.Text(), null, false),
            (weather, "search-searching", Str.Shell_Place_Location.Text(), PlaceRender.Unanswered, false),
            (weather, "search-two", Str.Shell_Place_Location.Text(), "zur", false),
            (weather, "search-five", Str.Shell_Place_Location.Text(), "spring", false),
            (weather, "search-six", Str.Shell_Place_Location.Text(), "san", false),
            (clock, "search-zone-end", "Timezone", "a", true),
        ];
        foreach (var (layout, name, path, type, end) in heights)
        {
            await PropsShot(screen, ticker, layout, System.IO.Path.Combine(outDir, $"props-{theme}-{name}.png"), open: path, type: type, scrollToEnd: end).ConfigureAwait(true);
        }

        // [S-2855] Use my location on the first level: off (the search under it), on and located (no
        // search), finding it, turned off in Windows (with the settings line) and not available; and
        // the Sun's, on. A stand-in state, never Windows' own.
        PropsCard[] sun = [new(new WidgetId("sun"), 200, 160, 320, 240, HeaderPlacement.Top, true)];
        (PropsCard[] Layout, string Name, string? Device)[] devices =
        [
            (weather, "device-off", null),
            (weather, "device-located", "located"),
            (weather, "device-locating", "locating"),
            (weather, "device-denied", "denied"),
            (weather, "device-unavailable", "unavailable"),
            (sun, "device-sun", "located"),
        ];
        foreach (var (layout, name, device) in devices)
        {
            AppServices.DebugDeviceLocation = device is null ? null : PlaceRender.Device(device);
            var config = device is null ? null : """{"useDeviceLocation":true}""";
            await PropsShot(screen, ticker, [.. layout.Select(c => c with { Config = config })], System.IO.Path.Combine(outDir, $"props-{theme}-{name}.png")).ConfigureAwait(true);
        }

        AppServices.DebugDeviceLocation = null;

        // [S-3732] The shortcuts' list with its first line held over the gap after the last.
        await PropsShot(screen, ticker, shortcuts, System.IO.Path.Combine(outDir, $"props-{theme}-shortcuts-drag.png"), open: "Shortcuts", drag: "drag:0:2").ConfigureAwait(true);

        // [S-3724]..[S-3730] Every row of the menu vocabulary on a stand-in widget's menu: the first
        // level (a note, a colour, a fill, numbers, a switch, a slider, a folder, a link, a confirmed
        // danger row), the list open, an item's own level, the colour open; [S-3736] a number asked
        // for as text (its field's level, refused while it is out of range); [S-3737] the "are you sure?".
        (string Name, string? Open, string? Type)[] vocabulary =
        [
            ("vocabulary", null, null),
            ("vocabulary-list", "Feeds", null),
            ("vocabulary-item", "Feeds/Blog feed", null),
            ("vocabulary-colour", "Accent", null),
            ("vocabulary-ask", "Refresh every", null),
            ("vocabulary-ask-refused", "Refresh every", "9999"),
            ("vocabulary-confirm", "Clear history", null),
        ];
        foreach (var (name, path, type) in vocabulary)
        {
            await PropsShot(screen, ticker, countdown, System.IO.Path.Combine(outDir, $"props-{theme}-{name}.png"), open: path, lines: Vocabulary, type: type).ConfigureAwait(true);
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

    /// <summary>[S-1862] The path that opens the menu as the card's Set up button does.</summary>
    private const string SetupOpen = "<setup>";

    private static async Task PropsShot(Grid screen, Ticker ticker, PropsCard[] layout, string path, string? open = null, string? type = null, Action<WidgetMenuLines>? lines = null, string? drag = null, string? click = null, bool scrollToEnd = false)
    {
        screen.Children.Clear();

        // The open control's panel hangs below the display's lower edge: the picture is taller.
        screen.Height = open is not null ? OpenHeight : PropsHeight;
        var flat = screen.Background;
        screen.Background = Wallpaper();

        var canvas = new Canvas { Clip = new RectangleGeometry { Rect = new Rect(0, 0, ScreenWidth, screen.Height) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        var chosen = layout.Where(s => s.Selected).ToList();
        var bar = Bar(Plain(widget: chosen.Count switch
        {
            0 => null,
            1 => new BarCrumb(chosen[0].Label ?? ModuleCatalog.Of(chosen[0].Kind).Name, ModuleCatalog.Of(chosen[0].Kind).Glyph),
            _ => new BarCrumb(Str.Shell_Views_WidgetCount.Plural(chosen.Count), Glyphs.Apps),
        }));
        canvas.Children.Add(bar);
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var barRect = new PixelRect(Math.Round((ScreenWidth - bar.DesiredSize.Width) / 2), Fluent.Space2, bar.DesiredSize.Width, bar.DesiredSize.Height);
        Canvas.SetLeft(bar, barRect.X);
        Canvas.SetTop(bar, barRect.Y);

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
                // A config other than the default, as the state file would hold it (a chosen device, a signed-out account).
                model.Config = Themia.Core.Storage.StateJson.Merged(model.Config, fragment);
            }

            if (spec.Style is { } style && WidgetStyles.Find(spec.Kind, style) is { } option)
            {
                WidgetStyles.Set(model.Config, option.Value); // [S-6007] the strip's Style shows the current one's icon
            }
            view.Widgets.Add(model);
            widgets.Add((model, spec));
        }

        var selected = widgets.Where(w => w.Spec.Selected).Select(w => w.Model.Id).ToList();
        var host = new NoticeRender.RenderCardHost(ticker) { EditMode = true, Selected = new HashSet<string>(selected, StringComparer.Ordinal) };

        var cards = new List<WidgetCard>();
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
            cards.Add(card);
        }

        var state = new AppState { Views = [view] };
        var selection = new WidgetSelection();
        selection.Replace(view.Id, selected);

        var strip = new PropertiesStrip(new PropertiesCommands(_ => { }, _ => { }, (_, _) => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { },
            Cut: () => { }, Copy: () => { }, Delete: () => { },
            WidgetLines: (id, ask, confirm) => lines is not null ? Stand(lines, ask, confirm) : view.Find(id) is { } found ? Services.ModuleHost.MenuLines(found, () => { }, ask, confirm, openSetting: _ => { }) : null));
        UiLanguage.ApplyFlow(strip);
        strip.RoomHeight = screen.Height - (2 * StripPlacement.Gap);
        strip.ShowSelection(QuickSet.For(state, selection, view.Id));
        strip.Visibility = Visibility.Visible;
        canvas.Children.Add(strip);
        strip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var target = StripPlacement.Bounds(widgets.Where(w => w.Spec.Selected).Select(w => new PixelRect(w.Spec.X, w.Spec.Y, w.Spec.W, w.Spec.H)));
        if (target is { } box)
        {
            var area = new PixelRect(0, 0, ScreenWidth, screen.Height);
            var spot = StripPlacement.Place(box, strip.DesiredSize.Width, strip.DesiredSize.Height, area, [barRect], UiLanguage.Flow == FlowDirection.RightToLeft);
            Canvas.SetLeft(strip, Math.Round(spot.X));
            Canvas.SetTop(strip, Math.Round(spot.Y));
        }

        // [S-3718] The levels along a path open, as clicks would open them; a field is typed into.
        if (open is not null)
        {
            strip.UpdateLayout();
            if (open == SetupOpen ? !strip.OpenSetup() : !strip.DebugOpen(open))
            {
                Console.WriteLine($"editbar: no submenu {open} for {path}");
            }

            if (type is not null)
            {
                await Task.Delay(100).ConfigureAwait(true);
                strip.DebugType(type);
            }

            // A row of the level clicked once its answers have come.
            if (click is not null)
            {
                await Task.Delay(700).ConfigureAwait(true);
                if (!strip.DebugClick(click))
                {
                    Console.WriteLine($"editbar: no line {click} for {path}");
                }
            }

            // [S-3734] A long level scrolled to its end under its pinned head and field.
            if (scrollToEnd)
            {
                await Task.Delay(700).ConfigureAwait(true);
                strip.DebugScrollToEnd();
            }

            // [S-3732] A list's line held mid-drag: "drag:<from>:<gap>".
            if (drag is { } held && held.Split(':') is [_, var from, var gap] && !strip.DebugDrag(int.Parse(from, System.Globalization.CultureInfo.InvariantCulture), int.Parse(gap, System.Globalization.CultureInfo.InvariantCulture)))
            {
                Console.WriteLine($"editbar: no drag {held} for {path}");
            }
        }

        await Task.Delay(800).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
        screen.Background = flat;

        foreach (var card in cards)
        {
            card.Detach();
        }
    }

    private static WidgetMenuLines Stand(Action<WidgetMenuLines> build, Action<WidgetTextAsk> ask, Action<WidgetConfirm> confirm)
    {
        var menu = new WidgetMenuLines(() => { }, ask, confirm);
        build(menu);
        return menu;
    }

    /// <summary>
    /// [S-31b]/[S-31c] The edit markers on real cards: <c>marks-&lt;theme&gt;-r4.png</c> and <c>-r16.png</c>
    /// (a selected card, an unselected one and an XS tile at that corner radius) and <c>-multi.png</c>
    /// (three selected beside one that is not).
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
    }

    private static async Task MarkerShot(Grid screen, Ticker ticker, PropsCard[] layout, double radius, string path)
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
        foreach (var (model, spec) in models)
        {
            var card = new WidgetCard(host, model) { Width = spec.W, Height = spec.H };
            card.ApplyGap(0);
            card.ApplyEditMode(true);
            Canvas.SetLeft(card, spec.X);
            Canvas.SetTop(card, spec.Y);
            canvas.Children.Add(card);
            cards.Add(card);
        }

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
