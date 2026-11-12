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

    /// <summary>Icons asked for and not yet answered, and the wait for none (UI thread only).</summary>
    private static int _pending;

    private static TaskCompletionSource? _quiet;

    /// <summary>
    /// The icon for <paramref name="path"/> at <paramref name="size"/> pixels, or null when it has none.
    /// <paramref name="theme"/> picks a packaged app's logo; <paramref name="variant"/> keys a second
    /// picture of the same path (a drive's own state).
    /// </summary>
    public static Task<ImageSource?> GetAsync(string path, int size, LogoTheme theme, string? variant = null)
    {
        var get = Volatile.Read(ref _get) ?? throw new InvalidOperationException("no icon source installed: the host installs it before any widget is drawn");
        var task = get(path, size, theme, variant);
        if (!task.IsCompleted)
        {
            _pending++;
            _ = Track(task);
        }

        return task;
    }

    /// <summary>
    /// [S-11852] Done once no icon asked for is still being fetched: the add page's picture of a list of
    /// programs waits for its rows' icons (with a bound of its own) before it is drawn.
    /// </summary>
    public static Task Quiet => _pending == 0 ? Task.CompletedTask : (_quiet ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    private static async Task Track(Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (Exception)
        {
            // ignore: the caller awaits the same task and handles its failure; this only counts it done
        }
        finally
        {
            if (--_pending == 0 && _quiet is { } quiet)
            {
                _quiet = null;
                quiet.TrySetResult();
            }
        }
    }

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
