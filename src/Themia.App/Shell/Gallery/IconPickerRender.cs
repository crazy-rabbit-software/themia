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
/// Debug only: draw the open icon picker ([S-1803]) to a PNG, off-screen.
/// <c>Themia.exe --iconpicker-png &lt;out.png&gt; [--search text] [--theme light|dark] [--initials]</c>.
/// <para>
/// The picker lives in a flyout, which a render cannot open without a window on the desktop; it is
/// drawn here inside a stand-in for the flyout's presenter, with the Home glyph as the current value.
/// <c>--initials</c> shows the default as a view's initials, as the view page does.
/// </para>
/// </summary>
internal static class IconPickerRender
{
    private const string Flag = "--iconpicker-png";
    private const string Scope = "iconpicker-render";

    /// <summary>The <c>--theme</c> of this render, read before the app's resources load.</summary>
    public static ApplicationTheme? RequestedTheme(string[] args)
    {
        if (Array.IndexOf(args, Flag) < 0)
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

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, $"usage: {Flag} <out.png> [--search text] [--theme light|dark] [--initials]");
            app.Exit();
            return true;
        }

        var outPath = System.IO.Path.GetFullPath(args[at + 1]);
        var search = Option(args, "--search");
        var initials = args.Contains("--initials");

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;

            try
            {
                var look = new IconLook(Glyphs.View, initials ? "WK" : null);
                var picker = new IconPicker("", look, null, _ => { });

                // A stand-in for the flyout presenter: the same acrylic fill, stroke and padding.
                var presenter = new Border
                {
                    Child = picker,
                    Padding = new Thickness(Fluent.Space4),
                    CornerRadius = new CornerRadius(Fluent.OverlayRadius),
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

                window = new Window { Content = root, Title = "Themia icon picker render" };
                UiLanguage.Apply(root);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)(600 * scale), (int)(600 * scale)));
                window.AppWindow.Show(activateWindow: false);

                // A render, not app code: the box takes text once it is loaded, and the grid
                // realises its rows on the next layout passes.
                await Task.Delay(500).ConfigureAwait(true);
                if (search is not null)
                {
                    picker.SetQuery(search);
                    await Task.Delay(500).ConfigureAwait(true);
                }

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath)!);
                await RenderPng.SaveAsync(presenter, outPath).ConfigureAwait(true);
                Console.WriteLine($"iconpicker: wrote {outPath}");
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the icon picker failed", ex);
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
