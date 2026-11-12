using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Storage.Streams;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11311] One now-playing widget's connection to Windows' media session (the host's
/// <see cref="IMediaSessions"/>): the three now-playing atoms (the track and its transport, the cover,
/// the progress) each hold one and so read the same session, the same snapshot and the same position,
/// and agree to the second. Event-driven: the session's change notification is marshalled to the view's
/// dispatcher, read once (a newer read supersedes an older one by its generation) and handed to
/// <c>changed</c>; the position between pushes is arithmetic against the session's anchor, never a
/// question asked of it. A preview presses, seeks and fetches nothing of the machine.
/// </summary>
public sealed class NowPlayingFeed : IDisposable
{
    private const string Scope = "music";

    private readonly IMediaSessions _media;
    private readonly Action _changed;
    private IMediaSession? _session;
    private DispatcherQueue? _queue;
    private CancellationTokenSource? _reader;
    private int _generation;
    private bool _detached = true;

    /// <param name="media">The host's media sessions.</param>
    /// <param name="changed">Raised on the view's thread with a new snapshot in <see cref="Now"/>.</param>
    public NowPlayingFeed(IMediaSessions media, Action changed)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(changed);
        _media = media;
        _changed = changed;
    }

    /// <summary>What is playing, as last read.</summary>
    public NowPlaying Now { get; private set; } = NowPlaying.Of(MusicState.Loading);

    /// <summary>Set from the surface: a preview card presses, seeks and fetches nothing of the machine.</summary>
    public bool Preview { get; set; }

    /// <summary>Debug renders: a sample stands still; the session is not read over it.</summary>
    public bool Frozen { get; private set; }

    /// <summary>Which read is current: a snapshot of an older one belongs to a session the widget moved on from.</summary>
    public int Generation => _generation;

    /// <summary>Open the session (once) and start listening on <paramref name="queue"/>'s thread.</summary>
    public void Attach(DispatcherQueue queue)
    {
        _queue = queue;
        _detached = false;
        _session ??= _media.Open(OnSessionChanged);
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        if (_session is not { } session)
        {
            return;
        }

        try
        {
            await session.StartAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not start listening to the media session", ex);
        }
    }

    /// <summary>The session said something changed. Raised on a WinRT thread, so it hops to the dispatcher first.</summary>
    private void OnSessionChanged()
    {
        if (_detached)
        {
            return;
        }

        if (_queue is not { } queue || !queue.TryEnqueue(Reload))
        {
            Log.Debug(Scope, "a session change arrived with no dispatcher to draw it on");
        }
    }

    /// <summary>Read the session again; a newer read supersedes this one.</summary>
    public void Reload()
    {
        if (_detached || Frozen || _session is null)
        {
            return;
        }

        _reader?.Cancel();
        var cts = new CancellationTokenSource();
        _reader = cts;
        _ = ReadAsync(++_generation, cts);
    }

    private async Task ReadAsync(int generation, CancellationTokenSource cts)
    {
        try
        {
            if (_session is not { } session)
            {
                return;
            }

            var now = await session.ReadAsync(cts.Token).ConfigureAwait(true);
            if (_detached || generation != _generation)
            {
                return;
            }

            Now = now;
            _changed();
        }
        catch (OperationCanceledException)
        {
            // ignore: superseded by a newer read, which will draw in this one's place.
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not read the media session", ex);
        }
        finally
        {
            if (ReferenceEquals(_reader, cts))
            {
                _reader = null;
            }

            cts.Dispose();
        }
    }

    /// <summary>
    /// [S-2056] The renders' track: paused a third of the way into "Northern Lights" by the Aurora
    /// Quartet, at <paramref name="at"/>, so the three atoms are looked at on the same moment.
    /// </summary>
    public static NowPlaying SampleTrack(DateTimeOffset at) => new(
        MusicState.Paused,
        "Northern Lights",
        "Aurora Quartet · Long Winter",
        "preview-track",
        new TrackTimeline(TimeSpan.FromSeconds(74), TimeSpan.FromSeconds(222)),
        at,
        CanPlayPause: true,
        CanNext: true,
        CanPrevious: true,
        CanSeek: true,
        Artist: "Aurora Quartet",
        Album: "Long Winter");

    /// <summary>Debug renders: show <paramref name="sample"/> and stop reading the session over it.</summary>
    public void Show(NowPlaying sample)
    {
        Frozen = true;
        _reader?.Cancel();
        ++_generation;
        Now = sample;
        _changed();
    }

    /// <summary>The position now, from the session's anchor (display only: the next snapshot overwrites it).</summary>
    public TimeSpan Position() => MusicRules.Interpolate(Now.Timeline, Now.AnchoredAt, DateTimeOffset.UtcNow, Now.State == MusicState.Playing);

    /// <summary>
    /// Ask the session to do something, and let its own change notification draw the result. Not
    /// optimistic: a player that refuses keeps the button saying what the machine is doing.
    /// </summary>
    public void Press(MediaTransport command)
    {
        if (_detached || Preview || _session is null)
        {
            return;
        }

        _ = PressAsync(command);
    }

    private async Task PressAsync(MediaTransport command)
    {
        if (_session is not { } session)
        {
            return;
        }

        try
        {
            await session.InvokeAsync(command, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"the media session refused {command}", ex);
        }
    }

    /// <summary>[S-1465] Seek, and anchor the position there at once; the session's timeline push confirms or corrects it.</summary>
    public void Seek(TimeSpan target)
    {
        if (_detached || Preview || _session is null || !Now.CanSeek)
        {
            return;
        }

        Now = Now with { Timeline = Now.Timeline with { Position = target }, AnchoredAt = DateTimeOffset.UtcNow };
        _changed();
        _ = SeekAsync(target);
    }

    private async Task SeekAsync(TimeSpan target)
    {
        if (_session is not { } session)
        {
            return;
        }

        try
        {
            if (!await session.SeekAsync(target, Now.Origin, CancellationToken.None).ConfigureAwait(true))
            {
                // Refused: read the session again so the position goes back to where the track really is.
                Reload();
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "seeking failed", ex);
        }
    }

    /// <summary>The current track's cover art, decoded; null when it has none, the read was superseded or the bytes are no picture.</summary>
    public async Task<BitmapImage?> ArtworkAsync(CancellationToken token)
    {
        if (_session is not { } session)
        {
            return null;
        }

        return await DecodeAsync(await session.ReadArtworkAsync(token).ConfigureAwait(true), token).ConfigureAwait(true);
    }

    /// <summary>
    /// Decode <paramref name="bytes"/> as a picture. The bytes go into a stream on a background
    /// continuation and decode on XAML's own worker; both streams are disposed on every path, since this
    /// runs once per track for as long as the desktop is up.
    /// </summary>
    public static async Task<BitmapImage?> DecodeAsync(byte[]? bytes, CancellationToken token)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        using var stream = new InMemoryRandomAccessStream();
        var writer = new DataWriter(stream.GetOutputStreamAt(0));
        try
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync().AsTask(token).ConfigureAwait(true);
            await writer.FlushAsync().AsTask(token).ConfigureAwait(true);

            // Detached before disposal: a DataWriter closes the stream it was built on, and the
            // decoder is about to read from exactly that stream.
            writer.DetachStream();
        }
        finally
        {
            writer.Dispose();
        }

        stream.Seek(0);
        try
        {
            var image = new BitmapImage();
            await image.SetSourceAsync(stream).AsTask(token).ConfigureAwait(true);
            return image;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Debug(Scope, $"could not decode the album art: {ex.GetType().Name}");
            return null;
        }
    }

    /// <summary>Stop listening: cancel what is in flight (each task frees its own source) and close the session.</summary>
    public void Dispose()
    {
        _detached = true;
        _reader?.Cancel();
        _reader = null;
        _session?.Dispose();
        _session = null;
    }
}
