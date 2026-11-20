using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-14104] A refit keeps joined cards joined (owner, 2026-11-19: "the items have no spacing between them"; [S-2240]): when
/// a card's stepped axis shrinks or grows on the load's fit ([S-14103]) or a config's refit ([S-2447]), the cards that
/// touched its moved edge, and the cards touching those along the same axis, move with it by the same amount. Nothing
/// else moves; a move past the work area or into a card is kept back.
/// </summary>
public sealed class RefitJoinsTests
{
    private const double W = 2560, H = 1440;

    private static readonly SurfaceMetrics Real = new("home", new ScreenMetrics(W, H) { Allowed = new FracRect(0, 0, 1, 1392 / H) }, 1, Settled: true);

    private static WidgetInstance Card(string kind, double x, double y, double w, double h)
        => WidgetInstance.Create(new WidgetId(kind), new FracRect(x / W, y / H, w / W, h / H));

    private static AppState State(params WidgetInstance[] widgets)
    {
        var state = AppState.CreateDefault();
        state.Views.Clear();
        state.Views.Add(new ViewState { Id = "home", Name = "Home", Widgets = [.. widgets] });
        return state;
    }

    private static int Load(AppState state)
    {
        var notes = new SizingNotes();
        notes.NoteAll(state, fit: true);
        return notes.Refit(state, [Real]);
    }

    private static void Dip(double expected, double frac, double screen) => Assert.Equal(expected, frac * screen, 6);

    [Fact]
    public void S14104_a_card_under_a_forecast_fitted_to_its_days_moves_up_with_its_foot_and_still_touches()
    {
        // The forecast stored as the old sizing painted it, 170 for five rows of 30, a clock joined under it.
        var forecast = Card("weatherForecast", 400, 300, 300, 170);
        var clock = Card("clock", 400, 470, 300, 130);
        var beside = Card("clock", 700, 300, 300, 130);
        var state = State(forecast, clock, beside);

        Assert.Equal(2, Load(state));

        Dip(150, forecast.Rect.H, H);
        Dip(450, clock.Rect.Y, H);
        Dip(130, clock.Rect.H, H);
        Assert.Equal(forecast.Rect.Bottom, clock.Rect.Y, 9);

        // Nothing else moves: the card beside the forecast keeps its place.
        Dip(700, beside.Rect.X, W);
        Dip(300, beside.Rect.Y, H);
    }

    [Fact]
    public void S14104_a_card_beside_a_stepped_width_follows_its_right_edge()
    {
        // The week planner's width steps by a day's column of whole cells and follows its days ([S-14410]: three days,
        // 600): stored 640, it is fitted to 600.
        var planner = Card("weekPlanner", 200, 200, 640, 180);
        var clock = Card("clock", 840, 200, 300, 130);
        var state = State(planner, clock);

        Assert.Equal(2, Load(state));

        Dip(600, planner.Rect.W, W);
        Dip(800, clock.Rect.X, W);
        Dip(200, clock.Rect.Y, H);
        Assert.Equal(planner.Rect.Right, clock.Rect.X, 9);
    }

    [Fact]
    public void S14104_a_chain_of_three_stays_joined_when_two_of_them_shrink()
    {
        // A forecast of 170 over a single metric of 40 (its one row: 30, [S-14600]) over a clock.
        var forecast = Card("weatherForecast", 400, 300, 300, 170);
        var metric = Card("cpuSpeed", 400, 470, 300, 40);
        var clock = Card("clock", 400, 510, 300, 130);
        var state = State(forecast, metric, clock);

        Load(state);

        Dip(150, forecast.Rect.H, H);
        Dip(450, metric.Rect.Y, H);
        Dip(30, metric.Rect.H, H);
        Dip(480, clock.Rect.Y, H);
        Assert.Equal(forecast.Rect.Bottom, metric.Rect.Y, 9);
        Assert.Equal(metric.Rect.Bottom, clock.Rect.Y, 9);
    }

    [Fact]
    public void S14104_a_config_that_takes_rows_away_pulls_the_card_under_it_up()
    {
        // Six tasks and the add row (210) over a clock; the rows set to three (120).
        var tasks = Card("tasks", 400, 300, 300, 210);
        var clock = Card("clock", 400, 510, 300, 130);
        var state = State(tasks, clock);
        var notes = new SizingNotes();
        notes.NoteAll(state);
        ((TasksConfig)tasks.Config).Rows = 3;

        Assert.Equal(2, notes.Refit(state, [Real]));

        Dip(120, tasks.Rect.H, H);
        Dip(420, clock.Rect.Y, H);
        Assert.Equal(tasks.Rect.Bottom, clock.Rect.Y, 9);
    }

    [Fact]
    public void S14104_a_move_past_the_work_area_or_into_a_card_is_kept_back()
    {
        // Rows added (three to six: 120 to 210) to tasks whose card under it already stands on the work area's foot.
        var tasks = Card("tasks", 400, 1142, 300, 120);
        var clock = Card("clock", 400, 1262, 300, 130);
        var state = State(tasks, clock);
        ((TasksConfig)tasks.Config).Rows = 3;
        var notes = new SizingNotes();
        notes.NoteAll(state);
        ((TasksConfig)tasks.Config).Rows = 6;
        notes.Refit(state, [Real]);
        Dip(1262, clock.Rect.Y, H);

        // Shrinking a forecast whose follower would land on a card beside the forecast: the follower stays.
        var forecast = Card("weatherForecast", 400, 300, 300, 170);
        var under = Card("clock", 400, 470, 600, 130);
        var right = Card("clock", 700, 300, 300, 160);
        var blocked = State(forecast, under, right);
        Load(blocked);
        Dip(150, forecast.Rect.H, H);
        Dip(470, under.Rect.Y, H);
    }
}
