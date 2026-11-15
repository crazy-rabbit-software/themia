using Themia.Core.Geometry;
using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11959] Widgets kept apart: a moved widget never lands over one that stays.</summary>
public sealed class WidgetCollisionsTests
{
    private static readonly ScreenMetrics Screen = new(1000, 1000);

    private static FracRect R(double x, double y, double w, double h) => new(x, y, w, h);

    private static FracRect Moved(FracRect r, BlockGeometry.Delta d) => BlockGeometry.Apply(r, d, Screen);

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
    public void S11959_AKeyNudgeOntoAWidgetDoesNotMove()
    {
        var start = R(0.1, 0.1, 0.1, 0.1);
        var other = R(0.2, 0.1, 0.1, 0.1);
        var moved = KeyboardNudge.Move([start], NudgeDirection.Right, NudgeStep.Fine, Screen, snapEnabled: false, apart: [other]);
        Assert.Equal(start, moved[0]);

        var free = KeyboardNudge.Move([start], NudgeDirection.Left, NudgeStep.Fine, Screen, snapEnabled: false, apart: [other]);
        Assert.True(free[0].X < start.X);
    }
}
