using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-14104] A refit keeps joined cards joined (owner, 2026-11-19: "the items have no spacing between them"; [S-2240]): when
/// a card's axis shrinks or grows on a config's refit ([S-2447]; a list's rows put it at its natural, [S-14503]), the cards
/// that touched its moved edge, and the cards touching those along the same axis, move with it by the same amount.
/// Nothing else moves; a move past the work area or into a card is kept back.
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

    /// <summary>Note the state, set a list's rows, refit: what a Rows line on its page does.</summary>
    private static int Rows(AppState state, WidgetInstance tasks, int rows)
    {
        var notes = new SizingNotes();
        notes.NoteAll(state);
        ((TasksConfig)tasks.Config).Rows = rows;
        return notes.Refit(state, [Real]);
    }

    private static void Dip(double expected, double frac, double screen) => Assert.Equal(expected, frac * screen, 6);

    [Fact]
    public void S14104_S14503_a_card_under_a_list_given_more_rows_moves_down_with_its_foot_and_still_touches()
    {
        // Three tasks and the add row (120) over a clock, a clock beside; the rows set to six: the list stands at its new
        // natural (210).
        var tasks = Card("tasks", 400, 300, 300, 120);
        var clock = Card("clock", 400, 420, 300, 130);
        var beside = Card("clock", 700, 300, 300, 130);
        var state = State(tasks, clock, beside);
        ((TasksConfig)tasks.Config).Rows = 3;

        Assert.Equal(2, Rows(state, tasks, 6));

        Dip(210, tasks.Rect.H, H);
        Dip(510, clock.Rect.Y, H);
        Dip(130, clock.Rect.H, H);
        Assert.Equal(tasks.Rect.Bottom, clock.Rect.Y, 9);

        // Nothing else moves: the card beside the list keeps its place.
        Dip(700, beside.Rect.X, W);
        Dip(300, beside.Rect.Y, H);
    }

    [Fact]
    public void S14104_a_chain_of_three_stays_joined_when_the_first_shrinks()
    {
        // Six tasks (210) over a metric over a clock; the rows set to three (120): both move up 90 and still touch.
        var tasks = Card("tasks", 400, 300, 300, 210);
        var metric = Card("cpuSpeed", 400, 510, 300, 30);
        var clock = Card("clock", 400, 540, 300, 130);
        var state = State(tasks, metric, clock);

        Rows(state, tasks, 3);

        Dip(120, tasks.Rect.H, H);
        Dip(420, metric.Rect.Y, H);
        Dip(30, metric.Rect.H, H);
        Dip(450, clock.Rect.Y, H);
        Assert.Equal(tasks.Rect.Bottom, metric.Rect.Y, 9);
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

        // Shrinking a list whose follower would land on a card beside the list: the follower stays.
        var list = Card("tasks", 400, 300, 300, 210);
        var under = Card("clock", 400, 510, 600, 130);
        var right = Card("clock", 700, 300, 300, 200);
        var blocked = State(list, under, right);
        Rows(blocked, list, 3);
        Dip(120, list.Rect.H, H);
        Dip(510, under.Rect.Y, H);
    }
}
