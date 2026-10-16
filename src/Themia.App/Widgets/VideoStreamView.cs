using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;

namespace Themia.App.Widgets;

/// <summary>
/// A camera on the desktop: the widget is the picture. Its only chrome is optional and kept off the
/// small tiles ([S-1312], <c>VideoStreamView.Overlay.cs</c>).
/// <para>
/// The cost is a socket that stays open and a JPEG decoded per frame, for as long as the picture
/// can be seen. So the session runs only while it can: it is torn down when the widget is paused,
/// folded away in a stack or a tab, or detached, and started again when it comes back. A refresh
/// interval swaps the open stream for one grabbed frame at a time.
/// </para>
/// <para>
/// MJPEG and snapshots are decoded here, by WIC, at the size they are drawn. A real video stream
/// (MP4, HLS) goes to Windows' own <see cref="MediaPlayer"/>, which is created only for such a
/// stream — a desktop of MJPEG cameras never loads the media pipeline at all.
/// </para>
/// </summary>
internal sealed partial class VideoStreamView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IStreamSink, IWidgetKeys
{
    private const string Scope = "video-stream";

    /// <summary>Below this height the message drops its icon; below the width, its body size.</summary>
    private const double IconNeedsHeight = 110;

    private const double BodyNeedsWidth = 170;

    private readonly RotatedBox _frame = new();
    private readonly Image _picture = new() { Stretch = Stretch.Uniform };
    private SoftwareBitmapSource? _pictureSource;
    private readonly Border _messageHost;
    private readonly TextBlock _message = new();
    private readonly FontIcon _messageIcon;
    private ShownWatch? _shown;

    private VideoStreamConfig _config = new();
    private IWidgetSurface? _surface;
    private StreamSession? _session;
    private StreamPhase _phase = StreamPhase.Connecting;
    private string? _detail;

    /// <summary>The address and cadence the running session was started for, so a cosmetic change does not reconnect.</summary>
    private (string Url, int Refresh) _sessionKey;

    private MediaPlayerElement? _videoElement;
    private MediaPlayer? _player;
    private MediaSource? _videoSource;

    /// <summary>[S-6602] The HLS source, kept to re-cap it when the tile changes size, and its renditions.</summary>
    private AdaptiveMediaSource? _adaptive;
    private IReadOnlyList<HlsVariant> _variants = [];
    private bool _showingVideo;
    private DecodeTarget _target = new(320, 240, StreamFit.Contain, false);
    private bool _detached;

