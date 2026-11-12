using Themia.App.Interop;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;

namespace Themia.App.Services.Wallpaper;

/// <summary>
/// [S-2116] [S-2117] The host's <see cref="IWallpaperSource"/>: what Windows shows on each display, read
/// through <c>IDesktopWallpaper</c> off the UI thread, placed from its file. Changes are heard, never
/// polled: the command window's <c>WM_SETTINGCHANGE</c> (a new wallpaper, a slideshow step),
/// <c>WM_SYSCOLORCHANGE</c> (the colour) and its display changes.
/// </summary>
internal sealed class WindowsWallpaper : IWallpaperSource, IDisposable
{
    private const string Scope = "wallpaper";

    private readonly CommandWindow _command;

    public WindowsWallpaper(CommandWindow command)
    {
        _command = command;
        _command.WallpaperChanged += OnChanged;
        _command.DisplaysChanged += OnChanged;
    }

    public event Action? Changed;

    public async Task<WallpaperPicture?> PictureAsync(PixelRect display, CancellationToken cancel)
    {
        try
        {
            var setting = await Task.Run(() => DesktopWallpaper.Read(display), cancel).ConfigureAwait(false);
            return setting is { } s
                ? await WallpaperFile.PlaceAsync(s.Path, s.Fit, s.Background, display, s.Desktop, cancel).ConfigureAwait(false)
                : null;
        }
        catch (OperationCanceledException)
        {
            return null; // ignore: a newer change asked again.
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "reading Windows' wallpaper failed; the cards draw without it", ex);
            return null;
        }
    }

    public void Dispose()
    {
        _command.WallpaperChanged -= OnChanged;
        _command.DisplaysChanged -= OnChanged;
    }

    private void OnChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            // Raised from the command window's procedure: nothing may escape back into it.
            Log.Error(Scope, "a wallpaper change could not be handled", ex);
        }
    }
}
