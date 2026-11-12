using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;

namespace Themia.App.Shell;

/// <summary>
/// [S-2112] [S-2118] One display's blurred wallpaper, kept once and shared by every card on it. It is
/// built when first asked for and again only when the source says Windows changed (off the UI thread:
/// read, decode, place, blur), so it costs nothing while nothing changes; a card shows its part of it
/// by an offset ([S-2114]), never by blurring again.
/// </summary>
internal sealed class Backdrop
{
    private readonly Backdrops _owner;
    private CancellationTokenSource? _loading;
    private int _cards;

    internal Backdrop(Backdrops owner, PixelRect display)
    {
        _owner = owner;
        Display = display;
    }

    /// <summary>The display's bounds in virtual screen pixels: what the source is asked about.</summary>
    public PixelRect Display { get; }

    /// <summary>The blurred picture, or null for none ([S-2111]: the cards fall back to a firmer fill).</summary>
    public ImageSource? Picture { get; private set; }

    /// <summary>The load in flight, or done: a render waits on it before its shot.</summary>
    public Task Loaded { get; private set; } = Task.CompletedTask;

    private event Action? ChangedCore;

    /// <summary>The picture changed (on the UI thread). Counted, so a backdrop no card watches is dropped.</summary>
    public event Action? Changed
    {
        add
        {
            ChangedCore += value;
            _cards++;
        }

        remove
        {
            ChangedCore -= value;
            _cards = Math.Max(0, _cards - 1);
        }
    }

    internal bool Watched => _cards > 0;

    internal void Load()
    {
        _loading?.Cancel();
        _loading?.Dispose();
        var cancel = new CancellationTokenSource();
        _loading = cancel;
        Loaded = LoadAsync(cancel.Token);
    }

    internal void Stop()
    {
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = null;
    }

    private async Task LoadAsync(CancellationToken cancel)
    {
        try
        {
            var picture = await Task.Run(
                async () =>
                {
                    var placed = await _owner.Source.PictureAsync(Display, cancel).ConfigureAwait(false);
                    if (placed is not null)
                    {
                        BoxBlur.Apply(placed.Pixels, placed.Width, placed.Height, BoxBlur.BackdropRadius, BoxBlur.BackdropPasses);
                    }

                    return placed;
                },
                cancel).ConfigureAwait(true);

            if (cancel.IsCancellationRequested)
            {
                return; // ignore: a newer load replaced this one.
            }

            Picture = picture is null ? null : ToBitmap(picture);
            ChangedCore?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // ignore: a newer load replaced this one.
        }
        catch (Exception ex)
        {
            Log.Error(Backdrops.Scope, "the blurred wallpaper could not be built; the cards draw without it", ex);
            Picture = null;
            ChangedCore?.Invoke();
        }
    }

    private static WriteableBitmap ToBitmap(WallpaperPicture picture)
    {
        var bitmap = new WriteableBitmap(picture.Width, picture.Height);
        picture.Pixels.CopyTo(bitmap.PixelBuffer);
        bitmap.Invalidate();
        return bitmap;
    }
}

/// <summary>
/// [S-2118] Every display's <see cref="Backdrop"/>, keyed by its bounds, over one <see cref="IWallpaperSource"/>.
/// A change the source raises rebuilds those cards watch and drops the rest (a display gone, or moved).
/// UI thread only; the source's change is brought onto it.
/// </summary>
internal sealed class Backdrops : IDisposable
{
    internal const string Scope = "backdrop";

    private readonly Dictionary<PixelRect, Backdrop> _byDisplay = [];
    private readonly DispatcherQueue _dispatcher;

    public Backdrops(IWallpaperSource source, DispatcherQueue dispatcher)
    {
        Source = source;
        _dispatcher = dispatcher;
        Source.Changed += OnSourceChanged;
    }

    internal IWallpaperSource Source { get; }

    /// <summary>The display's backdrop, built on first ask.</summary>
    public Backdrop For(PixelRect display)
    {
        if (!_byDisplay.TryGetValue(display, out var backdrop))
        {
            backdrop = new Backdrop(this, display);
            _byDisplay[display] = backdrop;
            backdrop.Load();
        }

        return backdrop;
    }

    /// <summary>Every load in flight, done: for a render's shot.</summary>
    public Task WhenLoaded() => Task.WhenAll(_byDisplay.Values.Select(b => b.Loaded));

    public void Dispose()
    {
        Source.Changed -= OnSourceChanged;
        foreach (var backdrop in _byDisplay.Values)
        {
            backdrop.Stop();
        }

        _byDisplay.Clear();
    }

    private void OnSourceChanged()
    {
        if (!_dispatcher.TryEnqueue(Reload))
        {
            Log.Warn(Scope, "a wallpaper change could not be queued; the backdrops follow on the next change");
        }
    }

    private void Reload()
    {
        foreach (var (display, backdrop) in _byDisplay.ToList())
        {
            if (backdrop.Watched)
            {
                backdrop.Load();
            }
            else
            {
                backdrop.Stop();
                _byDisplay.Remove(display);
            }
        }
    }
}
