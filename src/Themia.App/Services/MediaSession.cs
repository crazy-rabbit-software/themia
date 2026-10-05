using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Themia.App.Services;

/// <summary>Which transport button was pressed. A closed set, so an unknown command cannot reach the session.</summary>
public enum MediaTransport
{
    PlayPause = 0,
    Next = 1,
    Previous = 2,
}

/// <summary>One snapshot of whatever Windows is playing.</summary>
/// <param name="State">Which of the five states the session is in. Every one of them has a sentence.</param>
/// <param name="Title">The track title, already collapsed to one displayable line.</param>
/// <param name="Subtitle">Artist, album, or both.</param>
/// <param name="TrackKey">Identity of the loaded track, for deciding whether the art in hand is stale.</param>
/// <param name="Timeline">Position and duration, already folded to a zero-based frame.</param>
/// <param name="AnchoredAt">When <paramref name="Timeline"/> was true, so the position can be advanced from it.</param>
/// <param name="CanPlayPause">Whether the session will accept a play/pause.</param>
/// <param name="CanNext">Whether there is a next track to go to.</param>
/// <param name="CanPrevious">Whether there is a previous track to go back to.</param>
/// <param name="CanSeek">[S-1465] Whether the session takes a new position (a live stream does not).</param>
/// <param name="Artist">[S-1469] The artist alone, for the large tile's own line.</param>
/// <param name="Album">[S-1469] The album alone; empty when it repeats the artist.</param>
/// <param name="Source">[S-1469] The playing app's name, e.g. "Spotify".</param>
/// <param name="Origin">The session's own start time, which a seek is expressed against.</param>
public readonly record struct NowPlaying(
    MusicState State,
    string Title,
    string Subtitle,
    string TrackKey,
    TrackTimeline Timeline,
    DateTimeOffset AnchoredAt,
    bool CanPlayPause,
    bool CanNext,
    bool CanPrevious,
    bool CanSeek = false,
    string Artist = "",
    string Album = "",
    string Source = "",
    TimeSpan Origin = default)
{
    /// <summary>A state with nothing in it. Used for loading and for every flavour of idle.</summary>
    public static NowPlaying Of(MusicState state) => new(
        state,
        string.Empty,
        string.Empty,
        string.Empty,
        default,
        DateTimeOffset.UtcNow,
        false,
        false,
        false);

    /// <summary>True when there is a track to draw, as opposed to a sentence to draw.</summary>
    public bool HasTrack => State is MusicState.Playing or MusicState.Paused;
}

/// <summary>
/// Windows' own media session, as much of it as a widget needs.
/// <para>
/// <c>GlobalSystemMediaTransportControlsSessionManager</c> is the same thing the media keys and the
/// volume flyout's transport drive, which is the entire reason this widget needs no account, no
/// token and no per-player integration: whatever is playing has already told Windows what it is.
/// </para>
/// <para>
/// <b>Event-driven, not polled.</b> The manager raises when the current session changes, and each
/// session raises when its metadata, its playback state or its timeline does. A poll would have to
/// run at a second to feel live and would spend the rest of the day asking a session that has not
/// moved. The one thing that does advance on a clock is the elapsed-time readout, and that is
/// arithmetic on an anchor rather than a question asked of the session.
/// </para>
/// </summary>
internal sealed class MediaSession : IDisposable
{
    private const string Scope = "music";

    private readonly Action _onChanged;

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _disposed;

    /// <summary>
    /// Kept as fields so the exact delegate instance can be handed back to <c>-=</c>. Subscribing
    /// with a method group would create a new delegate each time and unsubscribe nothing, which on
    /// a widget that is added and removed leaves a handler per instance calling into a torn-down
    /// view.
    /// </summary>
    private readonly Windows.Foundation.TypedEventHandler<
        GlobalSystemMediaTransportControlsSessionManager, CurrentSessionChangedEventArgs> _onCurrentSessionChanged;

    private readonly Windows.Foundation.TypedEventHandler<
        GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs> _onMediaPropertiesChanged;

    private readonly Windows.Foundation.TypedEventHandler<
        GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs> _onPlaybackInfoChanged;

    private readonly Windows.Foundation.TypedEventHandler<
        GlobalSystemMediaTransportControlsSession, TimelinePropertiesChangedEventArgs> _onTimelineChanged;

    /// <param name="onChanged">
    /// Raised whenever something about the session changed, on whatever thread WinRT used. The
    /// caller marshals; this class deliberately knows nothing about a dispatcher.
    /// </param>
    public MediaSession(Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(onChanged);
        _onChanged = onChanged;

        _onCurrentSessionChanged = (_, _) => OnSessionSwapped();
        _onMediaPropertiesChanged = (_, _) => Notify();
        _onPlaybackInfoChanged = (_, _) => Notify();
        _onTimelineChanged = (_, _) => Notify();
    }

