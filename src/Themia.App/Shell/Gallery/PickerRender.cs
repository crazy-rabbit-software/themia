#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: draw the open Add-widget picker to a PNG, off-screen, like the other debug
/// renders. <c>Themia.exe --picker-png &lt;out.png&gt; [--search text] [--theme light|dark]</c>.
/// <para>
/// The picker lives in a flyout on a desktop surface, which a render cannot open without the user's
/// desktop; <see cref="WidgetPicker"/> is drawn here inside a stand-in for the flyout's presenter
/// instead.
/// </para>
/// </summary>
internal static class PickerRender
{
    private const string Scope = "picker-render";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--picker-png");
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "usage: --picker-png <out.png> [--search text] [--theme light|dark]");
            app.Exit();
            return true;
        }

        var outPath = System.IO.Path.GetFullPath(args[at + 1]);
        var search = Option(args, "--search");

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;

            try
            {
                var picker = new WidgetPicker(_ => { }, () => { });

                // A stand-in for the flyout presenter: the same acrylic fill, stroke and padding.
                var presenter = new Border
                {
                    Child = picker,
                    Padding = new Thickness(Fluent.Space4),
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    BorderBrush = DesignApplier.Brush("SurfaceStrokeColorFlyoutBrush", Colors.Gray),
                    Background = DesignApplier.Brush("AcrylicBackgroundFillColorDefaultBrush", Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };

                var root = new Grid
                {
                    Padding = new Thickness(Fluent.Space4),
                    Background = DesignApplier.Brush("SolidBackgroundFillColorBaseBrush", Colors.Black),
                    Children = { presenter },
                };

                window = new Window { Content = root, Title = "Themia picker render" };
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)(1100 * scale), (int)(820 * scale)));
                window.AppWindow.Show(activateWindow: false);

                await Task.Delay(500).ConfigureAwait(true);

                if (search is not null)
                {
                    picker.SetQuery(search);
                    await Task.Delay(300).ConfigureAwait(true);
                }

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath)!);
                await RenderPng.SaveAsync(presenter, outPath).ConfigureAwait(true);
                Console.WriteLine($"picker: wrote {outPath}");
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the picker failed", ex);
            }
            finally
            {
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
