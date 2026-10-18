using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3965] The icon of a file, a drive or an app (a process's executable, a drive root, a
/// <c>shell:</c> name), as a widget's row draws it: the host's shell-icon cache (the system image
/// list, a packaged app's logo), installed by the host once at start. UI thread only.
/// </summary>
public static class AppIconSource
{
    private static Func<string, int, LogoTheme, string?, Task<ImageSource?>>? _get;
    private static int _defaultSize;

    /// <summary>
    /// The icon for <paramref name="path"/> at <paramref name="size"/> pixels, or null when it has none.
    /// <paramref name="theme"/> picks a packaged app's logo; <paramref name="variant"/> keys a second
    /// picture of the same path (a drive's own state).
    /// </summary>
    public static Task<ImageSource?> GetAsync(string path, int size, LogoTheme theme, string? variant = null)
        => (Volatile.Read(ref _get) ?? throw new InvalidOperationException("no icon source installed: the host installs it before any widget is drawn"))(path, size, theme, variant);

    /// <summary>The icon for <paramref name="path"/> at the host's default size.</summary>
    public static Task<ImageSource?> GetAsync(string path, LogoTheme theme) => GetAsync(path, Volatile.Read(ref _defaultSize), theme);

    /// <summary>[S-3965] The theme a packaged app's logo is chosen for: the element's own, as it is drawn now.</summary>
    public static LogoTheme ThemeOf(FrameworkElement element)
        => element.ActualTheme == ElementTheme.Light ? LogoTheme.Light : LogoTheme.Dark;

    /// <summary>The host's icon cache and its default size. Once per process.</summary>
    internal static void Install(Func<string, int, LogoTheme, string?, Task<ImageSource?>> get, int defaultSize)
    {
        ArgumentNullException.ThrowIfNull(get);
        if (Interlocked.CompareExchange(ref _get, get, null) is not null)
        {
            throw new InvalidOperationException("the icon source is installed once per process");
        }

        Volatile.Write(ref _defaultSize, defaultSize);
    }
}
