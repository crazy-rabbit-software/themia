using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Music;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11311] Now playing's track atom: whatever Windows is playing, from Windows' own media session
/// (<see cref="NowPlayingFeed"/>; no account, no token, no per-player integration), as its title, its
/// artist and album line and the transport (<see cref="MusicFace"/>). Event-driven: it redraws when the
/// session says something changed; nothing is on a clock here (the position is the progress atom's).
/// The cover and the progress are atoms of their own that read the same session.
/// </summary>
internal sealed partial class MusicView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetKeys
{
    /// <inheritdoc />
    public CardOpen? Opens => MusicModule.Opens(_feed.Now);

    private readonly TextBlock _track = Family.Text(FamilyType.BodyStrong, ColourRole.TextPrimary);
    private readonly TextBlock _subtitle = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly StackPanel _lines = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly ControlsBlock _controls = new();
    private readonly Button _previous;
    private readonly Button _playPause;
    private readonly Button _next;
    private readonly StatusBlock _status = new();
    private readonly NowPlayingFeed _feed;
    private readonly string _glyph;

    private MusicConfig _config = new();
    private IWidgetSurface? _surface;
    private bool _preview;
    private bool _hidden;

    public MusicView(IWidgetHost host)
    {
        _feed = new NowPlayingFeed(host.Media, Present);
        _glyph = ModuleCatalog.Of(new WidgetId("music")).Glyph;

        // [S-11311] The lines at the content's top, the transport 4 under them at the bottom inset.
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _track.TextTrimming = TextTrimming.CharacterEllipsis;
        _subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _subtitle.MinHeight = FamilyType.BodyLine;
        _lines.Children.Add(_track);
        _lines.Children.Add(_subtitle);

        // [S-1113] Media transport keeps its order and direction in every language.
        _controls.FlowDirection = FlowDirection.LeftToRight;
        _controls.VerticalAlignment = VerticalAlignment.Bottom;
        _controls.HorizontalAlignment = HorizontalAlignment.Left;
        _previous = _controls.Icon(Glyphs.Previous, MusicStr.WidgetsB_Music_Previous.Text(), () => _feed.Press(MediaTransport.Previous));
        _playPause = _controls.MainIcon(Glyphs.Play, MusicStr.WidgetsB_Music_Play.Text(), () => _feed.Press(MediaTransport.PlayPause));
        _next = _controls.Icon(Glyphs.Next, MusicStr.WidgetsB_Music_Next.Text(), () => _feed.Press(MediaTransport.Next));

        Children.Add(_lines);
        Children.Add(_controls);
        _status.Hide();
        Children.Add(_status);
        _lines.Visibility = Visibility.Collapsed;
        _controls.Visibility = Visibility.Collapsed;
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
        _feed.Attach(DispatcherQueue);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MusicConfig ?? new MusicConfig();
        _feed.Reload();
        Present();
    }

    /// <summary>Put the snapshot in hand on screen and hide or show the card.</summary>
    private void Present()
    {
        var now = _feed.Now;

        // [S-1464] Paint nothing while idle, if asked to. Never in a preview card: it always draws.
        var hide = !_preview && MusicRules.Hides(_config.HideWhenIdle, now.State);
        if (hide != _hidden)
        {
            _hidden = hide;
            _surface?.SetHidden(hide);
        }

        var track = now.HasTrack;
        _lines.Visibility = track ? Visibility.Visible : Visibility.Collapsed;
        _controls.Visibility = track ? Visibility.Visible : Visibility.Collapsed;
        if (!track)
        {
            // [S-6714] Nothing to play is the Status row at the top: the music glyph and why.
            _status.Show(StatusKind.Empty, _glyph, MusicRules.StatusMessage(now.State));
            return;
        }

        _status.Hide();
        _track.Text = now.Title;
        _subtitle.Text = now.Subtitle;

        var playing = now.State == MusicState.Playing;
        ControlsBlock.Reglyph(_playPause, playing ? Glyphs.Pause : Glyphs.Play, playing ? MusicStr.WidgetsB_Music_Pause.Text() : MusicStr.WidgetsB_Music_Play.Text());

        // Disabled rather than hidden: a transport whose buttons come and go moves the two that
        // remain, and the one under the pointer is then a different button than it was.
        Enable(_previous, now.CanPrevious);
        _playPause.IsEnabled = now.CanPlayPause;
        Enable(_next, now.CanNext);
    }

    /// <summary>An icon button enabled or not, its glyph in the role that says so (its glyph names its own brush).</summary>
    private static void Enable(Button button, bool enabled)
    {
        button.IsEnabled = enabled;
        if (button.Content is FontIcon icon)
        {
            icon.Foreground = Family.Brush(enabled ? ColourRole.TextPrimary : ColourRole.TextDisabled);
        }
    }

    /// <summary>[S-1468] Only a tile with a track takes the keyboard on a click.</summary>
    public bool WantsKeys => _feed.Now.HasTrack && !_hidden;

    /// <summary>
    /// [S-1468] The original's keys: Space or Enter play/pause, Left/Right previous/next track,
    /// Shift+Left/Right five seconds back/forward.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        var now = _feed.Now;
        if (!now.HasTrack)
        {
            return false;
        }

        switch (key)
        {
            case WidgetKey.Space or WidgetKey.Enter when now.CanPlayPause:
                _feed.Press(MediaTransport.PlayPause);
                return true;
            case WidgetKey.Left when now.CanPrevious:
                _feed.Press(MediaTransport.Previous);
                return true;
            case WidgetKey.Right when now.CanNext:
                _feed.Press(MediaTransport.Next);
                return true;
            case WidgetKey.ShiftLeft or WidgetKey.ShiftRight when now.CanSeek:
                var delta = key == WidgetKey.ShiftLeft ? -MusicRules.NudgeStep : MusicRules.NudgeStep;
                _feed.Seek(MusicRules.Nudge(_feed.Position(), delta, now.Timeline.Duration));
                return true;
            default:
                return false;
        }
    }

    public void Detach() => _feed.Dispose();
}
