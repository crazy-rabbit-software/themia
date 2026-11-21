using Microsoft.UI.Xaml;
using Themia.App.Interop;
using Windows.Graphics;

namespace Themia.App.Shell;

/// <summary>
/// [S-69] The window a render or check draws in: off-screen, shown without activation, never on the user's desktop,
/// sized to its content's DIPs (plus the frame's margin) at the window's own scale. Made first, holding its content,
/// and shown once that content is in place (<see cref="Show(double, double, double?)"/>), as every render always did.
/// Closed on dispose.
/// </summary>
internal sealed class OffscreenWindow : IDisposable
{
    /// <summary>What the frame takes beside and above the content, in DIPs, so the content is drawn whole.</summary>
    public const double MarginWidth = 40;

    public const double MarginHeight = 80;

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

    /// <summary>A window titled <paramref name="title"/> holding <paramref name="content"/> (none: set it later), not shown yet.</summary>
    public static OffscreenWindow Create(string title, UIElement? content) => new(new Window { Content = content, Title = title });

    /// <summary>A window titled <paramref name="title"/> holding <paramref name="content"/>, shown for <paramref name="width"/> × <paramref name="height"/> DIPs.</summary>
    public static OffscreenWindow Open(string title, UIElement? content, double width, double height)
    {
        var window = Create(title, content);
        window.Show(width, height);
        return window;
    }

    /// <summary>
    /// Moved off-screen, sized for <paramref name="width"/> × <paramref name="height"/> DIPs of content at
    /// <paramref name="scale"/> (default its own), and shown without activation.
    /// </summary>
    public void Show(double width, double height, double? scale = null)
        => Show(Pixels(width, height, scale ?? Scale));

    /// <summary>Moved off-screen, sized to <paramref name="pixels"/>, and shown without activation.</summary>
    public void Show(SizeInt32 pixels)
    {
        Window.AppWindow.Move(new PointInt32(-32000, -32000));
        Window.AppWindow.Resize(pixels);
        Window.AppWindow.Show(activateWindow: false);
    }

    /// <summary>Sized for <paramref name="width"/> × <paramref name="height"/> DIPs of content.</summary>
    public void Resize(double width, double height) => Window.AppWindow.Resize(Pixels(width, height, Scale));

    public void Dispose() => Window.Close();

    private static SizeInt32 Pixels(double width, double height, double scale)
        => new((int)((width + MarginWidth) * scale), (int)((height + MarginHeight) * scale));
}
