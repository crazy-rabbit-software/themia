#if DEBUG
using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2377] <c>--raster 100</c>: a render's window drawn at 100 % whatever the display's scale (its thread makes it
/// DPI-unaware while it is built), so a picture is checked at 100 % and at the display's own (150 % here); without it
/// the window takes its display's scale as before.
/// </summary>
internal static class RenderRaster
{
    private const string Scope = "render-raster";

    /// <summary>What the frame takes beside and above the content, in DIPs (as <see cref="OffscreenWindow"/>'s).</summary>
    private const double MarginWidth = 40;

    private const double MarginHeight = 80;

    /// <summary>
    /// The render's harness (<see cref="RenderHarness.Open"/>, its content set later), its window for
    /// <paramref name="width"/> × <paramref name="height"/> DIPs, at 100 % where the run asks for <c>--raster 100</c>.
    /// </summary>
    public static RenderHarness Open(RenderRun run, string title, double width, double height)
    {
        var harness = Made(run, () => RenderHarness.Open(run, title, null, width, height));

        // In the screen's pixels, which a 100 % window's own DPI (96) does not say: the larger of its DPI and the system's.
        var dpi = Math.Max(Win32.GetDpiForWindow(harness.Window.Hwnd), Win32.GetDpiForSystem());
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        harness.Window.Window.AppWindow.Resize(new SizeInt32((int)((width + MarginWidth) * scale), (int)((height + MarginHeight) * scale)));
        return harness;
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