    public VideoStreamView()
    {
        _picture.HorizontalAlignment = HorizontalAlignment.Center;
        _picture.VerticalAlignment = VerticalAlignment.Center;
        _picture.Visibility = Visibility.Collapsed;
        _frame.Children.Add(_picture);

        var empty = Fluent.EmptyState(Glyphs.Video, _message, out _messageIcon);
        _messageHost = new Border { Child = empty, VerticalAlignment = VerticalAlignment.Center };

        (_chip, _strip) = BuildOverlay();

        Children.Add(_frame);
        Children.Add(_messageHost);
        Children.Add(_chip);
        Children.Add(_strip);

        // Hit-testable everywhere, so the hover strip appears wherever the pointer is on the
        // picture, letterbox bars included.
        Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Microsoft.UI.Colors.Transparent);
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerGone;
        PointerCanceled += OnPointerGone;
        PointerCaptureLost += OnPointerGone;
    }

    // ---- IWidgetView ---------------------------------------------------------------------------

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _pictureSource ??= new SoftwareBitmapSource();
        _picture.Source = _pictureSource;
        _shown ??= new ShownWatch(this, _ => Reconcile());
        ApplyConfig(config);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as VideoStreamConfig ?? new VideoStreamConfig();
        PublishTitle();

        _frame.Turn = _config.EffectiveRotation;
        var stretch = StretchFor(_config.EffectiveFit);
        _picture.Stretch = stretch;

        if (_videoElement is not null)
        {
            _videoElement.Stretch = stretch;
        }

        UpdateTarget();
        Reconcile();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _shown?.Dispose();
        _shown = null;
        StopSession();
        StopChrome();
        _ticker = null;

        // Release the last frame's surface too; a detached widget holds nothing.
        _picture.Source = null;
        _pictureSource?.Dispose();
        _pictureSource = null;
    }

    // ---- context menu ----------------------------------------------------------------------------

    /// <summary>
    /// [S-469] Pause is a state flipped while looking at the camera, so it lives on the widget, not in settings.
    /// [S-1746] Rotation is how the picture looks: it is in the settings (and the overlay), not on the menu.
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
    [
        _config.Paused
            ? new WidgetAction(Glyphs.Play, Str.VideoStream_Resume.Text(), () => SetPaused(false))
            : new WidgetAction(Glyphs.Pause, Str.VideoStream_Pause.Text(), () => SetPaused(true)),
        new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), RefreshNow),
    ];

    /// <summary>[S-1991] Only a tile with a stream to pause takes the keyboard on a click.</summary>
    public bool WantsKeys => !_detached && !string.IsNullOrWhiteSpace(_config.Url);

    /// <summary>[S-1991] Space pauses and resumes the stream (<see cref="ControlKeys.Stream"/>).</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (!WantsKeys || ControlKeys.Stream(key) != ControlKey.Toggle)
        {
            return false;
        }

        SetPaused(!_config.Paused);
        return true;
    }

    private void SetPaused(bool paused)
    {
        _config.Paused = paused;
        _surface?.RequestSave();
        Reconcile();
    }

    private void Rotate()
    {
        _config.Rotation = VideoStreamRules.NextRotation(_config.EffectiveRotation);
        _surface?.RequestSave();
        _frame.Turn = _config.Rotation;
        UpdateTarget();
        RenderChrome();
    }

    /// <summary>[S-465] Reconnect now, with the backoff reset.</summary>
    private void RefreshNow()
    {
        StopSession();
        Reconcile();
    }

    // ---- the session ----------------------------------------------------------------------------

    /// <summary>
    /// Bring what is running in line with what should be: one session for the configured address
    /// while the widget is visible and not paused, and nothing otherwise.
    /// </summary>
    private void Reconcile()
    {
        if (_detached)
        {
            return;
        }

        var resolved = _config.ResolvedUrl;
        var key = (resolved.Url, _config.EffectiveRefreshSeconds);

        if (!resolved.IsValid || _config.Paused || _shown is not { IsShown: true })
        {
            StopSession();
            Render();
            return;
        }

        if (_session is not null && _sessionKey == key)
        {
            Render();
            return;
        }

        StopSession();

        if (DispatcherQueue.GetForCurrentThread() is not { } ui || !Uri.TryCreate(resolved.Url, UriKind.Absolute, out var uri))
        {
            Render();
            return;
        }

        _sessionKey = key;
        _phase = StreamPhase.Connecting;
        _detail = null;
        ForgetFrames();
        _session = new StreamSession(uri, key.Item2, DecodeTargetNow, this, ui);
        _session.Start();
        Render();
    }

    private void StopSession()
    {
        _session?.Dispose();
        _session = null;
        _sessionKey = default;
        StopVideo();
    }

    /// <summary>
    /// Read on the session's thread, so it is a snapshot taken on the UI thread whenever the size,
    /// fit or rotation changes — never a dependency-property read from a worker.
    /// </summary>
    private DecodeTarget DecodeTargetNow() => _target;

    private void UpdateTarget()
    {
        var scale = XamlRoot?.RasterizationScale ?? 1;
        _target = new DecodeTarget(
            (int)Math.Ceiling(_area.Width * scale),
            (int)Math.Ceiling(_area.Height * scale),
            _config.EffectiveFit,
            VideoStreamRules.IsSideways(_config.EffectiveRotation));
        CapBitrate();
    }

    /// <summary>
    /// [S-6602] Cap the HLS player at the smallest rendition that fills the tile at its physical size
    /// (turned sideways, the other way round); no cap when none does or the sizes are not declared.
    /// </summary>
    private void CapBitrate()
    {
        if (_adaptive is not { } adaptive)
        {
            return;
        }

        var (width, height) = _target.Sideways ? (_target.Height, _target.Width) : (_target.Width, _target.Height);
        var cap = HlsVariants.CapFor(_variants, width, height, cover: _target.Fit != StreamFit.Contain);

        if (adaptive.DesiredMaxBitrate != (uint?)cap)
        {
            adaptive.DesiredMaxBitrate = (uint?)cap;
            Log.Debug(Scope, cap is { } bits ? $"capped at {bits} bit/s for {width}x{height}" : "not capped");
        }
    }

    // ---- IStreamSink -----------------------------------------------------------------------------

    void IStreamSink.OnPhase(StreamPhase phase, string? detail)
    {
        if (_detached)
        {
            return;
        }

        _phase = phase;
        _detail = detail;
        NotePhase(phase);
        Render();
    }

    void IStreamSink.OnFrame(SoftwareBitmap frame, FrameFacts facts)
    {
        if (_detached || _session is null || _pictureSource is not { } source)
        {
            frame.Dispose();
            return;
        }

        NoteFrame(facts);
        _ = ShowFrameAsync(source, frame);
    }

    private async Task ShowFrameAsync(SoftwareBitmapSource source, SoftwareBitmap frame)
    {
        try
        {
            // The source copies the pixels, so the bitmap is ours to release as soon as it has.
            await source.SetBitmapAsync(frame);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ObjectDisposedException or ArgumentException)
        {
            Log.Warn(Scope, "could not show a decoded frame", ex);
        }
        finally
        {
            frame.Dispose();
        }
    }

    void IStreamSink.OnVideo(Uri uri, StreamTransport transport, IReadOnlyList<HlsVariant> variants)
    {
        if (_detached || _session is null)
        {
            return;
        }

        _transport = transport;
        _variants = variants;
        _ = PlayVideoAsync(uri, transport);
    }

    /// <summary>
    /// [S-471] Hand a real video stream to Windows' player. Muted: a desktop widget that starts
    /// talking is not a camera view, and the original had no sound to offer.
    /// </summary>
    private async Task PlayVideoAsync(Uri uri, StreamTransport transport)
    {
        var session = _session;

        try
        {
            MediaSource source;

            if (transport == StreamTransport.Hls)
            {
                var adaptive = await AdaptiveMediaSource.CreateFromUriAsync(uri);

                if (adaptive.Status != AdaptiveMediaSourceCreationStatus.Success)
                {
                    VideoFailed(Str.WidgetsC_VideoStream_PlaylistFailed.Format(("status", adaptive.Status.ToString())));
                    return;
                }

                source = MediaSource.CreateFromAdaptiveMediaSource(adaptive.MediaSource);

                // Re-pointed, paused or hidden while the playlist was opening: not this session's.
                if (!_detached && ReferenceEquals(session, _session))
                {
                    _adaptive = adaptive.MediaSource;
                    _adaptive.InitialBitrate = InitialFor(_adaptive);
                    CapBitrate();
                }
            }
            else
            {
                source = MediaSource.CreateFromUri(uri);
            }

            // Re-pointed, paused or hidden while the playlist was opening.
            if (_detached || !ReferenceEquals(session, _session))
            {
                source.Dispose();
                return;
            }

            EnsurePlayer();
            _videoSource = source;
            _player!.Source = source;
            _player.Play();
            _showingVideo = true;
            _phase = StreamPhase.Connecting;
            Render();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ArgumentException or InvalidOperationException)
        {
            Log.Warn(Scope, "could not start the video player", ex);
            VideoFailed(Str.WidgetsC_VideoStream_CannotPlay.Text());
        }
    }

    /// <summary>[S-6602] Start at the highest rendition under the cap rather than above it and stepping down.</summary>
    private uint InitialFor(AdaptiveMediaSource adaptive)
    {
        var (width, height) = _target.Sideways ? (_target.Height, _target.Width) : (_target.Width, _target.Height);
        var cap = HlsVariants.CapFor(_variants, width, height, cover: _target.Fit != StreamFit.Contain);
        var fitting = adaptive.AvailableBitrates.Where(b => cap is not { } c || b <= c).ToList();
        return fitting.Count > 0 ? fitting.Max() : adaptive.InitialBitrate;
    }

    private void EnsurePlayer()
    {
        if (_player is not null)
        {
            return;
        }

        _player = new MediaPlayer
        {
            IsMuted = true,
            IsLoopingEnabled = true,
            AutoPlay = true,
            AudioCategory = MediaPlayerAudioCategory.Other,
        };

        // Not shown in the system media flyout: a camera on the desktop is not "now playing".
        _player.CommandManager.IsEnabled = false;
        _player.MediaOpened += OnMediaOpened;
        _player.MediaFailed += OnMediaFailed;

        _videoElement = new MediaPlayerElement
        {
            AreTransportControlsEnabled = false,
            Stretch = StretchFor(_config.EffectiveFit),
            IsHitTestVisible = false,
        };

        _videoElement.SetMediaPlayer(_player);
        _frame.Children.Add(_videoElement);
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
        => DispatcherQueue.TryEnqueue(() =>
        {
            if (!_detached && _showingVideo)
            {
                _phase = StreamPhase.Live;
                _reconnects = 0;

                // [S-1313] The player knows the video's own size; there is no per-frame rate to measure.
                if (_player?.PlaybackSession is { } playback)
                {
                    _facts = new FrameFacts((int)playback.NaturalVideoWidth, (int)playback.NaturalVideoHeight, _transport);
                }

                Render();
            }
        });

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        var error = args.Error;
        DispatcherQueue.TryEnqueue(() => VideoFailed(Str.WidgetsC_VideoStream_PlaybackFailed.Format(("error", error.ToString()))));
    }

    /// <summary>
    /// The player gave up. Start a fresh session after the backoff, which probes the address again —
    /// a camera that rebooted may come back as something else entirely.
    /// </summary>
    private void VideoFailed(string detail)
    {
        if (_detached)
        {
            return;
        }

        Log.Warn(Scope, detail);
        StopSession();
        _phase = StreamPhase.Retrying;
        _detail = detail;
        NotePhase(StreamPhase.Retrying);
        Render();

        var retry = DispatcherQueue.CreateTimer();
        retry.IsRepeating = false;
        retry.Interval = VideoStreamRules.RetryMax;
        retry.Tick += (timer, _) =>
        {
            timer.Stop();
            Reconcile();
        };
        retry.Start();
    }

    /// <summary>Release the player entirely: a paused or hidden camera keeps no media pipeline alive.</summary>
    private void StopVideo()
    {
        _showingVideo = false;

        if (_player is null)
        {
            return;
        }

        _player.MediaOpened -= OnMediaOpened;
        _player.MediaFailed -= OnMediaFailed;
        _player.Pause();
        _player.Source = null;
        _player.Dispose();
        _player = null;

        // The source holds the connection and, for HLS, the playlist's refresh timer.
        _videoSource?.Dispose();
        _videoSource = null;
        _adaptive = null;
        _variants = [];

        if (_videoElement is not null)
        {
            _videoElement.SetMediaPlayer(null);
            _frame.Children.Remove(_videoElement);
            _videoElement = null;
        }
    }

    // ---- drawing ---------------------------------------------------------------------------------

    private static Stretch StretchFor(StreamFit fit) => fit switch
    {
        StreamFit.Cover => Stretch.UniformToFill,
        StreamFit.Stretch => Stretch.Fill,
        _ => Stretch.Uniform,
    };

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var resolved = _config.ResolvedUrl;
        string? message;
        var glyph = Glyphs.Video;

        if (!resolved.IsValid)
        {
            // No address yet: the card's Set up button covers this ([S-1862]).
            message = resolved.Problem == WebUrlProblem.Empty
                ? string.Empty
                : Explain(resolved.Problem);
        }
        else if (_config.Paused)
        {
            message = Str.VideoStream_Paused.Text();
            glyph = Glyphs.Pause;
        }
        else
        {
            message = _phase switch
            {
                StreamPhase.Live => null,
                StreamPhase.Connecting => Str.VideoStream_Connecting.Text(),
                StreamPhase.Unsupported => _detail ?? Str.WidgetsC_VideoStream_NotCamera.Text(),
                _ => Str.WidgetsC_VideoStream_UnavailableRetrying.Text(),
            };
        }

        var live = message is null;
        _picture.Visibility = live && !_showingVideo ? Visibility.Visible : Visibility.Collapsed;

        if (_videoElement is not null)
        {
            _videoElement.Visibility = live && _showingVideo ? Visibility.Visible : Visibility.Collapsed;
        }

        _messageHost.Visibility = live ? Visibility.Collapsed : Visibility.Visible;
        RenderChrome();

        if (message is null)
        {
            return;
        }

        _message.Text = message;
        _messageIcon.Glyph = glyph;

        // Shed detail on a small tile: the icon first, then the body size.
        _messageIcon.Visibility = _area.IsMeasured && _area.Height < IconNeedsHeight ? Visibility.Collapsed : Visibility.Visible;
        _message.Apply(_area.IsMeasured && _area.Width < BodyNeedsWidth ? FluentType.Caption : FluentType.Body);
        ToolTipService.SetToolTip(_messageHost, _phase == StreamPhase.Retrying ? _detail : null);
    }

    /// <summary>[S-1110] The message and the chrome again; the picture is untouched.</summary>
    private void OnLanguage(Locale locale) => Render();

    private static string Explain(WebUrlProblem problem) => problem switch
    {
        WebUrlProblem.UnsupportedScheme => Str.WidgetsC_VideoStream_OnlyHttp.Text(),
        WebUrlProblem.Credentials => Str.WidgetsC_VideoStream_RemoveCredentials.Text(),
        _ => WebInput.Explain(problem),
    };

    /// <summary>[S-460] The camera's host and port, unless the user named the widget.</summary>
    private void PublishTitle()
    {
        var title = VideoStreamRules.Title(_config.Url);
        _surface?.SetHeaderTitle(title.Length == 0 ? null : title);
    }
}
