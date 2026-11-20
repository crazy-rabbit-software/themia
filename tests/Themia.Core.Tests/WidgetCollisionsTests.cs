using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11959] Widgets kept apart: a moved widget never lands over one that stays.</summary>
[Collection(GlobalLocaleCollection.Name)]
public sealed class WidgetCollisionsTests
{
    /// <summary>1000 DIPs square: 100 grid cells of 10 DIPs a side, one cell 0.01.</summary>
    private static readonly ScreenMetrics Screen = new(1000, 1000);

    private sealed class CardConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new CardConfig();
    }

    private static FracRect R(double x, double y, double w, double h) => new(x, y, w, h);

    private static FracRect Moved(FracRect r, BlockGeometry.Delta d) => BlockGeometry.Apply(r, d, Screen);

    private static WidgetInstance Card(string id, FracRect rect) => new() { Id = id, Config = new CardConfig(), Rect = rect };

    private static WidgetInstance Icon(string id, string key, FracRect rect) => new() { Id = id, Config = new DeskIconFake { Item = key }, Rect = rect };

    /// <summary>The rects of <paramref name="ids"/>, and of every other widget of the view: what moves and what stays.</summary>
    private static (List<FracRect> Moving, List<FracRect> Staying) Split(ViewState view, IReadOnlyCollection<string> ids)
        => ([.. view.Widgets.Where(w => ids.Contains(w.Id)).Select(w => w.Rect)], [.. view.Widgets.Where(w => !ids.Contains(w.Id)).Select(w => w.Rect)]);

    [Fact]
    public void S11959_AMoveIntoClearSpaceGoesAllTheWay()
    {
        var delta = BlockGeometry.Apart([R(0.1, 0.1, 0.1, 0.1)], new(0.2, 0), default, [R(0.6, 0.6, 0.1, 0.1)], Screen);
        Assert.Equal(new BlockGeometry.Delta(0.2, 0), delta);
    }

    [Fact]
    public void S11959_AMoveOntoAnotherWidgetStopsWhereItStood()
    {
        var start = R(0.1, 0.1, 0.1, 0.1);
        var other = R(0.3, 0.1, 0.1, 0.1);
        var delta = BlockGeometry.Apart([start], new(0.15, 0), new(0.05, 0), [other], Screen);
        Assert.Equal(new BlockGeometry.Delta(0.05, 0), delta);
    }

    [Fact]
    public void S11959_TouchingEdgesAreNoOverlap()
    {
        var delta = BlockGeometry.Apart([R(0.1, 0.1, 0.1, 0.1)], new(0.1, 0), default, [R(0.3, 0.1, 0.1, 0.1)], Screen);
        Assert.Equal(new BlockGeometry.Delta(0.1, 0), delta);
    }

    [Fact]
    public void S11959_ABlockedDiagonalSlidesAlongTheClearAxis()
    {
        // A widget to the right: the move right is blocked, the move down is not.
        var start = R(0.1, 0.1, 0.1, 0.1);
        var other = R(0.25, 0.0, 0.1, 0.5);
        var delta = BlockGeometry.Apart([start], new(0.1, 0.2), default, [other], Screen);
        Assert.Equal(new BlockGeometry.Delta(0, 0.2), delta);
    }

    [Fact]
    public void S11959_AWidgetAlreadyOverAnotherMayStayAndLeaveButNotComeOverAThird()
    {
        var start = R(0.1, 0.1, 0.2, 0.2);
        var under = R(0.15, 0.15, 0.1, 0.1);
        var third = R(0.5, 0.1, 0.1, 0.1);

        // Still over the one it started on: allowed.
        Assert.Equal(new BlockGeometry.Delta(0.02, 0), BlockGeometry.Apart([start], new(0.02, 0), default, [under, third], Screen));

        // Onto the third: held.
        Assert.Equal(new BlockGeometry.Delta(0.02, 0), BlockGeometry.Apart([start], new(0.25, 0), new(0.02, 0), [under, third], Screen));
    }

    [Fact]
    public void S11959_EveryWidgetOfABlockIsHeld()
    {
        var block = new[] { R(0.1, 0.1, 0.1, 0.1), R(0.1, 0.5, 0.1, 0.1) };
        var other = R(0.3, 0.5, 0.1, 0.1);
        var delta = BlockGeometry.Apart(block, new(0.2, 0), default, [other], Screen);
        Assert.Equal(default, delta);
        Assert.All(block, r => Assert.False(BlockGeometry.Over(Moved(r, delta), other)));
    }

    [Fact]
    public void S11959_AKeyNudgeOfOneCellOntoAWidgetDoesNotMove()
    {
        // [S-06] The grid is always on: a press is one whole cell. Touching the neighbour, a press towards it stays.
        var start = R(0.1, 0.1, 0.1, 0.1);
        var other = R(0.2, 0.1, 0.1, 0.1);
        var moved = KeyboardNudge.Move([start], NudgeDirection.Right, NudgeStep.Cell, Screen, apart: [other]);
        Assert.Equal(start, moved[0]);

        var free = KeyboardNudge.Move([start], NudgeDirection.Left, NudgeStep.Cell, Screen, apart: [other]);
        Assert.Equal(0.09, free[0].X, 9);

        // Kept apart off (no rects to stay clear of): the same press goes over it, as before.
        Assert.Equal(0.11, KeyboardNudge.Move([start], NudgeDirection.Right, NudgeStep.Cell, Screen)[0].X, 9);
    }

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(1536, 864)]
    public void S11959_a_locked_drag_lands_flush_beside_a_neighbour_on_its_line(double width, double height)
    {
        // [S-11892] A drag lands on the grid's lines at any pitch (1536 DIPs is 154 columns of 9.974): a block dragged
        // 13 cells stands flush beside the neighbour on the line 13 cells over, touching, never into it.
        var screen = new ScreenMetrics(width, height);
        var (cols, rows) = GridGeometry.GridDims(screen);
        var block = R(10.0 / cols, 10.0 / rows, 10.0 / cols, 10.0 / rows);
        var neighbour = R(33.0 / cols, 10.0 / rows, 10.0 / cols, 10.0 / rows);
        var wanted = BlockGeometry.Move([block], 13.2 / cols, 0, screen);
        Assert.Equal(13.0 / cols, wanted.Dx, 12);
        Assert.False(BlockGeometry.Over(Moved(block, wanted), neighbour));
        Assert.Equal(neighbour.X, Moved(block, wanted).Right, 12);
        Assert.Equal(wanted, BlockGeometry.Apart([block], wanted, default, [neighbour], screen));

        // A whole cell into it is an overlap: held.
        var into = BlockGeometry.Move([block], 14.0 / cols, 0, screen);
        Assert.Equal(default, BlockGeometry.Apart([block], into, default, [neighbour], screen));
    }

    [Fact]
    public void S11959_a_locked_mixed_selection_of_an_icon_and_a_card_is_held_as_one_block()
    {
        // [S-11879] The locked desktop's one selection carries icons and cards together: neither may land over a card that stays.
        var view = ViewState.Create("Home");
        view.Widgets.Add(Icon("i", @"C:\d\a.txt", R(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("clock", R(0, 0.2, 0.2, 0.1)));
        view.Widgets.Add(Card("other", R(0.25, 0.2, 0.1, 0.5)));
        string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true);
        var selection = new DesktopSelection();
        selection.SetLayout(view.Widgets.Select((w, k) => new DesktopBox(KeyOf(w)!, 0, k * 10, 0, 5, 5)), _ => null);
        selection.SelectAll();
        selection.Press(DesktopItemKeys.Widget("other"), ctrl: true, shift: false);

        var ids = DesktopPicks.Moving(view, selection, "i", KeyOf);
        Assert.Equal(["i", "clock"], ids);
        var (moving, staying) = Split(view, ids);

        // Right and down: the icon would be clear, the card would come over "other". The block slides down instead.
        var wanted = BlockGeometry.Move(moving, 0.1, 0.2, Screen);
        var delta = BlockGeometry.Apart(moving, wanted, default, staying, Screen);
        Assert.Equal(new BlockGeometry.Delta(0, 0.2), delta);
        Assert.All(moving, r => Assert.All(staying, s => Assert.False(BlockGeometry.Over(Moved(r, delta), s))));
    }

    [Fact]
    public void S11959_a_selection_moves_as_one_block_its_touching_widgets_never_holding_each_other()
    {
        // [S-1584] Several selected widgets move together and touch (one panel): only a widget outside the selection holds them.
        var view = ViewState.Create("Home");
        view.Widgets.Add(Card("a", R(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("b", R(0.1, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("lone", R(0.5, 0, 0.1, 0.1)));

        var ids = SelectionRules.InView(view, ["b", "a"]);
        Assert.Equal(["a", "b"], ids);
        var (moving, staying) = Split(view, ids);

        // Up to the lone card, flush: the whole way.
        Assert.Equal(new BlockGeometry.Delta(0.3, 0), BlockGeometry.Apart(moving, new(0.3, 0), default, staying, Screen));

        // One cell further, b would come over it: the block stays where it stood.
        Assert.Equal(new BlockGeometry.Delta(0.3, 0), BlockGeometry.Apart(moving, new(0.31, 0), new(0.3, 0), staying, Screen));
    }
}