    /// <summary>
    /// Connect to the session manager and start listening. Safe to call once; a second call is a
    /// no-op rather than a second set of handlers.
    /// </summary>
    public async Task StartAsync(CancellationToken token)
    {
        if (_manager is not null || _disposed)
        {
            return;
        }

        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager
                .RequestAsync()
                .AsTask(token)
                .ConfigureAwait(false);

            if (_disposed)
            {
                // Torn down while the request was out. Nothing was hooked yet, so there is nothing
                // to unhook — but hooking now would leave handlers on a manager nobody owns.
                return;
            }

            _manager = manager;
            _manager.CurrentSessionChanged += _onCurrentSessionChanged;
            OnSessionSwapped();
        }
        catch (OperationCanceledException)
        {
            // ignore: the widget was detached before the manager answered.
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not open the media session manager", ex);
            Notify();
        }
    }

    /// <summary>
    /// Move the per-session handlers onto whichever session is current now.
    /// <para>
    /// The manager's own event only says "the current session changed" — the metadata and timeline
    /// events belong to the session object, so they have to follow it. Unhooking the old one first
    /// is what stops a paused Spotify from still pushing updates over a video that has since taken
    /// the transport.
    /// </para>
    /// </summary>
    private void OnSessionSwapped()
    {
        try
        {
            DetachSession();

            if (_disposed)
            {
                return;
            }

            _session = _manager?.GetCurrentSession();

            if (_session is not null)
            {
                _session.MediaPropertiesChanged += _onMediaPropertiesChanged;
                _session.PlaybackInfoChanged += _onPlaybackInfoChanged;
                _session.TimelinePropertiesChanged += _onTimelineChanged;
            }
        }
        catch (Exception ex)
        {
            // Raised on a WinRT callback thread: an exception escaping here crosses the ABI.
            Log.Warn(Scope, "could not follow the current media session", ex);
        }

        Notify();
    }

    private void Notify()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _onChanged();
        }
        catch (Exception ex)
        {
            // Same reason: this runs on a WinRT callback, and an exception that escapes it is not
            // a managed exception any more.
            Log.Error(Scope, "a media session listener threw", ex);
        }
    }

    /// <summary>
    /// Read everything the tile draws, in one pass.
    /// <para>
    /// The timeline is anchored to <c>LastUpdatedTime</c> rather than to now: the session may have
    /// reported its position seconds ago, and treating a stale position as current is how a readout
    /// jumps backwards every time it refreshes.
    /// </para>
    /// </summary>
    public async Task<NowPlaying> ReadAsync(CancellationToken token)
    {
        var session = _session;

        if (session is null)
        {
            return NowPlaying.Of(MusicState.NoSession);
        }

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync().AsTask(token).ConfigureAwait(false);

            var title = MusicRules.Title(properties?.Title);
            var subtitle = MusicRules.Subtitle(properties?.Artist, properties?.AlbumTitle);
            var key = MusicRules.TrackKey(properties?.Title, properties?.Artist, properties?.AlbumTitle);

            var playback = session.GetPlaybackInfo();
            var status = playback?.PlaybackStatus
                ?? GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;

            var raw = session.GetTimelineProperties();

            var timeline = raw is null
                ? default
                : MusicRules.Normalise(raw.StartTime, raw.Position, raw.EndTime);

            // A session that is open but has nothing loaded is not the same as no session at all:
            // one means "your player is running", the other means "nothing on this PC claims the
            // media keys".
            var nothingLoaded = properties is null
                || (string.IsNullOrWhiteSpace(properties.Title) && string.IsNullOrWhiteSpace(properties.Artist));

            var state = status switch
            {
                _ when nothingLoaded => MusicState.Idle,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MusicState.Playing,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MusicState.Paused,
                _ => MusicState.Idle,
            };

            var controls = playback?.Controls;

            // LastUpdatedTime is the epoch when the session never reported one, which would anchor
            // the position in 1601 and interpolate a few centuries forward on the first tick.
            var anchoredAt = raw is not null && raw.LastUpdatedTime > DateTimeOffset.UnixEpoch
                ? raw.LastUpdatedTime
                : DateTimeOffset.UtcNow;

            return new NowPlaying(
                state,
                title,
                subtitle,
                key,
                timeline,
                anchoredAt,
                CanPlayPause: (controls?.IsPlayEnabled ?? false) || (controls?.IsPauseEnabled ?? false),
                CanNext: controls?.IsNextEnabled ?? false,
                CanPrevious: controls?.IsPreviousEnabled ?? false,
                CanSeek: (controls?.IsPlaybackPositionEnabled ?? false) && timeline.Duration > TimeSpan.Zero,
                Artist: MusicRules.Artist(properties?.Artist),
                Album: MusicRules.Album(properties?.Artist, properties?.AlbumTitle),
                Source: MusicRules.SourceName(SourceId(session)),
                Origin: raw?.StartTime ?? TimeSpan.Zero);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A session that closes between the handles being taken and the properties being read
            // throws rather than returning null. That is an idle tile, not a broken one.
            Log.Debug(Scope, $"the media session went away mid-read: {ex.GetType().Name}");
            return NowPlaying.Of(MusicState.NoSession);
        }
    }

    /// <summary>
    /// The album art as bytes, read off the UI thread.
    /// <para>
    /// The session hands over a stream <i>reference</i>, not a picture: opening it is I/O against
    /// whichever app is playing. Returns null whenever there is no art, which is most of the time —
    /// a browser tab playing a video usually has none.
    /// </para>
    /// </summary>
    public async Task<byte[]?> ReadArtworkAsync(CancellationToken token)
    {
        var session = _session;

        if (session is null)
        {
            return null;
        }

        IRandomAccessStreamWithContentType? stream = null;
        DataReader? reader = null;

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync().AsTask(token).ConfigureAwait(false);
            var thumbnail = properties?.Thumbnail;

            if (thumbnail is null)
            {
                return null;
            }

            stream = await thumbnail.OpenReadAsync().AsTask(token).ConfigureAwait(false);

            var size = stream.Size;

            // An empty stream is a player that offered art and had none. A huge one is not art a
            // 96px tile has any use for, and is not worth the allocation.
            if (size == 0 || size > MaxArtworkBytes)
            {
                return null;
            }

            reader = new DataReader(stream);
            await reader.LoadAsync((uint)size).AsTask(token).ConfigureAwait(false);

            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Debug(Scope, $"no album art from the session: {ex.GetType().Name}");
            return null;
        }
        finally
        {
            // Both hold a handle on the playing app's stream, and this runs once per track for as
            // long as the desktop is up.
            reader?.Dispose();
            stream?.Dispose();
        }
    }

    /// <summary>Four megabytes of cover art is already far more than a widget can show.</summary>
    private const ulong MaxArtworkBytes = 4 * 1024 * 1024;

    /// <summary>
    /// Press a transport button. Returns false when the session refused, so the view can leave the
    /// button showing what the machine is actually doing rather than what was asked of it.
    /// </summary>
    public async Task<bool> InvokeAsync(MediaTransport command, CancellationToken token)
    {
        var session = _session;

        if (session is null)
        {
            return false;
        }

        try
        {
            var operation = command switch
            {
                MediaTransport.Next => session.TrySkipNextAsync(),
                MediaTransport.Previous => session.TrySkipPreviousAsync(),
                _ => session.TryTogglePlayPauseAsync(),
            };

            return await operation.AsTask(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"the media session refused {command}", ex);
            return false;
        }
    }

    /// <summary>
    /// [S-1465] Move the track to <paramref name="position"/> (zero-based, as the tile shows it).
    /// Returns false when the session refused or cannot seek; the next timeline push says where it
    /// really is either way.
    /// </summary>
    public async Task<bool> SeekAsync(TimeSpan position, TimeSpan origin, CancellationToken token)
    {
        var session = _session;

        if (session is null)
        {
            return false;
        }

        try
        {
            // The session's frame starts at its own StartTime ([S-250]), so the zero-based position
            // the tile scrubbed to is shifted back into it.
            var ticks = (origin + position).Ticks;
            return await session.TryChangePlaybackPositionAsync(Math.Max(0, ticks)).AsTask(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "the media session refused a seek", ex);
            return false;
        }
    }

    /// <summary>The session's app id, or null when the session has gone.</summary>
    private static string? SourceId(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.SourceAppUserModelId;
        }
        catch (Exception ex)
        {
            Log.Debug(Scope, $"no app id from the media session: {ex.GetType().Name}");
            return null;
        }
    }

    private void DetachSession()
    {
        var session = _session;
        _session = null;

        if (session is null)
        {
            return;
        }

        try
        {
            session.MediaPropertiesChanged -= _onMediaPropertiesChanged;
            session.PlaybackInfoChanged -= _onPlaybackInfoChanged;
            session.TimelinePropertiesChanged -= _onTimelineChanged;
        }
        catch (Exception ex)
        {
            // Unhooking from a session whose player has already exited throws. The handlers die
            // with it either way; what matters is that this does not take the teardown down.
            Log.Debug(Scope, $"could not unhook a dead media session: {ex.GetType().Name}");
        }
    }

    /// <summary>Releases the manager and every handler. Safe to call twice.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DetachSession();

        var manager = _manager;
        _manager = null;

        if (manager is null)
        {
            return;
        }

        try
        {
            manager.CurrentSessionChanged -= _onCurrentSessionChanged;
        }
        catch (Exception ex)
        {
            Log.Debug(Scope, $"could not unhook the media session manager: {ex.GetType().Name}");
        }
    }
}
