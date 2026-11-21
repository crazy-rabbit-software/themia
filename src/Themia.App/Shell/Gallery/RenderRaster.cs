#if DEBUG
using Microsoft.UI.Xaml;
using Themia.App.Interop;
using Themia.Core.Diagnostics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2377] <c>--raster 100</c>: a render's window drawn at 100 % whatever the display's scale (its thread makes it
/// DPI-unaware while it is built), so a picture is checked at 100 % and at the display's own (150 % here); without it
/// the window takes its display's scale as before.
/// </summary>
internal static class RenderRaster
{
    private const string Scope = "render-raster";

    /// <summary>
    /// The render's window (<see cref="RenderHarness.MakeWindow"/>), made at 100 % where the run asks for <c>--raster 100</c>.
    /// </summary>
    public static OffscreenWindow MakeWindow(RenderHarness harness, string title, UIElement? content)
        => Made(harness.Run, () => harness.MakeWindow(title, content));

    /// <summary>
    /// <paramref name="window"/> shown for <paramref name="width"/> × <paramref name="height"/> DIPs in the screen's pixels,
    /// which a 100 % window's own DPI (96) does not say: the larger of its DPI and the system's.
    /// </summary>
    public static void Show(OffscreenWindow window, double width, double height)
    {
        var dpi = Math.Max(Win32.GetDpiForWindow(window.Hwnd), Win32.GetDpiForSystem());
        window.Show(width, height, dpi > 0 ? dpi / 96.0 : 1.0);
    }

    /// <summary>What <paramref name="make"/> builds (a window and what it holds), built DPI-unaware where the run asks for <c>--raster 100</c>.</summary>
    public static T Made<T>(RenderRun run, Func<T> make)
    {
        if (run.Option("--raster") != "100")
        {
            return make();
        }

        var was = Win32.SetThreadDpiAwarenessContext(Win32.DpiAwarenessUnaware);
        if (was == 0)
        {
            Log.Warn(Scope, "the render's thread could not be made DPI-unaware; it draws at the display's scale");
            return make();
        }

        try
        {
            return make();
        }
        finally
        {
            // ignore: the old context back; what it returns is the unaware one just set.
            _ = Win32.SetThreadDpiAwarenessContext(was);
        }
    }
}
#endif
