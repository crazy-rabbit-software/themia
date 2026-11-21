using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Music;

/// <summary>
/// [S-14922] One transport button as a widget (owner, 2026-11-21: "now playing widget should be split into the individual
/// buttons"): previous, play or pause, or next, an icon kind ([S-15003]: the one icon face, its glyph filling its card, a
/// button edge to edge, [S-15005]); at home in the Now playing family. Its one option is when it hides ([S-1464]).
/// </summary>
public abstract class MusicButtonModule(MediaTransport transport)
    : IconModule(MusicKinds.Button(transport), MusicJsonContext.Default, AddFolderId.NowPlaying, MusicKinds.Rank(transport))
{
    /// <summary>[S-1479] When it hides.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => MusicMenu.Lines(menu, (MusicPartConfig)config);

    public override FrameworkElement CreateView(IWidgetHost host) => new MusicButtonView(transport, host, Kind);
}

/// <summary>[S-14922] A transport button's words and looks, pure: its glyph, its name and what its colours say.</summary>
public static class MusicButtons
{
    /// <summary>[S-14922] The add page's name: "Previous track", "Play or pause", "Next track".</summary>
    public static LocalText Label(MediaTransport transport) => (transport switch
    {
        MediaTransport.Previous => MusicStr.Music_Previous_Label,
        MediaTransport.Next => MusicStr.Music_Next_Label,
        _ => MusicStr.WidgetsB_Music_PlayPause,
    }).Local();

    /// <summary>The add page's line under the name.</summary>
    public static LocalText Description(MediaTransport transport) => (transport switch
    {
        MediaTransport.Previous => MusicStr.Music_Previous_Desc,
        MediaTransport.Next => MusicStr.Music_Next_Desc,
        _ => MusicStr.Music_PlayPause_Desc,
    }).Local();

    /// <summary>Its search words beside the family's.</summary>
    public static IReadOnlyList<string> Words(MediaTransport transport) => transport switch
    {
        MediaTransport.Previous => ["previous", "back", "skip back", "rewind"],
        MediaTransport.Next => ["next", "skip", "forward"],
        _ => ["play", "pause", "resume", "stop"],
    };

    /// <summary>[S-14922] The glyph: previous, next, and play or (while it plays) pause.</summary>
    public static string Glyph(MediaTransport transport, bool playing) => transport switch
    {
        MediaTransport.Previous => Glyphs.Previous,
        MediaTransport.Next => Glyphs.Next,
        _ => playing ? Glyphs.Pause : Glyphs.Play,
    };

    /// <summary>
    /// [S-14922] What the icon shows for <paramref name="now"/>: its glyph, its name (its tooltip: what a click does,
    /// "Previous", "Play", "Pause", "Next") and its state. A button the player cannot do now, or any with no track, keeps its
    /// glyph drawn unavailable ([S-1940], WR-10), why its tooltip where there is no track; play or pause is the accent while it
    /// plays (On, as a toggle that is on), the others the item layer's tile at rest.
    /// </summary>
    public static IconShown Shown(MediaTransport transport, NowPlaying now)
    {
        var playing = now.HasTrack && now.State == MusicState.Playing;
        var name = (transport switch
        {
            MediaTransport.Previous => MusicStr.WidgetsB_Music_Previous,
            MediaTransport.Next => MusicStr.WidgetsB_Music_Next,
            _ => playing ? MusicStr.WidgetsB_Music_Pause : MusicStr.WidgetsB_Music_Play,
        }).Text();
        var can = now.HasTrack && transport switch
        {
            MediaTransport.Previous => now.CanPrevious,
            MediaTransport.Next => now.CanNext,
            _ => now.CanPlayPause,
        };
        var state = !can ? TileState.Unavailable : playing && transport == MediaTransport.PlayPause ? TileState.On : TileState.Off;
        return new IconShown(Glyph(transport, playing), state) { Name = name, Why = now.HasTrack ? null : MusicRules.StatusMessage(now.State) };
    }
}

/// <summary>
/// [S-14922] A transport button's view on the one icon face ([S-15001]): its glyph, the accent while play or pause plays,
/// unavailable while the player cannot do it. A click (and Space or Enter while it has the keyboard) asks the media session
/// through the host (<see cref="NowPlayingFeed.Press"/>), never optimistic: the session's own change notification draws the
/// result. In edit mode the press is the card's ([S-1911]); a preview presses nothing.
/// </summary>
internal sealed partial class MusicButtonView : IconView
{
    private readonly MediaTransport _transport;
    private readonly NowPlayingFeed _feed;
    private MusicPartConfig _config = new MusicPlayPauseConfig();

    public MusicButtonView(MediaTransport transport, IWidgetHost host, IconKind kind)
        : base(host, kind, presses: true)
    {
        _transport = transport;
        _feed = new NowPlayingFeed(host.Media, Render);
    }

    protected override void OnBind(IWidgetSurface surface) => _feed.Preview = surface.IsPreview;

    protected override void OnAttach(Ticker ticker, WidgetConfig config)
    {
        _feed.Attach(DispatcherQueue);
        OnApply(config);
    }

    protected override void OnApply(WidgetConfig config)
    {
        _config = config as MusicPartConfig ?? new MusicPlayPauseConfig();
        _feed.Reload();
        Render();
    }

    protected override void OnDetach() => _feed.Dispose();

    /// <summary>[S-14922] Ask the session; its notification draws the result.</summary>
    protected override void Press() => _feed.Press(_transport);

    /// <summary>The whole look from the session; hidden when idle ([S-1464]).</summary>
    private void Render()
    {
        var now = _feed.Now;
        Hide(MusicRules.Hides(_config.HideWhenIdle, now.State));
        Present(MusicButtons.Shown(_transport, now));
    }
}
