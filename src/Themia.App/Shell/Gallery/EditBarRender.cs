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
/// a colour), <c>-noitems.png</c> (a stopwatch: the Item background row greyed) and
/// <c>-picker.png</c> (the Background control's panel open beside the panel: Transparent and the
/// opacity slider), [S-6586] <c>-align.png</c> (a clock aligned right), <c>-alignmany.png</c> (a clock
/// and a quote of different alignments) and <c>-alignstyle.png</c> (the moon's month: the row greyed) — all over a busy stand-in wallpaper, so the panel's surface is judged against
/// one ([S-3715]).
/// </summary>
internal static class EditBarRender
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

                await Shot(screen, Bar(snapOn: true), null, outDir, $"editbar-{theme}.png").ConfigureAwait(true);
                await Shot(screen, Bar(snapOn: false), null, outDir, $"editbar-{theme}-snapoff.png").ConfigureAwait(true);

                var menu = PickerRender.Presenter(new WidgetPicker(_ => { }, () => { }, new PixelRect(0, 0, PickerRender.WorkWidth, PickerRender.WorkHeight)));
                UiLanguage.ApplyFlow(menu);
                await Shot(screen, Bar(snapOn: true), menu, outDir, $"editbar-{theme}-add.png").ConfigureAwait(true);

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

    private static EditBar Bar(bool snapOn) => Flowing(new EditBar(new EditBarCommands(
        AddFlyout: () => new Flyout(),
        FillViews: _ => { },
        SnapOn: snapOn,
        SetSnap: _ => { },
        Settings: () => { },
        Exit: () => { },
        Done: () => { })));

    /// <summary>[S-1113] The bar flows with the language, as the desktop sets it; the screen around it does not.</summary>
    private static EditBar Flowing(EditBar bar)
    {
        UiLanguage.ApplyFlow(bar);
        return bar;
    }

    /// <summary>One picture: the bar centred 8 DIPs under the display's top, and the menu hanging under its Add button.</summary>
    private static async Task Shot(Grid screen, FrameworkElement bar, FrameworkElement? menu, string outDir, string name)
    {
        screen.Children.Clear();
        screen.Height = menu is null ? ScreenHeight : OpenHeight;

        var canvas = new Canvas();
        screen.Children.Add(canvas);
        canvas.Children.Add(bar);

        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var barX = Math.Round((ScreenWidth - bar.DesiredSize.Width) / 2);
        Canvas.SetLeft(bar, barX);
        Canvas.SetTop(bar, Fluent.Space2);

        if (menu is not null)
        {
            canvas.Children.Add(menu);
            menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // A flyout opened from Add widget: centred under the button (the bar's first, 4 DIPs in), a gap below the bar.
            var addWidth = bar is EditBar edit ? edit.AddWidth : 0;
            var addCentre = barX + Fluent.Hairline + Fluent.Space1 + (addWidth / 2);
            Canvas.SetLeft(menu, Math.Clamp(addCentre - (menu.DesiredSize.Width / 2), Fluent.Space2, ScreenWidth - menu.DesiredSize.Width - Fluent.Space2));
            Canvas.SetTop(menu, Fluent.Space2 + bar.DesiredSize.Height + Fluent.Space1);
        }

        await Task.Delay(500).ConfigureAwait(true);

        var path = System.IO.Path.Combine(outDir, name);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
    }

    /// <summary>One widget of a properties shot: its kind, where its card is, its header, its title, and whether it is selected.</summary>
    private sealed record PropsCard(WidgetId Kind, double X, double Y, double W, double H, HeaderPlacement Header, bool Selected, string? Label = null, string? Style = null, string? ItemColor = null, double Scale = 1.0, string ItemText = "", FillChoice? Background = null, FillChoice? Items = null, ContentAlign? Align = null);

    /// <summary>[S-3713] The panel beside real cards in edit mode, each case on the stand-in display.</summary>
    private static async Task PropsShots(Grid screen, Ticker ticker, string outDir, string theme)
    {
        PropsCard[][] cases =
        [
            // [S-6521] A widget of its own item colour: the Item opacity row shows. [S-6531] At 125 %.
            // [S-6544] Its own item text colour too: white figures on its dark flip cards in either theme.
            [new(new WidgetId("clock"), 500, 160, 280, 180, HeaderPlacement.Top, true, "Zürich", "flip", "#2A3248", 1.25, "#FFFFFF")],
            [new(new WidgetId("countdown"), 980, 160, 280, 180, HeaderPlacement.Top, true)],
            [new(ShortcutsConfig.Descriptor.Id, 16, 72, 1248, 150, HeaderPlacement.Top, true)],
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

            // [S-6571] A stopwatch has no items: the row is greyed.
            [new(new WidgetId("stopwatch"), 500, 200, 280, 180, HeaderPlacement.Top, true)],

            // [S-6584] A clock aligned right: Right lit. Two of different alignments: none lit. The
            // moon's month style: the row greyed, "Not available in this style".
            [new(new WidgetId("clock"), 500, 200, 300, 180, HeaderPlacement.Top, true, "Right", Align: ContentAlign.Right)],
            [
                new(new WidgetId("clock"), 300, 200, 280, 160, HeaderPlacement.Top, true, "Left", Align: ContentAlign.Left),
                new(new WidgetId("quote"), 620, 200, 320, 180, HeaderPlacement.Top, true, Align: ContentAlign.Right),
            ],
            [new(new WidgetId("moon"), 500, 200, 280, 220, HeaderPlacement.Top, true, Style: "phaseMonth")],
        ];
        string[] names = ["right", "left", "below", "many", "toggle", "hidden", "fills", "noitems", "align", "alignmany", "alignstyle"];

        for (var i = 0; i < cases.Length; i++)
        {
            await PropsShot(screen, ticker, cases[i], System.IO.Path.Combine(outDir, $"props-{theme}-{names[i]}.png")).ConfigureAwait(true);
        }

        // [S-6569] The Background control's panel, open beside the properties panel.
        PropsCard[] picker = [new(new WidgetId("clock"), 200, 160, 300, 200, HeaderPlacement.Top, true, Style: "flip", Background: FillChoice.Of("#1B2030", 0.5))];
        await PropsShot(screen, ticker, picker, System.IO.Path.Combine(outDir, $"props-{theme}-picker.png"), openBackground: true).ConfigureAwait(true);
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

    /// <summary>The first drop-down in <paramref name="root"/>'s tree a screen reader calls <paramref name="name"/>.</summary>
    private static DropDownButton? FindChoice(DependencyObject root, string name)
    {
        if (root is DropDownButton button && Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button) == name)
        {
            return button;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindChoice(VisualTreeHelper.GetChild(root, i), name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static async Task PropsShot(Grid screen, Ticker ticker, PropsCard[] layout, string path, bool openBackground = false)
    {
        screen.Children.Clear();

        // The open control's panel hangs below the display's lower edge: the picture is taller.
        screen.Height = openBackground ? OpenHeight : PropsHeight;
        var flat = screen.Background;
        screen.Background = Wallpaper();

        var canvas = new Canvas { Clip = new RectangleGeometry { Rect = new Rect(0, 0, ScreenWidth, screen.Height) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        var bar = Bar(snapOn: true);
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
            card.ApplyContentScale(WidgetScale.Effective(1, 1, model.Scale));
            Canvas.SetLeft(card, spec.X);
            Canvas.SetTop(card, spec.Y);
            canvas.Children.Add(card);
            cards.Add(card);
        }

        var state = new AppState { Views = [view] };
        var selection = new WidgetSelection();
        selection.Replace(view.Id, selected);

        var strip = new PropertiesStrip(new PropertiesCommands(_ => { }, _ => { }, (_, _) => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { },
            id => view.Find(id) is { } found && Config.ConfigPages.HasWidgetSettings(found)));
        UiLanguage.ApplyFlow(strip);
        strip.ShowSelection(QuickSet.For(state, selection, view.Id));
        strip.Visibility = Visibility.Visible;
        canvas.Children.Add(strip);
        strip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var target = StripPlacement.Bounds(widgets.Where(w => w.Spec.Selected).Select(w => new PixelRect(w.Spec.X, w.Spec.Y, w.Spec.W, w.Spec.H)));
        if (target is { } box)
        {
            var spot = StripPlacement.Place(box, strip.DesiredSize.Width, strip.DesiredSize.Height, new PixelRect(0, 0, ScreenWidth, PropsHeight), [barRect], UiLanguage.Flow == FlowDirection.RightToLeft);
            Canvas.SetLeft(strip, Math.Round(spot.X));
            Canvas.SetTop(strip, Math.Round(spot.Y));

            // The flyout of the Background control, as it hangs under its button (a flyout on an
            // off-screen, never-activated window does not render, so its panel is drawn in place).
            if (openBackground)
            {
                strip.UpdateLayout();
                if (FindChoice(strip, Str.Settings_Style_BgLabel.Text()) is { } choice && ColorChoice.DebugPanel(choice) is { } panel)
                {
                    var flyout = new Border { Child = panel, Padding = new Thickness(Fluent.Space4) };
                    var hanging = new Grid { Children = { flyout } };
                    FlyoutSurface.Apply(hanging, flyout);
                    canvas.Children.Add(hanging);
                    var at = choice.TransformToVisual(canvas).TransformPoint(new Point(0, 0));
                    hanging.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(hanging, Math.Round(at.X + choice.ActualWidth - hanging.DesiredSize.Width));
                    Canvas.SetTop(hanging, Math.Round(at.Y + choice.ActualHeight + Fluent.Space1));
                }
                else
                {
                    Log.Warn(Scope, "the Background control was not found; the picker shot shows the panel alone");
                }
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
