#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: draw the desktop view bar ([S-906]) on a stand-in display, at a set of anchors,
/// sizes and modes, one PNG each, off-screen. <c>Themia.exe --viewbar-png &lt;outDir&gt;</c>.
/// <para>
/// The bar is the real <see cref="ViewBar"/> and is placed by the real rule
/// (<see cref="ViewBarRules.Place"/>) inside a work area that leaves a taskbar strip at the bottom,
/// so the pictures show both how a bar looks and where it lands. The edit-mode shots carry a
/// stand-in for the edit toolbar, to show a top bar moving out from under it.
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
        new("home", "Home office", "", Shown: false, IsDefault: true),
        new("games", "Gaming", "", Shown: false, IsDefault: false),
        new("focus", "Deep focus", null, Shown: false, IsDefault: false),
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
            Log.Error(Scope, "usage: --viewbar-png <outDir>");
            app.Exit();
            return true;
        }

        var outDir = System.IO.Path.GetFullPath(args[at + 1]);

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;

            try
            {
                System.IO.Directory.CreateDirectory(outDir);

                var screen = new Grid
                {
                    Width = ScreenWidth,
                    Height = ScreenHeight,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = DesignApplier.Brush("SolidBackgroundFillColorTertiaryBrush", Colors.DimGray),
                };

                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia view bar render" };
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((ScreenWidth + 40) * scale), (int)((ScreenHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                foreach (var c in Cases)
                {
                    Draw(screen, c);
                    await Task.Delay(400).ConfigureAwait(true);

                    var path = System.IO.Path.Combine(outDir, $"viewbar-{c.Name}.png");
                    await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
                    Console.WriteLine($"viewbar: wrote {path}");
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

    /// <summary>One shot: the work area, the taskbar strip, the toolbar stand-in in edit mode, and the bar at its anchor.</summary>
    private static void Draw(Grid screen, Case c)
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
            var stand = new PixelRect((ScreenWidth - 520) / 2, 24, 520, 50);
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
        bar.Update(Items, new ViewBarLook(c.Size, c.IconOnly, ViewBarRules.IsVertical(c.Anchor), c.Editing, c.Editing && c.Hidden), new DesignState());
        canvas.Children.Add(bar);

        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (x, y) = ViewBarRules.Place(area, bar.DesiredSize.Width, bar.DesiredSize.Height, c.Anchor, toolbar);
        Canvas.SetLeft(bar, x);
        Canvas.SetTop(bar, y);
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
    }
}
#endif
