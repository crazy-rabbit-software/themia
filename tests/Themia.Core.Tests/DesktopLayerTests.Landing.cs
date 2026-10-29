using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5591] The reconcile lands a dropped file in the view of the display it was dropped on.</summary>
public sealed partial class DesktopLayerTests
{
    private static void NearRect(FracRect expected, FracRect? actual)
    {
        Assert.NotNull(actual);
        var got = actual.Value;
        Assert.True(Math.Abs(expected.X - got.X) < 1e-9 && Math.Abs(expected.Y - got.Y) < 1e-9 && Math.Abs(expected.W - got.W) < 1e-9 && Math.Abs(expected.H - got.H) < 1e-9, $"expected {expected}, got {got}");
    }

    [Fact]
    public void S5591_the_reconcile_lands_a_spotted_arrival_in_its_view_at_its_point()
    {
        var state = TwoViews(out var home, out var work);
        var screen = new ScreenMetrics(1000, 1000) { Allowed = new FracRect(0, 0, 1, 1) };
        var plan = DesktopReconcile.Plan(state, [File("photo.jpg"), File("other.txt")], []);

        // The arrivals come in DesktopItemOrder (other.txt, photo.jpg): photo.jpg was dropped on the display showing Work.
        var unplaced = DesktopReconcile.Apply(
            state, plan, home.Id, screen, (100, 250), Create,
            [null, new DesktopLandingSpot(work.Id, 0.55, 0.3)],
            _ => screen);

        Assert.Empty(unplaced);
        Assert.Equal([Desk + "other.txt"], Keys(home));
        Assert.Equal([Desk + "photo.jpg"], Keys(work));
        NearRect(new FracRect(0.5, 0.25, 0.1, 0.25), Assert.Single(work.Widgets).Rect);
    }
}
