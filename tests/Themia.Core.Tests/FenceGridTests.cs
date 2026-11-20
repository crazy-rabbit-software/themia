using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-13040]..[S-13044] A fence is a region of the desktop icon grid (the owner, 2026-11-19: "the fence should basically just
/// be a helper to align the icon on the underlying engine without this transition in between"): its cells are the grid's,
/// an icon crossing its edge keeps its cell, and the drop cell is one computation for the free desktop and a fence.
/// </summary>
public sealed class FenceGridTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    /// <summary>A screen whose grid cell is no whole DIP (9.97 × 10.05): what most displays are.</summary>
    private static readonly ScreenMetrics Screen = new(1536, 864);

    private static readonly (int Cols, int Rows) Grid = GridGeometry.GridDims(Screen);

    private static readonly (double W, double H) Box = DesktopGrid.IconSize(new SizeDip(76, 100), Screen);

    private static double Title => FenceCells.TitleHeight(Screen);

    private static WidgetInstance Icon(string name, FracRect rect) => new() { Id = "icon-" + name, Config = new DeskIconFake { Item = Desk + name }, Rect = rect };

    private static FracRect Nowhere => new(0, 0, 0, 0);

    private sealed class OtherConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new OtherConfig();
    }

    /// <summary>A fence <paramref name="columns"/> boxes wide and <paramref name="rows"/> high under its title bar, its corner on grid line (<paramref name="col"/>, <paramref name="row"/>), holding <paramref name="names"/> with their icons.</summary>
    private static (ViewState View, WidgetInstance Fence, FenceFake Config) Fenced(int col, int row, int columns, int rows, params string[] names)
    {
        var view = ViewState.Create("Home");
        var config = new FenceFake { Items = [.. names.Select(n => Desk + n)] };
        var fence = new WidgetInstance { Id = "fence", Config = config, Rect = new FracRect(col / (double)Grid.Cols, row / (double)Grid.Rows, columns * Box.W, Title + (rows * Box.H)) };
        view.Widgets.Add(fence);
        view.Widgets.AddRange(names.Select(n => Icon(n, Nowhere)));
        return (view, fence, config);
    }

    private static bool OnGrid(double fraction, int lines) => Math.Abs((fraction * lines) - Math.Round(fraction * lines)) < 1e-6;

    private static void AssertGridCell(FracRect rect)
    {
        Assert.True(OnGrid(rect.X, Grid.Cols) && OnGrid(rect.Y, Grid.Rows), $"{rect} is not on the grid's lines");
        Assert.Equal(Box.W, rect.W, 9);
        Assert.Equal(Box.H, rect.H, 9);
    }

    private static WidgetInstance IconOf(ViewState view, string name) => view.Widgets.Single(w => w.Config is IDesktopItemConfig c && c.Item == Desk + name);

    [Fact]
    public void S13040_a_fences_cells_are_desktop_grid_cells()
    {
        var (view, fence, config) = Fenced(30, 8, 3, 2, "a", "b", "c", "d", "e");

        // Off its lines and a box and a half wide: laid, it is whole grid rows and whole boxes.
        fence.Rect = new FracRect(fence.X + 0.0013, fence.Y + 0.0021, 3.4 * Box.W, Title + (1.6 * Box.H));
        Assert.True(DesktopFences.Lay(view, Screen, Box));

        Assert.True(OnGrid(fence.X, Grid.Cols) && OnGrid(fence.Y, Grid.Rows));
        Assert.Equal(FenceGrid.TitleCells, Title * Grid.Rows, 9);
        Assert.Equal(3, fence.W / Box.W, 9);
        Assert.Equal(2, (fence.H - Title) / Box.H, 9);

        // Each icon on a cell of the grid, box against box under the title bar: the cell a free icon would have there.
        for (var i = 0; i < config.Items.Count; i++)
        {
            var rect = IconOf(view, "abcde"[i].ToString()).Rect;
            AssertGridCell(rect);
            Assert.Equal(fence.X + (i % 3 * Box.W), rect.X, 9);
            Assert.Equal(fence.Y + Title + (i / 3 * Box.H), rect.Y, 9);
            var still = BlockGeometry.Move([rect], 0, 0, Screen);
            Assert.Equal(0, still.Dx, 12);
            Assert.Equal(0, still.Dy, 12);
        }

        // Laid already: a second pass changes nothing.
        Assert.False(DesktopFences.Lay(view, Screen, Box));
    }

    [Fact]
    public void S13041_an_icons_cell_is_the_same_before_and_after_crossing_the_fence_edge()
    {
        var (view, fence, config) = Fenced(40, 10, 3, 2);
        DesktopFences.Lay(view, Screen, Box);
        var first = FenceCells.Cell(FenceCells.Of(fence.Rect, config, Screen, Box), 0);

        // A free icon one box left of the fence's first cell, on the same row of the grid.
        var outside = first with { X = first.X - Box.W };
        var icon = Icon("x", outside);
        view.Widgets.Add(icon);
        var moving = new HashSet<string>([icon.Id], StringComparer.Ordinal);
        Assert.Equal(outside, DesktopFences.DropCell(view, outside, Screen, Box, moving));

        // Moved one box right, the grid's move lands it on the fence's first cell, with the fence's helper or without.
        var plain = new BlockMove([outside], null, Screen);
        var helped = new BlockMove([outside], null, Screen, 0, landing => DesktopFences.DropCell(view, landing, Screen, Box, moving));
        plain.Step(Box.W, 0);
        helped.Step(Box.W, 0);
        Assert.Equal(plain.At(outside, plain.Landing).X, helped.At(outside, helped.Landing).X, 1.0 / Screen.Width);
        Assert.Equal(plain.At(outside, plain.Landing).Y, helped.At(outside, helped.Landing).Y, 1.0 / Screen.Height);
        Assert.Equal(first.X, helped.At(outside, helped.Landing).X, 9);
        Assert.Equal(first.Y, helped.At(outside, helped.Landing).Y, 9);

        // Let go: it is the fence's, and laid it stands exactly where it was dropped. No jump.
        icon.Rect = helped.At(outside, helped.Landing);
        var dropped = icon.Rect;
        Assert.Equal(1, DesktopFences.Land(view, [icon.Id], Screen, Box));
        Assert.Equal([Desk + "x"], config.Items);
        DesktopFences.Lay(view, Screen, Box);
        Assert.Equal(dropped.X, icon.Rect.X, 9);
        Assert.Equal(dropped.Y, icon.Rect.Y, 9);

        // Back out over the edge: a free icon again, on the cell it left, still there after the fences are laid.
        icon.Rect = outside;
        Assert.Equal(1, DesktopFences.Land(view, [icon.Id], Screen, Box));
        Assert.Empty(config.Items);
        DesktopFences.Lay(view, Screen, Box);
        Assert.Equal(outside, icon.Rect);
        Assert.Contains(Desk + "x", DesktopViews.Free(view));
    }

    [Fact]
    public void S13041_the_drop_cell_is_one_function_for_the_desktop_and_a_fence()
    {
        var (view, fence, config) = Fenced(40, 10, 3, 2, "a", "b");
        DesktopFences.Lay(view, Screen, Box);
        var layout = FenceCells.Of(fence.Rect, config, Screen, Box);
        var x = Icon("x", Nowhere);
        view.Widgets.Add(x);
        var moving = new HashSet<string>([x.Id], StringComparer.Ordinal);

        // Away from every fence: the grid's own landing, untouched.
        var away = new FracRect(5 / (double)Grid.Cols, 60 / (double)Grid.Rows, Box.W, Box.H);
        Assert.Equal(away, DesktopFences.DropCell(view, away, Screen, Box, moving));

        // On an item's cell: that cell (the item steps on); past the last item: the next cell it fills, never a gap.
        Assert.Equal(FenceCells.Cell(layout, 1), DesktopFences.DropCell(view, FenceCells.Cell(layout, 1), Screen, Box, moving));
        Assert.Equal(FenceCells.Cell(layout, 2), DesktopFences.DropCell(view, FenceCells.Cell(layout, 4), Screen, Box, moving));

        // Dropped on b's cell it takes b's place; laid, b stands on the next cell.
        x.Rect = DesktopFences.DropCell(view, FenceCells.Cell(layout, 1), Screen, Box, moving);
        DesktopFences.Land(view, [x.Id], Screen, Box);
        Assert.Equal([Desk + "a", Desk + "x", Desk + "b"], config.Items);
        DesktopFences.Lay(view, Screen, Box);
        Assert.Equal(FenceCells.Cell(layout, 1), x.Rect);
        Assert.Equal(FenceCells.Cell(layout, 2), IconOf(view, "b").Rect);

        // One of the fence's own moved inside it: counted among the items that stay.
        var a = IconOf(view, "a");
        var own = new HashSet<string>([a.Id], StringComparer.Ordinal);
        a.Rect = DesktopFences.DropCell(view, FenceCells.Cell(layout, 5), Screen, Box, own);
        Assert.Equal(FenceCells.Cell(layout, 2), a.Rect);
        DesktopFences.Land(view, [a.Id], Screen, Box);
        Assert.Equal([Desk + "x", Desk + "b", Desk + "a"], config.Items);

        // A fence that moves carries its icons: none of them lands anywhere else.
        Assert.Equal(0, DesktopFences.Land(view, [fence.Id, a.Id, x.Id], Screen, Box));
    }

    [Fact]
    public void S13043_scroll_and_roll_up_keep_cells_on_the_grid()
    {
        string[] names = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
        var (view, fence, config) = Fenced(40, 10, 3, 2, names);
        config.Scroll = 1;
        DesktopFences.Lay(view, Screen, Box);
        var top = FenceCells.Cell(FenceCells.Of(fence.Rect, config, Screen, Box) with { Scroll = 0 }, 0);

        // Scrolled a row: the first row it shows stands at the body's top; every icon it shows on a grid cell.
        Assert.Equal(top, IconOf(view, "3").Rect);
        foreach (var name in names[3..9])
        {
            AssertGridCell(IconOf(view, name).Rect);
        }

        var hidden = DesktopFences.Hidden(view, Screen, Box);
        Assert.Equal(["icon-0", "icon-1", "icon-2", "icon-9"], hidden.Order());

        // The icons it does not show wait inside it, taking no room of the free desktop.
        Assert.All(hidden, id => Assert.True(view.Find(id)!.Rect.X >= fence.X - 1e-9 && view.Find(id)!.Rect.Bottom <= fence.Rect.Bottom + 1e-9));

        // Past its last row: kept at the last.
        config.Scroll = 99;
        DesktopFences.Lay(view, Screen, Box);
        Assert.Equal(2, config.Scroll);

        // Rolled up: its title bar's grid rows alone, no icon shown.
        config.RolledUp = true;
        DesktopFences.Lay(view, Screen, Box);
        Assert.Equal(FenceGrid.TitleCells, fence.H * Grid.Rows, 9);
        Assert.Equal(names.Length, DesktopFences.Hidden(view, Screen, Box).Count);
    }

    [Fact]
    public void S13042_an_item_whose_icon_left_the_view_leaves_its_fence()
    {
        var (view, _, config) = Fenced(40, 10, 3, 2, "a", "b");
        view.Widgets.Remove(IconOf(view, "a"));

        DesktopFences.Lay(view, Screen, Box);

        Assert.Equal([Desk + "b"], config.Items);
    }

    [Fact]
    public void S13042_a_fences_icons_are_painted_right_over_it_in_its_order()
    {
        var (view, fence, _) = Fenced(40, 10, 3, 2, "b", "a");
        var free = Icon("free", Nowhere);
        var clock = new WidgetInstance { Id = "clock", Config = new OtherConfig(), Rect = Nowhere };
        view.Widgets.Insert(0, free);
        view.Widgets.Add(clock);

        Assert.Equal(["icon-free", "fence", "icon-b", "icon-a", "clock"], DesktopFences.PaintOrder(view).Select(w => w.Id));
    }

    [Fact]
    public void S13009_a_removed_fence_leaves_the_icons_it_showed_where_they_stand()
    {
        var (view, fence, _) = Fenced(40, 10, 3, 1, "a", "b", "c", "d", "e");
        DesktopFences.Lay(view, Screen, Box);
        var shown = IconOf(view, "b").Rect;

        view.Widgets.Remove(fence);
        var left = DesktopFences.PutBack(view, fence, Screen, Box, (76, 100), key => new DesktopItem(key, key, DesktopItemKind.File),
            (item, rect) => new WidgetInstance { Id = "new-" + item.Key, Config = new DeskIconFake { Item = item.Key }, Rect = rect });

        // The row it showed stays put, free now; the two it had scrolled away are laid at free spots, none on another.
        Assert.Empty(left);
        Assert.Equal(shown, IconOf(view, "b").Rect);
        var icons = view.Widgets.Where(w => w.Config is IDesktopItemConfig).ToList();
        Assert.Equal(5, icons.Count);
        for (var i = 0; i < icons.Count; i++)
        {
            for (var j = i + 1; j < icons.Count; j++)
            {
                Assert.True(DesktopGrid.Free(icons[i].Rect, [icons[j].Rect]), $"{icons[i].Id} on {icons[j].Id}");
            }
        }
    }

    [Fact]
    public void S14501_a_fence_takes_every_cell_its_title_bar_rows_and_its_boxes_from_the_bodys_start()
    {
        // Owner, 2026-11-20: "lets not limit the user in the alignment". A fence of any whole cells: on a display whose cells
        // are no whole DIP (9.974 across, 10.047 down) a resize lands on every cell, its title bar still its three rows and
        // its icons whole boxes from the body's top-left, the sliver a box does not fill at its far edge and foot empty.
        var sizing = WidgetSizing.Range(new SizeDip(152, 128), new SizeDip(228, 228), new SizeDip(456, 456));
        var limits = SizeFit.Limits(sizing, Screen, 1)!;
        var (cols, rows) = GridGeometry.GridDims(Screen);
        var least = (int)Math.Round(limits.Height.Min * rows);
        for (var k = 0; k < 20; k++)
        {
            Assert.Equal((least + k) / (double)rows, limits.Height.Range((least + k) / (double)rows), 9);
        }

        var odd = new FenceGrid.Layout(0, Title, (5.0 / cols) + (2 * Box.W), (3.0 / rows) + Box.H, Box.W, Box.H, 0);
        Assert.Equal((2, 1), (odd.Columns, odd.Rows));
        Assert.Equal((0.0, Title), (odd.Cell(0).X, odd.Cell(0).Y));
    }

    [Fact]
    public void S14263_a_picture_of_a_fence_lays_its_title_bar_and_boxes_in_whole_cells_of_its_card()
    {
        var (title, box) = FenceGrid.OnGrid(new SizeDip(76, 100), ItemGrid.Default());
        Assert.Equal((30.0, 80.0, 100.0), (title, box.Width, box.Height));

        var odd = new ItemGrid(9.974);
        var (oddTitle, oddBox) = FenceGrid.OnGrid(new SizeDip(76, 100), odd);
        Assert.Equal(FenceGrid.TitleCells * odd.Pitch, oddTitle, 9);
        Assert.Equal((8 * odd.Pitch, 11 * odd.Pitch), (oddBox.Width, oddBox.Height));
    }

    [Fact]
    public void S13047_a_fence_and_the_icons_it_holds_never_take_a_move_of_the_desktops_icons_over_as_a_drop_target()
    {
        // A fence holding a folder and an app's shortcut (each takes a drop of files), a free folder beside it, and a card.
        var (view, fence, _) = Fenced(40, 10, 3, 2, "folder", "app");
        DesktopFences.Lay(view, Screen, Box);
        var free = Icon("loose", new FracRect(0.1, 0.1, Box.W, Box.H));
        var card = new WidgetInstance { Id = "card", Config = new OtherConfig(), Rect = new FracRect(0.5, 0.1, 0.1, 0.1) };
        view.Widgets.Add(free);
        view.Widgets.Add(card);

        // The fence and every icon it holds stand for the fence; a free icon and a card for none.
        Assert.Same(fence, DesktopFences.FenceAt(view, fence.Id)?.Widget);
        Assert.Same(fence, DesktopFences.FenceAt(view, IconOf(view, "folder").Id)?.Widget);
        Assert.Same(fence, DesktopFences.FenceAt(view, IconOf(view, "app").Id)?.Widget);
        Assert.Null(DesktopFences.FenceAt(view, free.Id));
        Assert.Null(DesktopFences.FenceAt(view, card.Id));

        // The regression: an icon moved over a fenced folder became the shell's drag (another picture under the pointer)
        // halfway into the fence. Now it stays the move there; over a free folder it is handed over as before.
        bool HandsOver(string id) => DesktopDragOut.Starts(heldIsIcon: true, carriesFiles: true, overDesktop: true,
            overDropTarget: DesktopDragOut.DropTarget(takesDrops: true, fenced: DesktopFences.FenceAt(view, id) is not null));
        Assert.False(HandsOver(IconOf(view, "folder").Id));
        Assert.False(HandsOver(IconOf(view, "app").Id));
        Assert.False(HandsOver(fence.Id));
        Assert.True(HandsOver(free.Id));
        Assert.True(HandsOver(card.Id));

        // Off the desktop it is the shell's drag wherever it went; a widget that takes no files is no target.
        Assert.False(DesktopDragOut.DropTarget(takesDrops: false, fenced: false));
        Assert.True(DesktopDragOut.Starts(heldIsIcon: true, carriesFiles: true, overDesktop: false, overDropTarget: false));
    }
}
