using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.ScreenTime;

/// <summary>
/// [S-450] [S-454] [S-3310] [S-11928] What every screen-time view of the family holds alike, made and joined in one place:
/// the tracker followed (<see cref="ScreenTimeFeed"/>, no timer of its own: the tracker says when something changed), the day
/// navigation (<see cref="ScreenTimeDayNav"/>), the app links (<see cref="ScreenTimeLinks"/>: what is hovered and selected,
/// kept in step with the view's other screen-time widgets) and the view's dates. <see cref="Attach"/> joins them all and
/// <see cref="Detach"/> leaves them all; while detached a view draws nothing (<see cref="Detached"/>). UI thread.
/// </summary>
internal sealed class ScreenTimeWiring
{
    private readonly IWidgetHost _host;
    private readonly Action _render;

    /// <param name="host">The view's host: its tracker (<see cref="IWidgetHost.ScreenTime"/>).</param>
    /// <param name="render">Draws the view again: the tracker, the language or the day changed.</param>
    /// <param name="focus">Lights what the links point at: another widget hovered or selected an app.</param>
    public ScreenTimeWiring(IWidgetHost host, Action render, Action focus)
    {
        _host = host;
        _render = render;
        Links = new ScreenTimeLinks(focus, LinkTopics.HoveredApp, LinkTopics.SelectedApp);
        Nav = new ScreenTimeDayNav(render);
        Feed = new ScreenTimeFeed(render, Nav);
    }

    /// <summary>[S-3310] The app hovered and selected on the view.</summary>
    public ScreenTimeLinks Links { get; }

    /// <summary>[S-454] The day line and its arrows.</summary>
    public ScreenTimeDayNav Nav { get; }

    /// <summary>[S-450] The tracker followed.</summary>
    public ScreenTimeFeed Feed { get; }

    /// <summary>[S-11924] [S-11928] The view's days: the day selected there followed, or (the chart) the day pointed at.</summary>
    public WidgetLinks Dates { get; } = new();

    /// <summary>Whether the view is off its card: nothing is drawn until it is attached again.</summary>
    public bool Detached { get; private set; }

    /// <summary>
    /// [S-11928] A day selected on the view (a calendar's click) is the day a Day period shows, as far as the arrows reach;
    /// <paramref name="period"/> is the view's period now.
    /// </summary>
    public void FollowSelectedDay(Func<ScreenTimePeriod> period)
        => Dates.Follow(LinkTopics.SelectedDate, value =>
        {
            if (ScreenTimeRules.ClampPeriod(period()) == ScreenTimePeriod.Day && Nav.Select(value?.Date))
            {
                _render();
            }
        });

    /// <summary>[S-450] [S-3300] Follow the tracker and join the view's links: what is selected there already shows at once.</summary>
    public void Attach(IWidgetSurface? surface)
    {
        Detached = false;
        Feed.Attach(_host.ScreenTime);
        Links.Join(surface?.Links);
        Dates.Join(surface?.Links);
    }

    /// <summary>Let go of the tracker and the view's links: this widget's hover goes with it ([S-3304]).</summary>
    public void Detach()
    {
        Detached = true;
        Feed.Detach();
        Links.Leave();
        Dates.Leave();
    }
}
