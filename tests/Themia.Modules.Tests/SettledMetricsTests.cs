using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-854] A surface lays out first at provisional metrics (a 4K display's pixels at scale 1) and only
/// then at its real ones (2560 × 1440 DIPs at 150 %). What writes geometry waits for the real ones: a
/// pass at the provisional metrics followed by the real one stores exactly what the real one alone
/// does. They need the desktop icon kind and a kind with a sizing, so they run here.
/// </summary>
public sealed class SettledMetricsTests
{
    private static readonly FracRect WorkArea = new(0, 0, 1, 1392 / 1440.0);
    private static readonly SurfaceMetrics Provisional = new("home", new ScreenMetrics(3840, 2160) { Allowed = WorkArea }, 1, Settled: false);
    private static readonly SurfaceMetrics Real = new("home", new ScreenMetrics(2560, 1440) { Allowed = WorkArea }, 1, Settled: true);
    private static readonly SizeDip SmallCell = DesktopCell.Grid(DesktopIconSize.Small);

    /// <summary>The owner's Home (2026-11-15): seven small icons in one column 84 DIPs apart, one icon a little off its cell in the next, and a clock.</summary>
    private static AppState Home()
    {
        var state = AppState.CreateDefault();
        state.Views.Clear();
        var widgets = Enumerable.Range(0, 7)
            .Select(k => WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0, k * 84 / 1440.0, 60 / 2560.0, 84 / 1440.0)))
            .ToList();
        widgets.Add(WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(70 / 2560.0, 20 / 1440.0, 60 / 2560.0, 84 / 1440.0)));
        widgets.Add(WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.5, 0.1, 370 / 2560.0, 130 / 1440.0)));
        state.Views.Add(new ViewState { Id = "home", Name = "Home", Widgets = widgets });
        return state;
    }

    private static List<FracRect> Rects(AppState state) => [.. state.Views[0].Widgets.Select(w => w.Rect)];

    private static void Arrange(AppState state, SurfaceMetrics surface)
    {
        foreach (var (widget, rect) in DesktopGrid.Packs(state, [surface], SmallCell))
        {
            widget.Rect = rect;
        }
    }

    [Fact]
    public void S854_icons_arranged_at_provisional_then_real_metrics_end_where_the_real_metrics_alone_put_them()
    {
        var alone = Home();
        Arrange(alone, Real);

        var both = Home();
        Arrange(both, Provisional);
        Assert.Equal(Rects(Home()), Rects(both));
        Arrange(both, Real);

        Assert.Equal(Rects(alone), Rects(both));

        // What the gate stops: the provisional pass taken as real packs the icons on another grid.
        var ungated = Home();
        Arrange(ungated, Provisional with { Settled = true });
        Assert.NotEqual(Rects(alone), Rects(ungated));
    }

    [Fact]
    public void S854_a_refit_waiting_on_provisional_metrics_lands_where_the_real_metrics_alone_put_it()
    {
        static (AppState State, WidgetInstance Tasks, SizingNotes Notes) Changed()
        {
            var state = Home();
            var tasks = WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0.2, 0.2, 300 / 2560.0, (36 + (6 * 28) + 28 + 12) / 1440.0));
            state.Views[0].Widgets.Add(tasks);
            var notes = new SizingNotes();
            notes.NoteAll(state);
            ((TasksConfig)tasks.Config).Rows = 3;
            return (state, tasks, notes);
        }

        var alone = Changed();
        Assert.Equal(1, alone.Notes.Refit(alone.State, [Real]));

        var both = Changed();
        var before = Rects(both.State);
        Assert.Equal(0, both.Notes.Refit(both.State, [Provisional]));
        Assert.Equal(before, Rects(both.State));
        Assert.True(both.Notes.Waits(both.State));

        Assert.Equal(1, both.Notes.Refit(both.State, [Real]));
        Assert.False(both.Notes.Waits(both.State));
        Assert.Equal(Rects(alone.State), Rects(both.State));

        // What the gate stops: refitted at the provisional metrics, the rect is not the real one and never becomes it.
        var ungated = Changed();
        ungated.Notes.Refit(ungated.State, [Provisional with { Settled = true }]);
        ungated.Notes.Refit(ungated.State, [Real]);
        Assert.NotEqual(alone.Tasks.Rect, ungated.Tasks.Rect);
    }

    [Fact]
    public void S14103_a_loaded_card_is_fitted_to_its_steps_once_the_metrics_have_settled()
    {
        // Stored as the old sizing painted it: a forecast of five days 170 tall, two cells under its rows of 30.
        var state = Home();
        var forecast = WidgetInstance.Create(new WidgetId("weatherForecast"), new FracRect(0.2, 0.2, 300 / 2560.0, 170 / 1440.0));
        state.Views[0].Widgets.Add(forecast);
        var notes = new SizingNotes();
        notes.NoteAll(state, fit: true);
        Assert.True(notes.Waits(state));

        // Not on provisional metrics: it waits for the real ones.
        Assert.Equal(0, notes.Refit(state, [Provisional]));
        Assert.Equal(170 / 1440.0, forecast.Rect.H, 9);

        Assert.Equal(1, notes.Refit(state, [Real]));
        Assert.Equal(150 / 1440.0, forecast.Rect.H, 9);
        Assert.Equal(0.2, forecast.Rect.Y, 9);
        Assert.False(notes.Waits(state));

        // Once: a later refit with the same sizing leaves it, and an undo's note fits nothing.
        Assert.Equal(0, notes.Refit(state, [Real]));
        forecast.Rect = forecast.Rect with { H = 170 / 1440.0 };
        notes.NoteAll(state);
        Assert.Equal(0, notes.Refit(state, [Real]));
        Assert.Equal(170 / 1440.0, forecast.Rect.H, 9);
    }

    [Fact]
    public void S2447_a_widget_whose_view_no_display_shows_is_noted_as_it_is()
    {
        var state = Home();
        var tasks = WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0.2, 0.2, 0.1, 0.1));
        state.Views[0].Widgets.Add(tasks);
        var notes = new SizingNotes();
        notes.NoteAll(state);
        ((TasksConfig)tasks.Config).Rows = 3;

        Assert.Equal(0, notes.Refit(state, []));
        Assert.False(notes.Waits(state));
        Assert.Equal(new FracRect(0.2, 0.2, 0.1, 0.1), tasks.Rect);
    }
}
