#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-3506] Debug only: draw the real edit bar ([S-3500]) at the top centre of a stand-in display,
/// off-screen, to PNGs. <c>Themia.exe --editbar-png &lt;outDir&gt; [--theme light|dark]</c> writes
/// <c>editbar-&lt;theme&gt;.png</c> (snap on), <c>editbar-&lt;theme&gt;-snapoff.png</c>,
/// <c>editbar-&lt;theme&gt;-add.png</c> (the add menu open under Add widget, as the flyout hangs) and
/// <c>editbar-&lt;theme&gt;-placing.png</c> (the placement line in the bar's place), and [S-3706]
/// <c>editbar-&lt;theme&gt;-selected.png</c> / <c>editbar-&lt;theme&gt;-selected-many.png</c> (the
/// selection group for one clock, and for three widgets of mixed headers).
/// </summary>
internal static class EditBarRender
{
    private const string Scope = "editbar-render";
    private const string Flag = "--editbar-png";

    private const double ScreenWidth = 1280;
    private const double ScreenHeight = 140;
    private const double OpenHeight = 900;

    /// <summary>
    /// The <c>--theme</c> of this render or of <c>--picker-png</c> / <c>--viewbar-png</c>, read before the app's resources
    /// load: the bar's brushes come from the application theme.
    /// </summary>
    public static ApplicationTheme? RequestedTheme(string[] args)
    {
        if (Array.IndexOf(args, Flag) < 0 && Array.IndexOf(args, "--picker-png") < 0 && Array.IndexOf(args, "--viewbar-png") < 0)
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

                var menu = PickerRender.Presenter(new WidgetPicker(_ => { }, () => { }, PickerRender.WorkWidth, PickerRender.WorkHeight));
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

                var one = Bar(snapOn: true);
                one.ShowSelection(new QuickSetOffer(1, HeaderPlacement.Hidden, WidgetSurface.Card, "render", WidgetKind.Clock, null));
                await Shot(screen, one, null, outDir, $"editbar-{theme}-selected.png").ConfigureAwait(true);

                var many = Bar(snapOn: true);
                many.ShowSelection(new QuickSetOffer(3, null, WidgetSurface.Translucent, null, null, null));
                await Shot(screen, many, null, outDir, $"editbar-{theme}-selected-many.png").ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the edit bar failed", ex);
            }
            finally
            {
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
        Done: () => { },
        SetHeader: _ => { },
        SetSurface: _ => { },
        SetTitle: (_, _) => { },
        ReleaseFocus: () => { })));

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

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
