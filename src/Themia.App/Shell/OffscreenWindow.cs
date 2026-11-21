using Microsoft.UI.Xaml;
using Themia.App.Interop;
using Windows.Graphics;

namespace Themia.App.Shell;

/// <summary>
/// [S-69] The window a render or check draws in: off-screen, shown without activation, never on the user's desktop,
/// sized to its content's DIPs (plus the frame's margin) at the window's own scale. Closed on dispose.
/// </summary>
internal sealed class OffscreenWindow : IDisposable
{
    /// <summary>What the frame takes beside and above the content, in DIPs, so the content is drawn whole.</summary>
    private const double MarginWidth = 40;

    private const double MarginHeight = 80;

    private OffscreenWindow(Window window)
    {
        Window = window;
        Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var dpi = Win32.GetDpiForWindow(Hwnd);
        Scale = dpi > 0 ? dpi / 96.0 : 1.0;
    }

    /// <summary>The window.</summary>
    public Window Window { get; }

    /// <summary>Its handle.</summary>
    public nint Hwnd { get; }

    /// <summary>Its raster scale (its display's DPI over 96).</summary>
    public double Scale { get; }

    /// <summary>A window titled <paramref name="title"/> holding <paramref name="content"/> (none: set it later), sized for <paramref name="width"/> × <paramref name="height"/> DIPs and shown off-screen.</summary>
    public static OffscreenWindow Open(string title, UIElement? content, double width, double height)
    {
        var window = new OffscreenWindow(new Window { Content = content, Title = title });
        window.Window.AppWindow.Move(new PointInt32(-32000, -32000));
        window.Resize(width, height);
        window.Window.AppWindow.Show(activateWindow: false);
        return window;
    }

    /// <summary>Sized for <paramref name="width"/> × <paramref name="height"/> DIPs of content.</summary>
    public void Resize(double width, double height)
        => Window.AppWindow.Resize(new SizeInt32((int)((width + MarginWidth) * Scale), (int)((height + MarginHeight) * Scale)));

    public void Dispose() => Window.Close();
}
