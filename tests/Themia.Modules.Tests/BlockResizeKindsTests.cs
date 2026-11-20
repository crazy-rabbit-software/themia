using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2460]..[S-2463] Real widgets resized as one block by the selection's frame: every ready-made group, placed as the
/// add page places it, then dragged by its frame's corners and edges. Every member lands on whole cells, in its own
/// sizing (the desktop paints it exactly where it landed, with the config its size means, as a resize lands it), and
/// what touched still touches, what did not overlap still does not.
/// </summary>
public sealed class BlockResizeKindsTests
{
    /// <summary>A plain 1080p display and 2560 × 1440 at 150 % (171 columns of 9.98 DIPs).</summary>
    private static readonly ScreenMetrics[] Screens = [new(1920, 1080), new(2560 / 1.5, 1440 / 1.5)];

    /// <summary>Drags of the frame, in fractions of the screen: out, in, by corners and by edges.</summary>
    private static readonly (ResizeHandle Handle, double Dx, double Dy)[] Drags =
    [
        (ResizeHandle.BottomRight, 0.05, 0.04),
        (ResizeHandle.BottomRight, 0.21, 0.17),
        (ResizeHandle.Right, 0.1, 0),
        (ResizeHandle.Left, 0.03, 0),
        (ResizeHandle.Top, 0, -0.05),
        (ResizeHandle.Bottom, 0, 0.12),
        (ResizeHandle.BottomRight, -0.04, -0.03),
        (ResizeHandle.TopLeft, -0.08, -0.06),
        (ResizeHandle.TopRight, 0.013, -0.011),
    ];

    public static TheoryData<string> Recipes()
    {
        var data = new TheoryData<string>();
        foreach (var recipe in ReadyMadeGroups.Offered())
        {
            data.Add(recipe.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S2460_every_ready_made_group_resized_by_its_frame_lands_on_lines_in_every_members_sizing_joined_as_it_was(string id)
    {
        var recipe = ReadyMadeGroups.Find(id)!;
        foreach (var screen in Screens)
        {
            var (cols, rows) = GridGeometry.GridDims(screen);
            var widgets = PlacementPayload.ForReadyMade(recipe).Plan(0.3, 0.35, screen, new PlacementSizes(1)).Added;
            var start = widgets.Select(w => w.Rect).ToArray();
            var limits = widgets.Select(w => SizeFit.Limits(w, screen, 1)).ToArray();
            var changed = 0;

            foreach (var (handle, dx, dy) in Drags)
            {
                var resized = BlockResize.Resize(start, limits, handle, dx, dy, screen);
                var why = $"{id} on {screen.Width:0}: {handle} by ({dx}, {dy})";
                changed += resized.SequenceEqual(start) ? 0 : 1;

                for (var i = 0; i < widgets.Count; i++)
                {
                    var r = resized[i];
                    Assert.True(Whole(r.X, cols) && Whole(r.Right, cols) && Whole(r.Y, rows) && Whole(r.Bottom, rows), $"{why}: {widgets[i].Kind.Value} off the lines");

                    // As the resize lands it: the config its size means, and the desktop paints it in that config's sizing.
                    var config = SizeFit.ConfigFor(widgets[i].Config, r, screen, 1, limits[i]);
                    var after = SizeFit.Limits(SizeFit.Of(config), screen, 1);
                    Assert.True(r == SizeFit.Shown(r, after, screen), $"{why}: {widgets[i].Kind.Value} is not painted where it landed");

                    for (var j = i + 1; j < widgets.Count; j++)
                    {
                        Assert.True(Touch(start[i], start[j]) == Touch(resized[i], resized[j]), $"{why}: {widgets[i].Kind.Value} and {widgets[j].Kind.Value} touched {Touch(start[i], start[j])}");
                        Assert.True(BlockGeometry.Over(start[i], start[j]) == BlockGeometry.Over(resized[i], resized[j]), $"{why}: {widgets[i].Kind.Value} and {widgets[j].Kind.Value} overlap");
                    }
                }
            }

            // The drags resize real groups: most of them change the block.
            Assert.True(changed >= Drags.Length - 2, $"{id} on {screen.Width:0}: only {changed} of {Drags.Length} drags changed the block");
        }
    }

    private static bool Whole(double frac, int lines) => Math.Abs((frac * lines) - Math.Round(frac * lines)) < 1e-6;

    /// <summary>Two rects meet edge to edge, overlapping along it.</summary>
    private static bool Touch(FracRect a, FracRect b)
    {
        static bool Near(double p, double q) => Math.Abs(p - q) < 1e-9;
        static bool Along(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0) > 1e-9;
        return (Along(a.X, a.Right, b.X, b.Right) && (Near(a.Bottom, b.Y) || Near(b.Bottom, a.Y)))
            || (Along(a.Y, a.Bottom, b.Y, b.Bottom) && (Near(a.Right, b.X) || Near(b.Right, a.X)));
    }
}
