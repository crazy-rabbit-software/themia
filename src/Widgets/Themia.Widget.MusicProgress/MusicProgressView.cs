using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.Widget.MusicProgress;

/// <summary>
/// [S-11330] [S-15104] How far into what Windows is playing it is, as a bar alone (owner, 2026-11-21: "track progress
/// should be split into three widgets too, two text based widgets and a scaling progress bar that also fills up to the
/// universal spacing given"): the seek bar (click to jump, drag to scrub, [S-1465]) filling the card's box less the
/// universal spacing, from the same media session as the other now-playing parts (<see cref="NowPlayingFeed"/>). The
/// elapsed time and the track's length are text atoms of the Now playing family (<c>musicElapsed</c>,
/// <c>musicDuration</c>). The bar advances once a second while a track plays: arithmetic against the session's anchor,
/// never a question asked of it.
/// </summary>
internal sealed partial class MusicProgressView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetTopEdge
{
    private readonly MusicSeekBar _seek = new();
    private readonly NowPlayingFeed _feed;

    private Ticker? _ticker;

    /// <summary>The ticker subscription at the cadence the view needs now (<see cref="Ticker.Slot"/>).</summary>
    private readonly Ticker.Slot _tick = new();
    private MusicProgressConfig _config = new();
    private IWidgetSurface? _surface;
    private bool _preview;
    private bool _hidden;

    public MusicProgressView(IWidgetHost host)
    {
        _feed = new NowPlayingFeed(host.Media, Present);
        Children.Add(_seek);
        _seek.Committed += fraction => _feed.Seek(MusicRules.SeekTarget(fraction, _feed.Now.Timeline.Duration));
    }

    /// <summary>[S-5700] It owns its top edge: the universal spacing is its own on every side.</summary>
    public bool OwnsTopEdge => true;

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        _preview = surface.IsPreview;
        _feed.Preview = surface.IsPreview;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _feed.Attach(DispatcherQueue);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MusicProgressConfig ?? new MusicProgressConfig();

        _feed.Reload();
        Present();
    }

    /// <summary>The snapshot in hand: hide or show the card, the bar, the second's tick while it plays.</summary>
    private void Present()
    {
        var now = _feed.Now;
        var hide = !_preview && MusicRules.Hides(_config.HideWhenIdle, now.State);
        if (hide != _hidden)
        {
            _hidden = hide;
            _surface?.SetHidden(hide);
        }

        _seek.CanSeek = now.HasTrack && now.CanSeek && !_preview;
        UpdatePosition();
        _ = _tick.Set(_ticker, now.State == MusicState.Playing && !_hidden ? TimeSpan.FromSeconds(1) : TimeSpan.Zero, UpdatePosition);
    }

    /// <summary>Advance the bar from the anchor. Display only: the next snapshot overwrites it. No track: an empty rail (WR-10).</summary>
    private void UpdatePosition()
    {
        var now = _feed.Now;
        if (!_seek.IsScrubbing)
        {
            _seek.Value = now.HasTrack ? MusicRules.Progress(_feed.Position(), now.Timeline.Duration) : 0;
        }
    }

    public void Detach()
    {
        _tick.Dispose();
        _ticker = null;
        _feed.Dispose();
    }
}
