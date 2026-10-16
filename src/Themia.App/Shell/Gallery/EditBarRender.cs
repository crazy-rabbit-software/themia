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
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-3506] Debug only: draw the real edit bar ([S-3500]) at the top centre of a stand-in display,
/// off-screen, to PNGs. <c>Themia.exe --editbar-png &lt;outDir&gt; [--theme light|dark]</c> writes
/// <c>editbar-&lt;theme&gt;.png</c> (snap on), <c>editbar-&lt;theme&gt;-snapoff.png</c>,
/// <c>editbar-&lt;theme&gt;-add.png</c> (the add menu open under Add widget, as the flyout hangs) and
/// <c>editbar-&lt;theme&gt;-placing.png</c> (the placement line in the bar's place), and [S-3713]
/// the properties strip on a stand-in display with real cards in edit mode:
/// <c>props-&lt;theme&gt;-above.png</c>, <c>-below.png</c> (a widget at the top edge of the
/// display), <c>-many.png</c> (three of mixed headers), <c>-toggle.png</c> (no Header choice) and
/// <c>-hidden.png</c> (header hidden, beside the same widget with one).
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
                    Text = Str.Shell_Place_Hint.Format(("action", Str.Shell_Place_PlaceOne.Format(("name", Str.Widget_Clock_Label.Text())))),
                };
                var line = new Border { Child = hint, MinHeight = EditBar.ButtonSize + (2 * Fluent.Space1), Padding = new Thickness(Fluent.Space4, 0, Fluent.Space4, 0) };
                EditBar.Frame(line);
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
    private sealed record PropsCard(WidgetKind Kind, double X, double Y, double W, double H, HeaderPlacement Header, bool Selected, string? Label = null, int? Style = null);

    /// <summary>[S-3713] The strip against real cards in edit mode, each case on the stand-in display.</summary>
    private static async Task PropsShots(Grid screen, Ticker ticker, string outDir, string theme)
    {
        PropsCard[][] cases =
        [
            [new(WidgetKind.Clock, 500, 260, 280, 180, HeaderPlacement.Top, true, "Zürich", (int)ClockStyle.Flip)],
            [new(WidgetKind.Countdown, 980, 16, 280, 180, HeaderPlacement.Top, true)],
            [
                new(WidgetKind.Clock, 300, 200, 220, 160, HeaderPlacement.Top, true),
                new(WidgetKind.Countdown, 540, 240, 260, 180, HeaderPlacement.Bottom, true),
                new(WidgetKind.Clock, 820, 200, 200, 140, HeaderPlacement.Hidden, true),
            ],
            [new(WidgetKind.WifiToggle, 580, 260, 120, 120, HeaderPlacement.Hidden, true)],
            [
                new(WidgetKind.Countdown, 360, 260, 280, 180, HeaderPlacement.Hidden, true),
                new(WidgetKind.Countdown, 680, 260, 280, 180, HeaderPlacement.Top, false),
            ],
        ];
        string[] names = ["above", "below", "many", "toggle", "hidden"];

        for (var i = 0; i < cases.Length; i++)
        {
            await PropsShot(screen, ticker, cases[i], System.IO.Path.Combine(outDir, $"props-{theme}-{names[i]}.png")).ConfigureAwait(true);
        }
    }

    private static async Task PropsShot(Grid screen, Ticker ticker, PropsCard[] layout, string path)
    {
        screen.Children.Clear();
        screen.Height = PropsHeight;

        var canvas = new Canvas();
        screen.Children.Add(canvas);

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
            if (spec.Style is { } style)
            {
                WidgetStyles.Set(model.Config, style); // [S-6007] the strip's Style shows the current one's icon
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
            Canvas.SetLeft(card, spec.X);
            Canvas.SetTop(card, spec.Y);
            canvas.Children.Add(card);
            cards.Add(card);
        }

        var state = new AppState { Views = [view] };
        var selection = new WidgetSelection();
        selection.Replace(view.Id, selected);

        var strip = new PropertiesStrip(new PropertiesCommands(_ => { }, _ => { }, (_, _) => { }, () => { }, _ => { }, _ => { }));
        UiLanguage.ApplyFlow(strip);
        strip.ShowSelection(QuickSet.For(state, selection, view.Id));
        strip.Visibility = Visibility.Visible;
        canvas.Children.Add(strip);
        strip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var target = StripPlacement.Bounds(widgets.Where(w => w.Spec.Selected).Select(w => new PixelRect(w.Spec.X, w.Spec.Y, w.Spec.W, w.Spec.H)));
        if (target is { } box)
        {
            var spot = StripPlacement.Place(box, strip.DesiredSize.Width, strip.DesiredSize.Height, new PixelRect(0, 0, ScreenWidth, PropsHeight), [barRect]);
            Canvas.SetLeft(strip, Math.Round(spot.X));
            Canvas.SetTop(strip, Math.Round(spot.Y));
        }

        await Task.Delay(800).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");

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
            new(WidgetKind.Clock, 120, 120, 280, 180, HeaderPlacement.Top, true),
            new(WidgetKind.Countdown, 460, 140, 240, 160, HeaderPlacement.Top, false),
            new(WidgetKind.WifiToggle, 760, 160, 64, 64, HeaderPlacement.Hidden, true),
            new(WidgetKind.WifiToggle, 860, 160, 64, 64, HeaderPlacement.Hidden, false),
        ];
        PropsCard[] multi =
        [
            new(WidgetKind.Clock, 120, 120, 240, 160, HeaderPlacement.Top, true),
            new(WidgetKind.Countdown, 380, 120, 240, 160, HeaderPlacement.Top, true),
            new(WidgetKind.Clock, 640, 120, 200, 160, HeaderPlacement.Hidden, true),
            new(WidgetKind.Countdown, 880, 120, 240, 160, HeaderPlacement.Top, false),
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
