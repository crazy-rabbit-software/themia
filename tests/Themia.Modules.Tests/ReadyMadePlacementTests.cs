using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2471] [S-1065] Every ready-made group the add page offers can be added: placed through the tile's
/// own path (the placement payload's plan, landed into a view as the host lands it), it arrives whole, as
/// one group of all its widgets, clean to load validation, and its page is the group's.
/// </summary>
public sealed class ReadyMadePlacementTests
{
    /// <summary>The displays the groups are placed on: a 1080p screen, 1440p at 150 % (in DIPs), the 1280 × 720 floor.</summary>
    private static readonly ScreenMetrics[] Screens = [new(1920, 1080), new(2560 / 1.5, 1440 / 1.5), new(1280, 720)];

    public static TheoryData<string, int, bool> Placements()
    {
        var data = new TheoryData<string, int, bool>();
        foreach (var recipe in ReadyMadeGroups.Offered())
        {
            for (var s = 0; s < Screens.Length; s++)
            {
                data.Add(recipe.Id, s, false);
                data.Add(recipe.Id, s, true);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Placements))]
    public void S2471_every_offered_ready_made_group_lands_whole_as_loose_widgets(string id, int screenAt, bool gridSizes)
    {
        var recipe = ReadyMadeGroups.Find(id)!;
        var screen = Screens[screenAt];
        var state = new AppState { Views = [ViewState.Create("v")] };
        state.ActiveViewId = state.Views[0].Id;
        var payload = PlacementPayload.ForReadyMade(recipe);

        // [S-1065] The licence's gate sees every widget it creates, before the ghost and on landing.
        Assert.Equal(recipe.Widgets.Count, payload.Creates);

        var plan = payload.Plan(0.5, 0.5, screen, gridSizes ? new PlacementSizes(1) : null);
        Assert.Equal(recipe.Widgets.Count, plan.NetAdded(state));

        state.ApplyPlacement(plan, state.Views[0].Id);

        // [S-2475] Loose widgets like any others, every one of them what the placement selects.
        var view = state.Views[0];
        // [S-16682] Each its kind, or its preset of a kind.
        Assert.Equal(recipe.Widgets.Select(w => w.Kind), view.Widgets.Select(w => Themia.Sdk.WidgetPick.Of(w.Config).Key));
        Assert.Equal(view.Widgets.Select(w => w.Id), plan.Roots);
        Assert.Empty(StateRepair.Repair(state));
        Assert.All(view.Widgets, w => Assert.True(w.Rect.IsFinite && w.Rect.W > 0 && w.Rect.H > 0, $"{id}: {w.Kind.Value} has no box"));
        var written = Themia.Core.Storage.SettingsTransfer.Serialize(state);
        Assert.DoesNotContain("\"group\":", written, StringComparison.Ordinal);
        Assert.DoesNotContain("\"groups\":", written, StringComparison.Ordinal);

        // Selected together, their page is several widgets': what they share and their look.
        var selection = new WidgetSelection();
        selection.Replace(view.Id, plan.Roots);
        var page = SelectionPage.Of(state, selection, view.Id);
        Assert.NotNull(page);
        Assert.Equal(SelectionKind.Several, page.Kind);
    }

    /// <summary>The content scales a display places with.</summary>
    private static readonly double[] Sizes = [1, 1.25, 1.5, 2];

    /// <summary>The device pixels per DIP the cells are drawn at.</summary>
    private static readonly double[] Rasters = [1, 1.25, 1.5, 2];

    public static TheoryData<string> Recipes()
    {
        var data = new TheoryData<string>();
        foreach (var recipe in ReadyMadeGroups.Offered())
        {
            data.Add(recipe.Id);
        }

        return data;
    }

