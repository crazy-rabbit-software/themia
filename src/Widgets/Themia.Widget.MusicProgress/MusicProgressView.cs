using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.MusicProgress;

/// <summary>
/// [S-11330] How far into what Windows is playing it is: the elapsed time, the seek bar (click to jump,
/// drag to scrub, [S-1465]) and the total, from the same media session as the track atom
/// (<see cref="NowPlayingFeed"/>). The one thing on a clock is the elapsed time, a second while a track
/// plays: arithmetic against the session's anchor, never a question asked of it.
/// </summary>
internal sealed partial class MusicProgressView : Grid, IWidgetView, IWidgetSurfaceAware
{
    private readonly TextBlock _elapsed = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly TextBlock _total = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly MusicSeekBar _seek = new();
    private readonly NowPlayingFeed _feed;

    private Ticker? _ticker;
    private IDisposable? _subscription;
    private MusicProgressConfig _config = new();
    private IWidgetSurface? _surface;
    private bool _preview;
    private bool _hidden;

    public MusicProgressView(IWidgetHost host)
    {
        _feed = new NowPlayingFeed(host.Media, Present);

        // [S-1113] The timeline keeps its direction in every language.
        FlowDirection = FlowDirection.LeftToRight;
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        VerticalAlignment = VerticalAlignment.Top;
        Height = MusicProgressFace.Row + FamilySpace.Inset;
        ColumnSpacing = FamilySpace.S2;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _total.HorizontalAlignment = HorizontalAlignment.Right;
        _seek.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(_seek, 1);
        SetColumn(_total, 2);
        Children.Add(_elapsed);
        Children.Add(_seek);
        Children.Add(_total);

        _seek.Scrubbing += OnScrubbing;
        _seek.Committed += fraction => _feed.Seek(MusicRules.SeekTarget(fraction, _feed.Now.Timeline.Duration));
    }

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

    /// <summary>The snapshot in hand: hide or show the card, the times and the bar, the second's tick while it plays.</summary>
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
        Resubscribe(now.State == MusicState.Playing && !_hidden);
    }

    /// <summary>The second's tick only while a track plays; nothing otherwise.</summary>
    private void Resubscribe(bool wanted)
    {
        if (wanted == (_subscription is not null))
        {
            return;
        }

        _subscription?.Dispose();
        _subscription = wanted ? _ticker?.Subscribe(TimeSpan.FromSeconds(1), UpdatePosition) : null;
    }

    /// <summary>Advance the bar and the times from the anchor. Display only: the next snapshot overwrites it.</summary>
    private void UpdatePosition()
    {
        var now = _feed.Now;
        if (!now.HasTrack)
        {
            _seek.Value = 0;
            _elapsed.Text = string.Empty;
            _total.Text = string.Empty;
            return;
        }

        var position = _feed.Position();
        if (!_seek.IsScrubbing)
        {
            _seek.Value = MusicRules.Progress(position, now.Timeline.Duration);
            _elapsed.Text = MusicRules.Clock(position);
        }

        // A stream with no end has no total to show, and "0:00" in that slot reads as a track that will not start.
        _total.Text = now.Timeline.Duration > TimeSpan.Zero ? MusicRules.Clock(now.Timeline.Duration) : string.Empty;
        var time = MusicProgressFace.TimeWidth(_total.Text);
        ColumnDefinitions[0].Width = new GridLength(time);
        ColumnDefinitions[2].Width = new GridLength(time);
    }

    /// <summary>[S-1465] The elapsed time follows the pointer while scrubbing; -1 means the scrub was abandoned.</summary>
    private void OnScrubbing(double fraction)
        => _elapsed.Text = fraction < 0
            ? MusicRules.Clock(_feed.Position())
            : MusicRules.Clock(MusicRules.SeekTarget(fraction, _feed.Now.Timeline.Duration));

    public void Detach()
    {
        _subscription?.Dispose();
        _subscription = null;
        _ticker = null;
        _feed.Dispose();
    }
}
