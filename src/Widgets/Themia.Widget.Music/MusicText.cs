using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.Music;

/// <summary>
/// [S-14921] One text of the track as a text kind ([S-14934]): at home in the Now playing family under Music &amp; media
/// (<see cref="AddFolderId.NowPlaying"/>); its one option when it hides ([S-1464]). [S-15108] It is a text atom: its card
/// is the atom's (<see cref="AtomRules.TextSizing"/>), a title's ink for the title, a detail's for the rest, the times a
/// short card. SPEC.md beside this file.
/// </summary>
public abstract class MusicTextModule(string key)
    : TextModule(MusicKinds.Text(key), MusicJsonContext.Default, AddFolderId.NowPlaying, MusicKinds.Rank(key), key == MusicKinds.Title ? 7 : null,
        links: key == MusicKinds.Elapsed ? LinkUse.Of([], [LinkTopicId.ScrubbedTrack]) : null)
{
    /// <summary>[S-15108] A new text's width, and a new time's.</summary>
    public const double TextWidth = 240, TimeWidth = 60;

    private readonly string _key = key;

    /// <summary>[S-1479] When it hides.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => MusicMenu.Lines(menu, (MusicPartConfig)config);

    /// <inheritdoc />
    protected override WidgetSizing Sizing() => _key switch
    {
        MusicKinds.Title => AtomRules.TextSizing(TextWidth, AtomRules.TitleInk),
        MusicKinds.Elapsed or MusicKinds.Duration => AtomRules.TextSizing(TimeWidth, AtomRules.DetailInk),
        _ => AtomRules.TextSizing(TextWidth, AtomRules.DetailInk),
    };

    public override FrameworkElement CreateView(IWidgetHost host) => new MusicTextView(host, Kind);
}

/// <summary>
/// [S-14921] One text of what Windows is playing on the text atom's face (<see cref="AtomTextView"/>), read from Windows'
/// own media session through the SDK's <see cref="NowPlayingFeed"/> (so the title, the artist, the album, the buttons, the
/// cover and the progress show the same track at the same moment). Event-driven: it redraws when the session says
/// something changed; [S-15106] the elapsed time alone is on a clock, a second while a track plays, arithmetic against the
/// session's anchor, and [S-15109] it follows a bar's scrub through the view's links. Hidden when idle as its config says
/// ([S-1464]).
/// </summary>
internal sealed partial class MusicTextView : AtomTextView
{
    private readonly NowPlayingFeed _feed;
    private readonly string _key;

    /// <summary>The ticker subscription at the cadence the view needs now (<see cref="Ticker.Slot"/>).</summary>
    private readonly Ticker.Slot _tick = new();
    private Ticker? _ticker;
    private MusicPartConfig _config = new MusicConfig();

    /// <summary>[S-15109] The view's links: the elapsed time follows a bar's scrub.</summary>
    private readonly WidgetLinks _links = new();
    private IWidgetSurface? _surface;
    private TrackLink? _scrub;

    public MusicTextView(IWidgetHost host, TextKind kind)
        : base(host, kind)
    {
        _key = kind.Key;
        _feed = new NowPlayingFeed(host.Media, Render);
        if (MusicKinds.IsTime(_key))
        {
            // [S-1113] A time keeps its direction in every language; [S-14319] its figures never move as it runs.
            FlowDirection = FlowDirection.LeftToRight;
            Line.Figures();
        }

        if (_key == MusicKinds.Elapsed)
        {
            // [S-15109] While a bar of the view is scrubbed, the elapsed time is where the pointer has it.
            _links.Follow(LinkTopics.ScrubbedTrack, scrub =>
            {
                _scrub = scrub;
                Show();
            });
        }
    }

    protected override void OnBind(IWidgetSurface surface)
    {
        _surface = surface;
        _feed.Preview = surface.IsPreview;
    }

    protected override void OnAttach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _feed.Attach(DispatcherQueue);
        _links.Join(_surface?.Links);
        OnApply(config);
    }

    protected override void OnApply(WidgetConfig config)
    {
        _config = config as MusicPartConfig ?? new MusicConfig();
        _feed.Reload();
        Render();
    }

    protected override void OnDetach()
    {
        _links.Leave();
        _scrub = null;
        _tick.Dispose();
        _ticker = null;
        _feed.Dispose();
    }

    private void Render()
    {
        var now = _feed.Now;
        var hides = MusicRules.Hides(_config.HideWhenIdle, now.State);
        Hide(hides);
        Show();

        // [S-15106] The second's tick only for the elapsed time, only while a track plays.
        var runs = _key == MusicKinds.Elapsed && now.State == MusicState.Playing && !hides;
        _ = _tick.Set(_ticker, runs ? TimeSpan.FromSeconds(1) : TimeSpan.Zero, Show);
    }

    private void Show()
        => Present(MusicKinds.Shown(_key, _feed.Now, _key == MusicKinds.Elapsed ? MusicKinds.Position(_feed.Now, _feed.Position(), _scrub) : default));
}
