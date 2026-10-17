using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Storage.Streams;

namespace Themia.App.Widgets;

/// <summary>
/// Whatever Windows is playing, from Windows' own media session.
/// <para>
/// No account, no token and no per-player integration:
/// <c>GlobalSystemMediaTransportControlsSessionManager</c> is the same session the media keys and
/// the volume flyout's transport drive, so anything on the machine that plays sound has already
/// told the OS what it is playing.
/// </para>
/// <para>
/// <b>Event-driven.</b> The session raises when its metadata, its playback state or its timeline
/// changes, and the tile redraws on that. The one thing on a clock is the elapsed-time readout,
/// which is arithmetic against the anchor the session gave — not a question asked of it. The
/// spectrum is a widget of its own now, the audio visualizer ([S-3200]).
/// </para>
/// <para>
/// Parts: this file holds the session, the transport, seeking, keys and hiding; the ladder that
/// places the parts is in <c>MusicView.Layout.cs</c>.
/// </para>
/// </summary>
internal sealed partial class MusicView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private const string Scope = "music";

    private readonly Canvas _canvas = new();

    private readonly Border _artHost;

    /// <summary>[S-6407] The Cover style's gradient under the lines and the transport, so they read over any art.</summary>
    private readonly Border _scrim = new() { IsHitTestVisible = false };
    private readonly Image _art = new();
    private readonly FontIcon _artPlaceholder;

    private readonly TextBlock _track = Fluent.Text(FluentType.BodyStrong);
    private readonly TextBlock _subtitle = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private readonly TextBlock _album = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private readonly TextBlock _elapsed = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly TextBlock _total = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly MusicSeekBar _seek = new();

    private readonly Button _previous = new();
    private readonly Button _playPause = new();
    private readonly Button _next = new();
    private readonly FontIcon _playPauseIcon;
    private readonly FontIcon[] _transportIcons;
    private readonly StackPanel _transport;

    private readonly StackPanel _empty;
    private readonly TextBlock _emptyMessage = new();

    private Ticker? _ticker;
    private MusicConfig _config = new();
    private MediaSession? _session;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private IWidgetSurface? _surface;
    private DispatcherQueue? _queue;

    private NowPlaying _now = NowPlaying.Of(MusicState.Loading);
    private CancellationTokenSource? _reader;
    private CancellationTokenSource? _artwork;
    private string _artKey = string.Empty;

    /// <summary>
    /// Which read is current. A snapshot carrying an older number belongs to a session the widget
    /// has moved on from, and drawing it would put the previous track back on screen.
    /// </summary>
    private int _generation;

    private bool _detached;
    private bool _preview;
    private bool _hidden;

#if DEBUG
    /// <summary>Debug renders only: a sample track is on screen, and the real session must not replace it.</summary>
    private bool _frozen;
#else
    private const bool _frozen = false;