    /// <summary>
    /// [S-11809] [S-2248] [S-2249] Regression (owner, 2026-11-15: "the groups that get placed as
    /// combinations they dont actually touch / connect to each other anymore"): on every display, at
    /// every content scale, on the grid, every pair of widgets the recipe puts side by side
    /// lands as neighbours (their cells meet on one device pixel at every raster scale) and is drawn
    /// joined (one panel: no line, square inner corners); none overlaps another; and
    /// the desktop paints each exactly where it landed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Recipes))]
    public void S11809_every_ready_made_group_lands_with_its_neighbours_meeting_and_joined(string id)
    {
        var recipe = ReadyMadeGroups.Find(id)!;
        var neighbours = new List<(int A, int B)>();
        for (var i = 0; i < recipe.Widgets.Count; i++)
        {
            for (var j = i + 1; j < recipe.Widgets.Count; j++)
            {
                var (a, b) = (recipe.Widgets[i].Box, recipe.Widgets[j].Box);
                if (CardJoins.Meeting(new DipRect(a.X, a.Y, a.Width, a.Height), new DipRect(b.X, b.Y, b.Width, b.Height), 1) is not null)
                {
                    neighbours.Add((i, j));
                }
            }
        }

        Assert.NotEmpty(neighbours);
        foreach (var screen in Screens)
        {
            foreach (var scale in Sizes)
            {
                var plan = PlacementPayload.ForReadyMade(recipe).Plan(0.4137, 0.3719, screen, new PlacementSizes(scale));
                var what = $"{id} on {screen.Width:0.#}x{screen.Height:0.#}, scale {scale}";

                // [S-2474] A recipe taller or wider than the work area at this content scale (This PC's one column at
                // 2× on a 960-DIP display) cannot stand whole on it: each card lands in reach instead ([S-09]).
                var (tall, wide) = (recipe.Bounds.Height * scale, recipe.Bounds.Width * scale);
                if (tall > screen.Allowed.H * screen.Height || wide > screen.Allowed.W * screen.Width)
                {
                    Assert.All(plan.Added, w => Assert.Equal(w.Rect, GridGeometry.Clamp(w.Rect, screen)));
                    continue;
                }

                foreach (var w in plan.Added)
                {
                    // Within float noise: the painted box recomputes its whole cells.
                    var shown = SizeFit.Shown(w.Rect, SizeFit.Limits(w, screen, scale), screen);
                    Assert.True(Math.Abs(shown.X - w.Rect.X) + Math.Abs(shown.Y - w.Rect.Y) + Math.Abs(shown.W - w.Rect.W) + Math.Abs(shown.H - w.Rect.H) < 1e-9, $"{what}: {w.Kind.Value} is painted at {shown}, not where it landed ({w.Rect})");
                }

                foreach (var raster in Rasters)
                {
                    var cells = plan.Added.Select(w => GridGeometry.PaintedCell(w.Rect, screen, raster)).ToArray();
                    var outlines = CardJoins.Paint(cells, [.. cells.Select(_ => (int?)0)], raster);
                    foreach (var (a, b) in neighbours)
                    {
                        var (ka, kb) = (plan.Added[a].Kind.Value, plan.Added[b].Kind.Value);
                        Assert.True(CardJoins.Meeting(cells[a], cells[b], raster) is not null, $"{what} at {raster}: {ka} and {kb} no longer meet ({cells[a]} / {cells[b]})");
                        Assert.True(outlines[a] != CardOutline.Free && outlines[b] != CardOutline.Free, $"{what} at {raster}: {ka} and {kb} are not joined");
                    }

                    for (var i = 0; i < cells.Length; i++)
                    {
                        for (var j = i + 1; j < cells.Length; j++)
                        {
                            var (a, b) = (cells[i], cells[j]);
                            var overlapW = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
                            var overlapH = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
                            Assert.False(overlapW > 1e-6 && overlapH > 1e-6, $"{what} at {raster}: {plan.Added[i].Kind.Value} overlaps {plan.Added[j].Kind.Value}");
                        }
                    }
                }
            }
        }
    }
}
