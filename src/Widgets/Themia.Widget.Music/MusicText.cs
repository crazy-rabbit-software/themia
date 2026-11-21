using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.Music;

/// <summary>
/// [S-14921] One text of the track as a text kind ([S-14934]): at home in the Now playing family under Music &amp; media
/// (<see cref="AddFolderId.NowPlaying"/>); its one option when it hides ([S-1464]). SPEC.md beside this file.
/// </summary>
public abstract class MusicTextModule(string key)
    : TextModule(MusicKinds.Text(key), MusicJsonContext.Default, AddFolderId.NowPlaying, MusicKinds.Rank(key), key == MusicKinds.Title ? 7 : null)
{
    /// <summary>[S-1479] When it hides.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config) => MusicMenu.Lines(menu, (MusicPartConfig)config);

    public override FrameworkElement CreateView(IWidgetHost host) => new MusicTextView(host, Kind);
}

/// <summary>
/// [S-14921] One text of what Windows is playing on the text face (<see cref="TextView"/>), read from Windows' own media
/// session through the SDK's <see cref="NowPlayingFeed"/> (so the title, the artist, the album, the buttons, the cover and
/// the progress show the same track at the same moment). Event-driven: it redraws when the session says something changed.
/// Hidden when idle as its config says ([S-1464]).
/// </summary>
internal sealed partial class MusicTextView : TextView
{
    private readonly NowPlayingFeed _feed;
    private readonly string _key;
    private MusicPartConfig _config = new MusicConfig();

    public MusicTextView(IWidgetHost host, TextKind kind)
        : base(host, kind)
    {
        _key = kind.Key;
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
        _config = config as MusicPartConfig ?? new MusicConfig();
        _feed.Reload();
        Render();
    }

    protected override void OnDetach() => _feed.Dispose();

    private void Render()
    {
        var now = _feed.Now;
        Hide(MusicRules.Hides(_config.HideWhenIdle, now.State));
        Present(MusicKinds.Shown(_key, now));
    }
}
