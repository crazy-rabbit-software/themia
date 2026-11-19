using System.Text.Json;
using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-13970]..[S-13976] The desktop's icon layout (owner, 2026-11-19: "desktop icons should not only have the size settings
/// as they have now, they should also have different types"): text below, text beside and icon only, each with its own cell
/// at every icon size; one setting for the whole desktop; a change re-lays every free icon by the size change's one path and
/// every fence by its one lay; icon only's name shows on hover, focus and rename.
/// </summary>
public sealed class DesktopIconLayoutTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    /// <summary>The owner's display: a 10-DIP grid.</summary>
    private static readonly ScreenMetrics Owner = new(2560, 1440);

    private static readonly DesktopIconLayout[] Layouts = [DesktopIconLayout.Below, DesktopIconLayout.Beside, DesktopIconLayout.IconOnly];

    private static readonly DesktopIconSize[] Sizes = [DesktopIconSize.Small, DesktopIconSize.Medium, DesktopIconSize.Large];

    private sealed class CardConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new CardConfig();
    }

    private static FracRect Dip(double x, double y, double w, double h) => new(x / Owner.Width, y / Owner.Height, w / Owner.Width, h / Owner.Height);

    private static (double X, double Y, double W, double H) InDip(FracRect r)
        => (Math.Round(r.X * Owner.Width, 6), Math.Round(r.Y * Owner.Height, 6), Math.Round(r.W * Owner.Width, 6), Math.Round(r.H * Owner.Height, 6));

    private static bool Overlap(FracRect a, FracRect b)
        => a.X < b.X + b.W - 1e-9 && b.X < a.X + a.W - 1e-9 && a.Y < b.Y + b.H - 1e-9 && b.Y < a.Y + a.H - 1e-9;

    // ---- [S-13971] the cell of each layout at each size -------------------------------------------------------------

    [Theory]
    [InlineData(DesktopIconLayout.Below, DesktopIconSize.Small, 60, 84)]
    [InlineData(DesktopIconLayout.Below, DesktopIconSize.Medium, 76, 100)]
    [InlineData(DesktopIconLayout.Below, DesktopIconSize.Large, 124, 148)]
    [InlineData(DesktopIconLayout.Beside, DesktopIconSize.Small, 160, 40)]
    [InlineData(DesktopIconLayout.Beside, DesktopIconSize.Medium, 200, 56)]
    [InlineData(DesktopIconLayout.Beside, DesktopIconSize.Large, 272, 104)]
    [InlineData(DesktopIconLayout.IconOnly, DesktopIconSize.Small, 48, 48)]
    [InlineData(DesktopIconLayout.IconOnly, DesktopIconSize.Medium, 64, 64)]
    [InlineData(DesktopIconLayout.IconOnly, DesktopIconSize.Large, 112, 112)]
    public void S13971_each_layout_has_its_own_cell_at_each_size(DesktopIconLayout layout, DesktopIconSize size, double width, double height)
    {
        Assert.Equal(new SizeDip(width, height), DesktopCell.Grid(size, layout));
        Assert.Equal(new SizeDip(width, height), DesktopCell.Size(size, layout, 2));
        Assert.Equal(width, DesktopCell.Width(size, layout));
    }

    [Fact]
    public void S13971_the_aspect_is_the_layouts_tall_below_wide_beside_square_alone()
    {
        foreach (var size in Sizes)
        {
            var below = DesktopCell.Grid(size, DesktopIconLayout.Below);
            var beside = DesktopCell.Grid(size, DesktopIconLayout.Beside);
            var alone = DesktopCell.Grid(size, DesktopIconLayout.IconOnly);
            Assert.True(below.Height > below.Width, $"{size} below");
            Assert.True(beside.Width > 2.5 * beside.Height, $"{size} beside");
            Assert.Equal(alone.Width, alone.Height);

            // Icon only is the smallest cell; every cell holds its icon.
            Assert.True(alone.Width * alone.Height < below.Width * below.Height);
            Assert.All(new[] { below, beside, alone }, cell => Assert.True(cell.Width > DesktopCell.Icon(size) && cell.Height > DesktopCell.Icon(size)));
        }
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void S13971_S11875_every_layouts_box_is_whole_grid_cells_never_smaller_than_its_cell(double scale)
    {
        var screen = new ScreenMetrics(2560 / scale, 1440 / scale);
        var (cols, rows) = GridGeometry.GridDims(screen);
        foreach (var layout in Layouts)
        {
            foreach (var size in Sizes)
            {
                var cell = DesktopCell.Grid(size, layout);
                var (w, h) = DesktopGrid.IconSize(cell, screen);
                Assert.Equal(Math.Round(w * cols), w * cols, 6);
                Assert.Equal(Math.Round(h * rows), h * rows, 6);
                Assert.True(w * screen.Width >= cell.Width - 1e-6 && h * screen.Height >= cell.Height - 1e-6, $"{layout} {size} at {scale}");

                // Less than one grid cell more than the cell on each axis.
                Assert.True((w * screen.Width) - cell.Width < screen.Width / cols, $"{layout} {size} at {scale} wide");
                Assert.True((h * screen.Height) - cell.Height < screen.Height / rows, $"{layout} {size} at {scale} tall");
            }
        }

        // On the owner's 10-DIP grid: 80 × 100, 200 × 60 and 70 × 70 at medium.
        Assert.Equal((80.0, 100.0), Round(DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.Below), Owner)));
        Assert.Equal((200.0, 60.0), Round(DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.Beside), Owner)));
        Assert.Equal((70.0, 70.0), Round(DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.IconOnly), Owner)));
    }

    private static (double, double) Round((double W, double H) box) => (Math.Round(box.W * Owner.Width, 6), Math.Round(box.H * Owner.Height, 6));

    [Fact]
    public void S13973_beside_the_icon_the_name_has_what_is_right_of_it_and_never_runs_out_of_the_cell()
    {
        foreach (var size in Sizes)
        {
            var cell = DesktopCell.Grid(size, DesktopIconLayout.Beside);
            var name = DesktopCell.NameWidth(size, DesktopIconLayout.Beside, 0);
            Assert.Equal(DesktopCell.BesideName(size), name);
            Assert.Equal(cell.Width, DesktopCell.BesideInset + DesktopCell.Icon(size) + DesktopCell.BesideGap + name + DesktopCell.BesideInset);

            // Two Caption lines fit beside the icon at every size: the cell is as tall as the taller of them.
            Assert.True(cell.Height >= (2 * Themia.Sdk.Kit.FamilyType.CaptionLine) + (2 * DesktopCell.BesideInset));
            Assert.Equal(cell, DesktopCell.Size(size, DesktopIconLayout.Beside, 1));

            // Drawn wider (its whole grid cells), the name takes the room, never the icon's.
            Assert.Equal(name + 8, DesktopCell.NameWidth(size, DesktopIconLayout.Beside, cell.Width + 8));
        }

        // Below the icon the name is the cell's width less its sides, as before.
        Assert.Equal(72, DesktopCell.NameWidth(DesktopIconSize.Medium, DesktopIconLayout.Below, 0));
    }

    // ---- [S-13974] icon only's name ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, true, true)]
    public void S13974_icon_only_shows_its_name_only_while_pointed_at_focused_or_renamed(bool hovered, bool focused, bool renaming, bool shows)
    {
        Assert.Equal(shows, DesktopCell.ShowsName(DesktopIconLayout.IconOnly, hovered, focused, renaming));

        // Below and beside the icon the name always shows.
        Assert.True(DesktopCell.ShowsName(DesktopIconLayout.Below, hovered, focused, renaming));
        Assert.True(DesktopCell.ShowsName(DesktopIconLayout.Beside, hovered, focused, renaming));
    }

    [Fact]
    public void S13974_icon_onlys_name_stands_past_the_cell_under_the_icon_and_the_tip_always_carries_it()
    {
        // Only icon only's name is drawn past its cell, over the neighbours.
        Assert.Equal([false, false, true], Layouts.Select(DesktopCell.NameOverhangs));

        // The tip leads with the name in icon only, whether or not it is cut; elsewhere only when it is cut.
        Assert.True(DesktopCell.TipNames(DesktopIconLayout.IconOnly, nameCut: false));
        Assert.False(DesktopCell.TipNames(DesktopIconLayout.Below, nameCut: false));
        Assert.False(DesktopCell.TipNames(DesktopIconLayout.Beside, nameCut: false));
        Assert.True(DesktopCell.TipNames(DesktopIconLayout.Beside, nameCut: true));

        foreach (var size in Sizes)
        {
            // Its name starts under the icon (centred in its cell), never over it, inside the cell's own room or just past it.
            var cell = DesktopCell.Grid(size, DesktopIconLayout.IconOnly);
            var top = DesktopCell.HoverNameTop(size, cell.Height);
            var iconBottom = ((cell.Height - DesktopCell.Icon(size)) / 2) + DesktopCell.Icon(size);
            Assert.Equal(iconBottom + DesktopCell.Gap, top);
            Assert.True(top < cell.Height, $"{size}: the name starts in its own cell");

            // In a grid box taller than the cell, it follows the centred icon.
            Assert.Equal(top + 3, DesktopCell.HoverNameTop(size, cell.Height + 6));

            // It wraps at text beside's name width on up to its lines, never the cell's (which has no room for a name).
            Assert.Equal(DesktopCell.BesideName(size), DesktopCell.NameWidth(size, DesktopIconLayout.IconOnly, cell.Width));
            Assert.Equal(DesktopCell.HoverLines, DesktopCell.Fit(size, DesktopIconLayout.IconOnly, 1).Lines);
            Assert.Equal(1, DesktopCell.Fit(size, DesktopIconLayout.Beside, 1).Lines);
            Assert.Equal(2, DesktopCell.Fit(size, DesktopIconLayout.Below, 7).Lines);

            // The cell is square and the same with one name line or two.
            Assert.Equal(cell, DesktopCell.Size(size, DesktopIconLayout.IconOnly, 1));
        }
    }

    // ---- [S-13972] a change re-lays every icon by the one path ------------------------------------------------------

    [Fact]
    public void S13972_a_layout_change_and_back_keeps_the_column_and_the_row_in_order_round_the_widgets()
    {
        var view = ViewState.Create("Home");
        var clock = new WidgetInstance { Id = "clock", Config = new CardConfig(), Rect = Dip(600, 100, 300, 200) };
        view.Widgets.Add(clock);
        var column = Enumerable.Range(0, 6).Select(k => Icon($"c{k}", Dip(10, 10 + (k * 100), 80, 100))).ToList();
        var row = Enumerable.Range(0, 4).Select(k => Icon($"r{k}", Dip(700 + (k * 80), 900, 80, 100))).ToList();
        view.Widgets.AddRange(column);
        view.Widgets.AddRange(row);
        var origin = view.Widgets.Select(w => InDip(w.Rect)).ToList();

        var from = DesktopIconLayout.Below;
        foreach (var to in new[] { DesktopIconLayout.Beside, DesktopIconLayout.IconOnly, DesktopIconLayout.Beside, DesktopIconLayout.Below })
        {
            var box = DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Medium, to), Owner);
            DesktopGrid.Rebox(view, DesktopCell.Grid(DesktopIconSize.Medium, from), DesktopCell.Grid(DesktopIconSize.Medium, to), Owner);

            // Every icon at the new box, none on another, none on the clock, all on the display.
            var icons = column.Concat(row).ToList();
            Assert.All(icons, i => Assert.Equal((box.W, box.H), (i.Rect.W, i.Rect.H)));
            Assert.All(icons, i => Assert.False(Overlap(i.Rect, clock.Rect), $"{from} -> {to}: {InDip(i.Rect)} on the clock"));
            for (var a = 0; a < icons.Count; a++)
            {
                for (var b = a + 1; b < icons.Count; b++)
                {
                    Assert.False(Overlap(icons[a].Rect, icons[b].Rect), $"{from} -> {to}: {InDip(icons[a].Rect)} on {InDip(icons[b].Rect)}");
                }
            }

            // The column is one column in its order, the row one row in its order.
            Assert.All(column, i => Assert.Equal(column[0].Rect.X, i.Rect.X, 9));
            Assert.True(column.Zip(column.Skip(1)).All(p => p.First.Rect.Y < p.Second.Rect.Y));
            Assert.All(row, i => Assert.Equal(row[0].Rect.Y, i.Rect.Y, 9));
            Assert.True(row.Zip(row.Skip(1)).All(p => p.First.Rect.X < p.Second.Rect.X));
            from = to;
        }

        // Back at text below: where they began.
        Assert.Equal(origin, view.Widgets.Select(w => InDip(w.Rect)));

        // The same cell both ways moves nothing.
        var cell = DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.Below);
        Assert.False(DesktopGrid.Rebox(view, cell, cell, Owner));
    }

    [Fact]
    public void S13972_a_layout_and_a_size_change_at_once_are_one_change_of_the_cell()
    {
        var view = ViewState.Create("Home");
        var icons = Enumerable.Range(0, 5).Select(k => Icon($"c{k}", Dip(10, 10 + (k * 100), 80, 100))).ToList();
        view.Widgets.AddRange(icons);

        Assert.True(DesktopGrid.Rebox(view, DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.Below), DesktopCell.Grid(DesktopIconSize.Large, DesktopIconLayout.Beside), Owner));

        var box = DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Large, DesktopIconLayout.Beside), Owner);
        Assert.All(icons, i => Assert.Equal((box.W, box.H), (i.Rect.W, i.Rect.H)));
        Assert.Equal(icons.Select(i => i.Rect.Y).Order(), icons.Select(i => i.Rect.Y));
        Assert.All(icons, i => Assert.Equal(icons[0].Rect.X, i.Rect.X, 9));
    }

    [Theory]
    [InlineData(DesktopIconLayout.Below)]
    [InlineData(DesktopIconLayout.Beside)]
    [InlineData(DesktopIconLayout.IconOnly)]
    public void S13972_S13976_every_fence_lays_its_icons_on_the_new_box_and_the_free_icons_keep_clear_of_it(DesktopIconLayout to)
    {
        var view = ViewState.Create("Home");
        var below = DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.Below), Owner);
        var title = FenceCells.TitleHeight(Owner);
        var fence = new WidgetInstance
        {
            Id = "fence",
            Config = new FenceFake { Items = [.. Enumerable.Range(0, 5).Select(k => Desk + $"f{k}")] },
            Rect = Dip(400, 200, 240, 0) with { H = title + (2 * below.H) },
        };
        view.Widgets.Add(fence);
        var held = Enumerable.Range(0, 5).Select(k => Icon($"f{k}", new FracRect(0, 0, 0, 0))).ToList();
        view.Widgets.AddRange(held);

        // Free icons in a row right under the fence, and laid as text below has them.
        var free = Enumerable.Range(0, 4).Select(k => Icon($"r{k}", Dip(400 + (k * 80), 430, 80, 100))).ToList();
        view.Widgets.AddRange(free);
        DesktopFences.Lay(view, Owner, below);

        DesktopGrid.Rebox(view, DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.Below), DesktopCell.Grid(DesktopIconSize.Medium, to), Owner);
        var box = DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Medium, to), Owner);
        DesktopFences.Lay(view, Owner, box);

        // The fence is whole boxes of the new layout under its title bar, its icons on its cells in its order.
        var config = (FenceFake)fence.Config;
        Assert.Equal(Math.Round(fence.W / box.W), fence.W / box.W, 9);
        Assert.Equal(Math.Round((fence.H - title) / box.H), (fence.H - title) / box.H, 9);
        var layout = FenceCells.Of(fence.Rect, config, Owner, box).Clamped(config.Items.Count);
        for (var i = 0; i < held.Count; i++)
        {
            Assert.Equal((box.W, box.H), (held[i].Rect.W, held[i].Rect.H));
            if (layout.Shows(i))
            {
                Assert.Equal(FenceCells.Cell(layout, i), held[i].Rect);
            }
        }

        // No free icon is under the fence as it is laid now, nor on another.
        Assert.All(free, i => Assert.False(Overlap(i.Rect, fence.Rect), $"{to}: {InDip(i.Rect)} under the fence at {InDip(fence.Rect)}"));
        Assert.True(free.Zip(free.Skip(1)).All(p => p.First.Rect.X < p.Second.Rect.X));
    }

    // ---- [S-13976] a fence's shape follows the box ------------------------------------------------------------------

    [Fact]
    public void S13976_a_fence_of_wide_boxes_is_a_list_and_the_others_stay_near_square()
    {
        var below = DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.Below);
        var beside = DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.Beside);
        var alone = DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.IconOnly);

        // Text below and icon only: as many columns as rows, as before ([S-13008]).
        Assert.Equal(3 * below.Width, FenceGrid.Around(9, below).Width);
        Assert.Equal(3 * alone.Width, FenceGrid.Around(9, alone).Width);
        Assert.Equal(2, FenceGrid.MinAcross(below));
        Assert.Equal(2, FenceGrid.MinAcross(alone));

        // Text beside: fewer columns by the box's proportion, one box across at the least.
        Assert.Equal(2 * beside.Width, FenceGrid.Around(6, beside).Width);
        Assert.Equal(FenceGrid.TitleDip + (3 * beside.Height), FenceGrid.Around(6, beside).Height);
        Assert.Equal(beside.Width, FenceGrid.Around(2, beside).Width);
        Assert.Equal(1, FenceGrid.MinAcross(beside));
    }

    // ---- [S-13970] the one setting ----------------------------------------------------------------------------------

    [Fact]
    public void S13970_a_new_state_has_text_below_the_icons()
    {
        Assert.Equal(DesktopIconLayout.Below, new AppPreferences().DesktopIconLayout);
        Assert.Equal(DesktopIconLayout.Below, AppState.CreateDefault().Preferences.DesktopIconLayout);
    }

    [Fact]
    public void S13970_the_icon_layout_is_copied_and_written_by_name()
    {
        var preferences = new AppPreferences { DesktopIconLayout = DesktopIconLayout.IconOnly };

        Assert.Equal(DesktopIconLayout.IconOnly, preferences.Clone().DesktopIconLayout);

        var json = JsonSerializer.Serialize(preferences, AppStateJsonContext.Default.AppPreferences).Replace(" ", string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"desktopIconLayout\":\"IconOnly\"", json, StringComparison.Ordinal);
        Assert.Equal(DesktopIconLayout.IconOnly, JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppPreferences)!.DesktopIconLayout);
    }

    [Fact]
    public void S13970_a_number_that_names_no_layout_is_repaired_to_text_below_loudly()
    {
        var state = AppState.CreateDefault();
        state.Preferences.DesktopIconLayout = (DesktopIconLayout)42;

        var repairs = AppStateStore.Validate(state);

        Assert.Equal(DesktopIconLayout.Below, state.Preferences.DesktopIconLayout);
        Assert.Contains(repairs, r => r.Contains("desktop icon layout", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(DesktopIconLayout.Below, DesktopMenuCommand.TextBelowIcons)]
    [InlineData(DesktopIconLayout.Beside, DesktopMenuCommand.TextBesideIcons)]
    [InlineData(DesktopIconLayout.IconOnly, DesktopMenuCommand.IconsOnly)]
    public void S13970_view_holds_the_layouts_after_the_sizes_the_one_in_force_with_the_dot(DesktopIconLayout layout, DesktopMenuCommand line)
    {
        var view = DesktopBackground.Menu(new DesktopBackgroundFacts(false, false, IconSize: DesktopIconSize.Small, IconLayout: layout))[0];

        Assert.Equal(
            [DesktopMenuCommand.LargeIcons, DesktopMenuCommand.MediumIcons, DesktopMenuCommand.SmallIcons, null,
             DesktopMenuCommand.TextBelowIcons, DesktopMenuCommand.TextBesideIcons, DesktopMenuCommand.IconsOnly, null,
             DesktopMenuCommand.AutoArrangeIcons],
            view.Children.Select(c => c.Command));

        // One dot in each set: the size in force and the layout in force.
        var chosen = view.Children.Where(c => c.Chosen == true).Select(c => c.Command).ToList();
        Assert.Equal([DesktopMenuCommand.SmallIcons, line], chosen);
        Assert.Equal(layout, DesktopBackground.LayoutOf(line));
        Assert.Null(DesktopBackground.SizeOf(line));
        Assert.Null(DesktopBackground.LayoutOf(DesktopMenuCommand.SmallIcons));
        Assert.Null(DesktopBackground.LayoutOf(DesktopMenuCommand.AutoArrangeIcons));
    }

    [Fact]
    public void S13970_the_menus_facts_read_the_layout_from_the_state()
    {
        var state = AppState.CreateDefault();
        state.Preferences.DesktopIconLayout = DesktopIconLayout.Beside;

        Assert.Equal(DesktopIconLayout.Beside, DesktopBackgroundFacts.Of(state, false, false).IconLayout);
    }

    private static WidgetInstance Icon(string name, FracRect rect) => new() { Id = "icon-" + name, Config = new DeskIconFake { Item = Desk + name }, Rect = rect };
}

/// <summary>
/// [S-13970] The layout in force: the one static of the rule, which the one-argument cell (<see cref="DesktopCell.Grid(DesktopIconSize)"/>)
/// and every view read. Run alone, never beside a test that reads it, and put back.
/// </summary>
[Collection(DesktopCellInForceCollection.Name)]
public sealed class DesktopIconLayoutInForceTests
{
    [Fact]
    public void S13970_the_layout_in_force_is_text_below_until_applied_and_an_unknown_one_is_text_below()
    {
        var before = DesktopCell.CurrentLayout;
        try
        {
            DesktopCell.Apply(DesktopIconLayout.Below);
            Assert.Equal(DesktopCell.Grid(DesktopIconSize.Medium, DesktopIconLayout.Below), DesktopCell.Grid(DesktopIconSize.Medium));

            Assert.True(DesktopCell.Apply(DesktopIconLayout.Beside));
            Assert.Equal(DesktopIconLayout.Beside, DesktopCell.CurrentLayout);
            Assert.False(DesktopCell.Apply(DesktopIconLayout.Beside));

            // The cell in force follows it: the kind's sizing, the host's grid and the fences read this.
            Assert.Equal(DesktopCell.Grid(DesktopIconSize.Large, DesktopIconLayout.Beside), DesktopCell.Grid(DesktopIconSize.Large));
            Assert.Equal(DesktopCell.Size(DesktopIconSize.Small, DesktopIconLayout.Beside, 1), DesktopCell.Size(DesktopIconSize.Small, 1));

            Assert.True(DesktopCell.Apply((DesktopIconLayout)42));
            Assert.Equal(DesktopIconLayout.Below, DesktopCell.CurrentLayout);
        }
        finally
        {
            DesktopCell.Apply(before);
        }
    }
}

/// <summary>Tests that switch the desktop's icon layout in force run after every other, never beside one.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesktopCellInForceCollection
{
    public const string Name = "Desktop cell in force";
}
