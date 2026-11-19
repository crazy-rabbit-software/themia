#if DEBUG
using Microsoft.UI.Xaml;
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

    /// <summary>The window <paramref name="make"/> builds, at 100 % where <paramref name="args"/> asks for it.</summary>
    public static Window Window(string[] args, Func<Window> make)
    {
        var at = Array.IndexOf(args, "--raster");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] != "100")
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

    /// <summary>
    /// Size <paramref name="window"/> to hold <paramref name="width"/> × <paramref name="height"/> DIPs: in the screen's
    /// pixels, which a 100 % window's own DPI (96) does not say, so the larger of its DPI and the system's is taken.
    /// </summary>
    public static void Size(Window window, double width, double height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var dpi = Math.Max(Win32.GetDpiForWindow(hwnd), Win32.GetDpiForSystem());
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        window.AppWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
    }
}
#endif
