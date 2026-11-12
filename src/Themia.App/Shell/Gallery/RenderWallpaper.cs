#if DEBUG
using Microsoft.UI.Dispatching;
using Themia.App.Services;
using Themia.App.Services.Wallpaper;
using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2119] Debug renders only: a stand-in wallpaper, so a render draws its cards frosted ([S-2110]) over
/// a picture of its own instead of Windows'. One line in a renderer:
/// <c>using var wallpaper = RenderWallpaper.Use(path, new ScreenMetrics(w, h));</c> — the screen is the
/// page the cards stand on, in DIPs, its top-left the cards' origin (their <c>Canvas.Left/Top</c>). The
/// picture is placed as Windows' Fill places it, over black. While it is in use every render host
/// (<see cref="NoticeRender.RenderCardHost"/>) reports that screen and its backdrop.
/// </summary>
internal static class RenderWallpaper
{
    private static Backdrops? _backdrops;

    /// <summary>The stand-in screen while one is in use, else null.</summary>
    public static ScreenMetrics? Screen { get; private set; }

    /// <summary>The stand-in screen's backdrop while one is in use, else null.</summary>
    public static Backdrop? Backdrop => Screen is { } screen ? _backdrops?.For(Display(screen)) : null;

    /// <summary>
    /// Draw the cards of a render over <paramref name="path"/> (a picture file's full path) on a screen
    /// of <paramref name="screen"/>, until the result is disposed. Call on the UI thread.
    /// </summary>
    public static IDisposable Use(string path, ScreenMetrics screen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("RenderWallpaper.Use runs on the UI thread");

        var previous = AppServices.Backdrops;
        _backdrops = new Backdrops(new FileWallpaper(Path.GetFullPath(path)), dispatcher);
        AppServices.Backdrops = _backdrops;
        Screen = screen;

        // Start the picture now, so it is ready by the time the cards are drawn.
        _ = _backdrops.For(Display(screen));
        return new Restore(previous);
    }

    /// <summary>The backdrop's picture, done: await before the shot (no guessing at a delay).</summary>
    public static Task WhenLoaded() => _backdrops?.WhenLoaded() ?? Task.CompletedTask;

    /// <summary>The stand-in display: the screen's DIPs taken as its pixels, at the origin.</summary>
    private static PixelRect Display(ScreenMetrics screen) => new(0, 0, Math.Round(screen.Width), Math.Round(screen.Height));

    private sealed class Restore(Backdrops? previous) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done)
            {
                return;
            }

            _done = true;
            _backdrops?.Dispose();
            _backdrops = null;
            Screen = null;
            AppServices.Backdrops = previous;
        }
    }

    /// <summary>The stand-in source: one file, Fill, over black, for any display; it never changes.</summary>
    private sealed class FileWallpaper(string path) : IWallpaperSource
    {
        public event Action? Changed
        {
            add { }
            remove { }
        }

        public Task<WallpaperPicture?> PictureAsync(PixelRect display, CancellationToken cancel)
            => WallpaperFile.PlaceAsync(path, WallpaperFit.Fill, new RgbColor(0, 0, 0), display, display, cancel);
    }
}
#endif