#endif

    public MusicView()
    {
        var definition = ModuleCatalog.Of(WidgetKind.Music);

        _artPlaceholder = Fluent.Icon(definition.Glyph, "TextFillColorTertiaryBrush", Fluent.Space6);
        _artPlaceholder.HorizontalAlignment = HorizontalAlignment.Center;
        _artPlaceholder.VerticalAlignment = VerticalAlignment.Center;

        _art.Stretch = Stretch.UniformToFill;
        _art.Visibility = Visibility.Collapsed;

        // The picture over the placeholder rather than instead of it: swapping the Border's child per
        // track would rebuild the visual on every skip. A Border clips to its radius, so a cover that
        // is not square cannot paint over the rounded corners.
        _artHost = new Border
        {
            Child = new Grid { Children = { _artPlaceholder, _art } },
            Background = FluentLayout.Themed("ControlFillColorSecondaryBrush", Colors.DimGray),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
        };

        _playPauseIcon = Fluent.Icon(Glyphs.Play, "TextFillColorPrimaryBrush");
        var previousIcon = Fluent.Icon(Glyphs.Previous, "TextFillColorPrimaryBrush");
        var nextIcon = Fluent.Icon(Glyphs.Next, "TextFillColorPrimaryBrush");
        _transportIcons = [previousIcon, _playPauseIcon, nextIcon];

        StyleButton(_previous, previousIcon, Str.WidgetsB_Music_Previous.Text());
        StyleButton(_playPause, _playPauseIcon, Str.WidgetsB_Music_PlayPause.Text());
        StyleButton(_next, nextIcon, Str.WidgetsB_Music_Next.Text());

        _previous.Click += OnPrevious;
        _playPause.Click += OnPlayPause;
        _next.Click += OnNext;

        _transport = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space1,
            Children = { _previous, _playPause, _next },

            // [S-1113] Media transport keeps its order and direction in every language.
            FlowDirection = FlowDirection.LeftToRight,
        };

        _total.TextAlignment = TextAlignment.Right;
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_elapsed, FontNumeralAlignment.Tabular);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_total, FontNumeralAlignment.Tabular);

        _seek.Scrubbing += OnScrubbing;
        _seek.Committed += OnSeekCommitted;

        _emptyMessage.Text = MusicRules.StatusMessage(MusicState.Loading);
        _empty = Fluent.EmptyState(definition.Glyph, _emptyMessage, out _);
        _empty.Visibility = Visibility.Collapsed;

        foreach (var part in new FrameworkElement[] { _artHost, _scrim, _track, _subtitle, _album, _transport, _seek, _elapsed, _total })
        {
            part.Visibility = Visibility.Collapsed;
            _canvas.Children.Add(part);
        }

        Children.Add(_canvas);
        Children.Add(_empty);

        static void StyleButton(Button button, FontIcon icon, string tooltip)
        {
            button.Content = icon;
            button.Padding = new Thickness(0);
            button.CornerRadius = new CornerRadius(Fluent.ControlRadius);
            ToolTipService.SetToolTip(button, tooltip);
        }
    }

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        _preview = surface.IsPreview;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        _queue = DispatcherQueue;

        // [S-6407] The scrim is built from the theme's own base fill, so it is redrawn with the theme.
        ActualThemeChanged += OnThemeChanged;

        _session ??= new MediaSession(OnSessionChanged);

        // Opening the manager is an async WinRT call; the first snapshot lands through the same
        // change notification as every later one, so there is one path into Present and not two.
        _ = StartSessionAsync();

        ApplyConfig(config);
    }

    private async Task StartSessionAsync()
    {
        var session = _session;

        if (session is null)
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

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MusicConfig ?? new MusicConfig();

        // [S-6406] Text only draws no art, so none is held or fetched.
        if (!_config.ShowsArt)
        {
            ClearArtwork();
        }

        // A config touch can change what has to be fetched — the art, most obviously — so the
        // snapshot is re-read rather than merely redrawn. Session reads are cheap and rare.
        Reload();
        Present();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>
    /// The position ticker: one second while a track is playing and its time is shown, nothing
    /// otherwise. Rebuilt only when the cadence changed, so a redraw never tears down the shared timer.
    /// </summary>
    private void Resubscribe()
    {
        var wanted = _now.State == MusicState.Playing && _config.ShowProgress && !_hidden
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.Zero;

        if (wanted == _subscribedInterval && (_subscription is not null || wanted == TimeSpan.Zero))
        {
            return;
        }

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = wanted;

        if (wanted != TimeSpan.Zero)
        {
            _subscription = _ticker?.Subscribe(wanted, Advance);
        }
    }

    /// <summary>Move the elapsed readout on, without asking the session anything.</summary>
    private void Advance()
    {
        if (!_detached && _now.HasTrack)
        {
            UpdatePosition();
        }
    }

    /// <summary>The session said something changed. Raised on a WinRT thread, so it hops to the dispatcher first.</summary>
    private void OnSessionChanged()
    {
        if (_detached)
        {
            return;
        }

        var queue = _queue;

        if (queue is null || !queue.TryEnqueue(Reload))
        {
            Log.Debug(Scope, "a session change arrived with no dispatcher to draw it on");
        }
    }

    private void Reload()
    {
        if (_detached || _frozen || _session is null)
        {
            return;
        }

        _reader?.Cancel();

        var cts = new CancellationTokenSource();
        _reader = cts;
        var generation = ++_generation;

        _ = ReadAsync(generation, cts);
    }

    private async Task ReadAsync(int generation, CancellationTokenSource cts)
    {
        try
        {
            var session = _session;

            if (session is null)
            {
                return;
            }

            var now = await session.ReadAsync(cts.Token).ConfigureAwait(true);

            if (_detached || generation != _generation)
            {
                return;
            }

            _now = now;
            Present();

            if (_config.ShowsArt && now.HasTrack && now.TrackKey != _artKey)
            {
                await LoadArtworkAsync(now.TrackKey, generation, cts.Token).ConfigureAwait(true);
            }
            else if (!now.HasTrack)
            {
                ClearArtwork();
            }
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
    /// Fetch the album art and put it on screen. The bytes come off the session's stream on a
    /// background continuation and decode on XAML's own worker; both streams are disposed on every
    /// path, since this runs once per track for as long as the desktop is up.
    /// </summary>
    private async Task LoadArtworkAsync(string key, int generation, CancellationToken token)
    {
        var session = _session;

        if (session is null)
        {
            return;
        }

        var bytes = await session.ReadArtworkAsync(token).ConfigureAwait(true);
        await ShowArtworkAsync(bytes, key, generation).ConfigureAwait(true);
    }

    /// <summary>Decode <paramref name="bytes"/> and put them on screen as the art of <paramref name="key"/>.</summary>
    private async Task ShowArtworkAsync(byte[]? bytes, string key, int generation)
    {
        if (_detached || generation != _generation)
        {
            return;
        }

        if (bytes is null || bytes.Length == 0)
        {
            ClearArtwork();

            // Remembered even when there was none, so a track with no cover is not re-fetched on
            // every timeline push.
            _artKey = key;
            return;
        }

        _artwork?.Cancel();
        var cts = new CancellationTokenSource();
        _artwork = cts;

        InMemoryRandomAccessStream? stream = null;

        try
        {
            stream = new InMemoryRandomAccessStream();

            var writer = new DataWriter(stream.GetOutputStreamAt(0));

            try
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync().AsTask(cts.Token).ConfigureAwait(true);
                await writer.FlushAsync().AsTask(cts.Token).ConfigureAwait(true);

                // Detached before disposal: a DataWriter closes the stream it was built on, and the
                // decoder is about to read from exactly that stream.
                writer.DetachStream();
            }
            finally
            {
                writer.Dispose();
            }

            stream.Seek(0);

            var image = new BitmapImage();
            await image.SetSourceAsync(stream).AsTask(cts.Token).ConfigureAwait(true);

            if (_detached || generation != _generation)
            {
                return;
            }

            _art.Source = image;
            _art.Visibility = Visibility.Visible;
            _artPlaceholder.Visibility = Visibility.Collapsed;
            _artKey = key;
        }
        catch (OperationCanceledException)
        {
            // ignore: a newer track's art is already on its way.
        }
        catch (Exception ex)
        {
            Log.Debug(Scope, $"could not decode the album art: {ex.GetType().Name}");
            ClearArtwork();
        }
        finally
        {
            if (ReferenceEquals(_artwork, cts))
            {
                _artwork = null;
            }

            cts.Dispose();
            stream?.Dispose();
        }
    }

    private void ClearArtwork()
    {
        _art.Source = null;
        _art.Visibility = Visibility.Collapsed;
        _artPlaceholder.Visibility = Visibility.Visible;
        _artKey = string.Empty;
    }

    /// <summary>Put the snapshot in hand on screen, hide or show the card, and settle the layout around it.</summary>
    private void Present()
    {
        // [S-1464] Paint nothing while idle, if asked to. Never in a preview card: it always draws.
        var hide = !_preview && MusicRules.Hides(_config.HideWhenIdle, _now.State);
        if (hide != _hidden)
        {
            _hidden = hide;
            _surface?.SetHidden(hide);
        }

        var track = _now.HasTrack;

        _canvas.Visibility = track ? Visibility.Visible : Visibility.Collapsed;
        _empty.Visibility = track ? Visibility.Collapsed : Visibility.Visible;

        if (!track)
        {
            _emptyMessage.Text = MusicRules.StatusMessage(_now.State);
        }
        else
        {
            _track.Text = _now.Title;
            _subtitle.Text = _now.Subtitle;
            _album.Text = _now.Album;

            var playing = _now.State == MusicState.Playing;

            _playPauseIcon.Glyph = playing ? Glyphs.Pause : Glyphs.Play;
            ToolTipService.SetToolTip(_playPause, playing ? Str.WidgetsB_Music_Pause.Text() : Str.WidgetsB_Music_Play.Text());

            // Disabled rather than hidden: a transport whose buttons come and go moves the two that
            // remain, and the one under the pointer is then a different button than it was.
            _previous.IsEnabled = _now.CanPrevious;
            _playPause.IsEnabled = _now.CanPlayPause;
            _next.IsEnabled = _now.CanNext;
            _seek.CanSeek = _now.CanSeek && !_preview;

            UpdatePosition();
        }

        Lay();
        Resubscribe();
    }

    /// <summary>Advance the bar and the labels from the anchor. Display only: the next snapshot overwrites it.</summary>
    private void UpdatePosition()
    {
        var position = Position();

        if (!_seek.IsScrubbing)
        {
            _seek.Value = MusicRules.Progress(position, _now.Timeline.Duration);
            _elapsed.Text = MusicRules.Clock(position);
        }

        // A stream with no end has no total to show, and "0:00" in that slot reads as a track that
        // will not start.
        _total.Text = _now.Timeline.Duration > TimeSpan.Zero ? MusicRules.Clock(_now.Timeline.Duration) : string.Empty;
    }

    private TimeSpan Position() => MusicRules.Interpolate(_now.Timeline, _now.AnchoredAt, DateTimeOffset.UtcNow, _now.State == MusicState.Playing);

    // ---- transport, seeking and keys --------------------------------------------------------------

    private void OnPlayPause(object sender, RoutedEventArgs args) => Press(MediaTransport.PlayPause);

    private void OnNext(object sender, RoutedEventArgs args) => Press(MediaTransport.Next);

    private void OnPrevious(object sender, RoutedEventArgs args) => Press(MediaTransport.Previous);

    /// <summary>
    /// Ask the session to do something, and let its own change notification draw the result.
    /// Deliberately not optimistic: if the player refuses, the button keeps saying what the machine
    /// is actually doing. Inert in a preview card: it never touches real playback.
    /// </summary>
    private void Press(MediaTransport command)
    {
        if (_detached || _preview || _session is null)
        {
            return;
        }

        _ = PressAsync(command);
    }

    private async Task PressAsync(MediaTransport command)
    {
        var session = _session;

        if (session is null)
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

    /// <summary>[S-1465] The elapsed label follows the pointer while scrubbing; -1 means the scrub was abandoned.</summary>
    private void OnScrubbing(double fraction)
    {
        _elapsed.Text = fraction < 0
            ? MusicRules.Clock(Position())
            : MusicRules.Clock(MusicRules.SeekTarget(fraction, _now.Timeline.Duration));
    }

    private void OnSeekCommitted(double fraction) => Seek(MusicRules.SeekTarget(fraction, _now.Timeline.Duration));

    /// <summary>[S-1465] Seek, and anchor the readout there at once; the session's timeline push confirms or corrects it.</summary>
    private void Seek(TimeSpan target)
    {
        if (_detached || _preview || _session is null || !_now.CanSeek)
        {
            return;
        }

        _now = _now with { Timeline = _now.Timeline with { Position = target }, AnchoredAt = DateTimeOffset.UtcNow };
        UpdatePosition();
        _ = SeekAsync(target);
    }

    private async Task SeekAsync(TimeSpan target)
    {
        var session = _session;

        if (session is null)
        {
            return;
        }

        try
        {
            if (!await session.SeekAsync(target, _now.Origin, CancellationToken.None).ConfigureAwait(true))
            {
                // Refused: read the session again so the bar goes back to where the track really is.
                Reload();
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "seeking failed", ex);
        }
    }

    /// <summary>[S-1468] Only a tile with a track takes the keyboard on a click.</summary>
    public bool WantsKeys => _now.HasTrack && !_hidden;

    /// <summary>
    /// [S-1468] The original's keys: Space or Enter play/pause, Left/Right previous/next track,
    /// Shift+Left/Right five seconds back/forward.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || !_now.HasTrack)
        {
            return false;
        }

        switch (key)
        {
            case WidgetKey.Space or WidgetKey.Enter when _now.CanPlayPause:
                Press(MediaTransport.PlayPause);
                return true;
            case WidgetKey.Left when _now.CanPrevious:
                Press(MediaTransport.Previous);
                return true;
            case WidgetKey.Right when _now.CanNext:
                Press(MediaTransport.Next);
                return true;
            case WidgetKey.ShiftLeft or WidgetKey.ShiftRight when _now.CanSeek:
                var delta = key == WidgetKey.ShiftLeft ? -MusicRules.NudgeStep : MusicRules.NudgeStep;
                Seek(MusicRules.Nudge(Position(), delta, _now.Timeline.Duration));
                return true;
            default:
                return false;
        }
    }

    private void OnThemeChanged(FrameworkElement sender, object args) => Lay();

    public void Detach()
    {
        _detached = true;
        ActualThemeChanged -= OnThemeChanged;

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        // Cancel but do not dispose: each in-flight task owns its source and frees it on the way out.
        _reader?.Cancel();
        _reader = null;
        _artwork?.Cancel();
        _artwork = null;

        _session?.Dispose();
        _session = null;

        _art.Source = null;
        _ticker = null;
    }
}
