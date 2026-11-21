using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-15666] A view's fences looked up once answer what walking the view answered; [S-15667] the room looked up by cell
/// answers what the list of rects answered; and the collect's stamp moves with what a collect reads, never with a move.
/// </summary>
public sealed class FenceIndexTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private static readonly ScreenMetrics Screen = new(1536, 864);

    private static WidgetInstance Icon(string name, double x = 0.1, double y = 0.1) => new() { Id = "icon-" + name, Config = new DeskIconFake { Item = Desk + name }, Rect = new FracRect(x, y, 0.05, 0.08) };

    private static WidgetInstance Fence(string id, FenceSource source, params string[] names)
        => new() { Id = id, Config = new FenceFake { Source = source, Items = [.. names.Select(n => Desk + n)], Rules = new FenceRuleSet { Types = [FenceFileType.Images] } }, Rect = new FracRect(0.5, 0.1, 0.3, 0.3) };

    private sealed class CardFake : WidgetConfig
    {
        public override WidgetConfig Clone() => new CardFake();
    }

    private static ViewState Desktop()
    {
        var view = ViewState.Create("Home");
        view.Widgets.AddRange([
            Fence("first", FenceSource.Held, "a", "b", "gone"),
            Icon("a"), Icon("b"), Icon("c"), Icon("d"),
            new WidgetInstance { Id = "card", Config = new CardFake(), Rect = new FracRect(0.3, 0.3, 0.1, 0.1) },

            // An item in two fences stays the first's; a second icon of one item is not the one the view keeps.
            Fence("second", FenceSource.Desktop, "b", "c"),
            new WidgetInstance { Id = "icon-c-twice", Config = new DeskIconFake { Item = Desk + "c" }, Rect = new FracRect(0.2, 0.2, 0.05, 0.08) },
        ]);
        return view;
    }

    [Fact]
    public void S15666_the_fences_looked_up_once_answer_as_the_view_walked()
    {
        var view = Desktop();
        var index = FenceIndex.Of(view);

        Assert.Equal(["first", "second"], index.Fences.Select(f => f.Widget.Id));
        Assert.Equal("first", index.FenceAt("first")?.Widget.Id);
        Assert.Equal("first", index.FenceAt("icon-a")?.Widget.Id);
        Assert.Equal("first", index.FenceAt("icon-b")?.Widget.Id);
        Assert.Equal("second", index.FenceAt("icon-c")?.Widget.Id);
        Assert.Null(index.FenceAt("icon-c-twice"));
        Assert.Null(index.FenceAt("icon-d"));
        Assert.Null(index.FenceAt("card"));

        Assert.Equal("first", index.Holding(Desk + "b")?.Widget.Id);
        Assert.Equal("first", index.Holding(Desk + "gone")?.Widget.Id);
        Assert.Null(index.Holding(Desk + "d"));
        Assert.Equal("icon-c", index.Icon(Desk + "c")?.Id);

        Assert.Equal(new Dictionary<string, (string, int)> { ["icon-a"] = ("first", 0), ["icon-b"] = ("first", 1), ["icon-c"] = ("second", 1) },
            index.Members.ToDictionary(m => m.Key, m => (m.Value.Fence.Widget.Id, m.Value.Index)));
        Assert.Equal(["icon-d", "icon-c-twice"], index.FreeIcons().Select(w => w.Id));

        // The view-walking rules answer the same.
        foreach (var widget in view.Widgets)
        {
            Assert.Equal(index.FenceAt(widget.Id)?.Widget.Id, DesktopFences.FenceAt(view, widget.Id)?.Widget.Id);
            if (widget.Config is IDesktopItemConfig item)
            {
                Assert.Equal(DesktopFences.Holding(view, item.Item)?.Widget.Id, index.Holding(item.Item)?.Widget.Id);
            }
        }

        Assert.Equal(DesktopFences.PaintOrder(view).Select(w => w.Id), DesktopFences.PaintOrder(index).Select(w => w.Id));
    }

    [Fact]
    public void S15667_the_room_looked_up_by_cell_blocks_as_the_list_of_rects_does()
    {
        var random = new Random(15667);
        var (cols, rows) = GridGeometry.GridDims(Screen);
        for (var round = 0; round < 40; round++)
        {
            // Widgets on and off the grid's lines, past the display's edges, touching, and one with no size.
            var rects = new List<FracRect>();
            for (var i = 0; i < 30; i++)
            {
                var x = (random.NextDouble() * 1.2) - 0.1;
                var y = (random.NextDouble() * 1.2) - 0.1;
                var onLines = random.Next(2) == 0;
                rects.Add(onLines
                    ? new FracRect(Math.Round(x * cols) / cols, Math.Round(y * rows) / rows, random.Next(1, 20) / (double)cols, random.Next(1, 20) / (double)rows)
                    : new FracRect(x, y, random.NextDouble() * 0.3, random.NextDouble() * 0.3));
            }

            rects.Add(new FracRect(0.5, 0.5, 0, 0));
            rects.Add(new FracRect(double.NaN, 0.5, 0.1, 0.1));
            var room = Occupancy.Of(rects, Screen);

            for (var probe = 0; probe < 400; probe++)
            {
                var box = new FracRect(random.Next(-2, cols + 2) / (double)cols, random.Next(-2, rows + 2) / (double)rows, random.Next(1, 12) / (double)cols, random.Next(1, 12) / (double)rows);
                Assert.Equal(Linear(box, rects), room.Blocker(box));
            }
        }
    }

    [Fact]
    public void S15667_free_spots_found_over_the_room_by_cell_are_the_ones_the_list_finds()
    {
        var size = DesktopGrid.IconSize(new SizeDip(76, 100), Screen);
        var occupied = new List<FracRect> { new(0, 0, 0.6, 0.6), new(0.7, 0.2, 0.2, 0.7) };
        var room = Occupancy.Of(occupied, Screen);

        Assert.Equal(DesktopGrid.FirstFree(size, occupied, Screen), DesktopGrid.FirstFree(size, room, Screen));
        Assert.Equal(DesktopGrid.NearestFree(size, 0.3, 0.3, occupied, Screen), DesktopGrid.NearestFree(size, 0.3, 0.3, room, Screen));

        // An arrival takes its spot in the room: the next one lands elsewhere.
        var first = DesktopGrid.Arrive(room, size, 0.3, 0.3, Screen);
        var second = DesktopGrid.Arrive(room, size, 0.3, 0.3, Screen);
        Assert.NotNull(first);
        Assert.NotEqual(first, second);
        Assert.Null(room.Blocker(new FracRect(0.95, 0.95, 0.01, 0.01)));
        Assert.Equal(first, room.Blocker(first!.Value));
    }

    [Fact]
    public void S15666_the_collects_stamp_moves_with_the_fences_rules_and_items_never_with_a_move()
    {
        var view = Desktop();
        var state = new AppState { Views = [view], ActiveViewId = view.Id };
        var stamp = DesktopFences.CollectStamp(state);
        Assert.NotNull(stamp);

        // A widget or an icon moved, a card's config: nothing a collect reads.
        view.Find("card")!.X = 0.4;
        view.Find("icon-d")!.Y = 0.6;
        Assert.Equal(stamp, DesktopFences.CollectStamp(state));

        // An item taken out by hand, the rules, the source and the fences' order: what a collect reads.
        var second = (FenceFake)view.Find("second")!.Config;
        second.Items.Remove(Desk + "c");
        var taken = DesktopFences.CollectStamp(state);
        Assert.NotEqual(stamp, taken);

        second.Rules = new FenceRuleSet { Types = [FenceFileType.Documents] };
        var ruled = DesktopFences.CollectStamp(state);
        Assert.NotEqual(taken, ruled);

        var fence = view.Find("second")!;
        view.Widgets.Remove(fence);
        view.Widgets.Insert(0, fence);
        Assert.NotEqual(ruled, DesktopFences.CollectStamp(state));

        // No collecting or search fence: nothing to collect.
        second.Source = FenceSource.Held;
        Assert.Null(DesktopFences.CollectStamp(state));
    }

    private static FracRect? Linear(FracRect rect, List<FracRect> occupied)
    {
        foreach (var other in occupied)
        {
            if (other.IsFinite
                && rect.X < other.Right - 1e-9 && other.X < rect.Right - 1e-9
                && rect.Y < other.Bottom - 1e-9 && other.Y < rect.Bottom - 1e-9)
            {
                return other;
            }
        }

        return null;
    }
}
