#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-11712] Debug only: draw the add page open under the real edit bar to a PNG, off-screen, like the
/// other debug renders. <c>Themia.exe --picker-png &lt;out.png&gt; [--tab Id] [--search text] [--width w]
/// [--height h] [--theme light|dark] [--lang xx]</c>: <c>--tab</c> chooses a tab by its <see cref="AddFolderId"/>
/// (Start, Time, … QuickSettings), <c>--search</c> types into the search box, <c>--width</c> and
/// <c>--height</c> size the stand-in display (1920 × 1032 DIPs: a 1080p display at 100 % with its taskbar).
/// Every picture the page asks for is drawn before the shot. One path with <c>--editbar-png</c>'s add shots.
/// </summary>
internal static class PickerRender
{
    private const string Scope = "picker-render";
    private const string Usage = "usage: --picker-png <out.png> [--tab Id] [--search text] [--width w] [--height h] [--theme light|dark]";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--picker-png");
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, Usage);
            Console.WriteLine(Usage);
            app.Exit();
            return true;
        }

        var outPath = System.IO.Path.GetFullPath(args[at + 1]);
        var search = Option(args, "--search");
        AddFolderId? tab = null;
        if (Option(args, "--tab") is { } name)
        {
            if (Enum.TryParse<AddFolderId>(name, ignoreCase: true, out var id) && Enum.IsDefined(id))
            {
                tab = id;
            }
            else
            {
                Console.WriteLine($"picker: no tab '{name}' (has: {string.Join(", ", Enum.GetNames<AddFolderId>())})");
            }
        }

        // What went wrong while drawing (a picture that could not be drawn) is said with the picture.
        Log.AddSink(static record =>
        {
            if (record.Level >= LogLevel.Warn || record.Scope == "add-pictures")
            {
                Console.WriteLine($"picker: {record.Level} {record.Scope}: {record.Message} {record.Detail}");
            }
        });

        var width = Number(args, "--width") ?? 1920;
        var height = Number(args, "--height") ?? 1032;

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;

            try
            {
                EditBarRender.ApplyDesignTheme(app);
                var screen = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = DesignApplier.Brush("SolidBackgroundFillColorTertiaryBrush", Colors.DimGray),
                };

                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia add page render" };
                UiLanguage.Apply(screen);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((width + 40) * scale), (int)((height + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath)!);
                await EditBarRender.AddPageShot(screen, outPath, width, height, tab, search).ConfigureAwait(true);
                Console.WriteLine($"picker: wrote {outPath}");
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the add page failed", ex);
                Console.WriteLine($"picker: failed: {ex.Message}");
            }
            finally
            {
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    private static double? Number(string[] args, string name)
        => Option(args, name) is { } text && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) && value >= 320
            ? value
            : null;

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
