using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Contacts.Tests;

/// <summary>
/// [S-12450] [S-12807] [S-14810] The contacts' rows stand at their one fixed size from their room's top, touching (owner,
/// 2026-11-20: "the widget items should actually have a fixed size ... the last item will be cutoff to some extent then").
/// </summary>
public sealed class ContactsPitchTests
{
    [Fact]
    public void S12450_S14810_rows_keep_their_size_in_any_card()
    {
        var config = new ContactsConfig { Layout = ContactsLayout.List, Lines = 2, Rows = 5, SearchBox = true };
        var sizing = ContactsPlan.Sizing(config);

        // The view at the natural box: the card less its inset.
        var natural = ContactsPlan.Of(new WidgetArea(300, sizing.Natural.Height - 12), config);
        Assert.Equal((40.0, 5), (natural.Pitch, natural.Rows));

        // A tall view: the rows keep their 40, as many as the room reaches.
        var tall = ContactsPlan.Of(new WidgetArea(300, 2000), config);
        Assert.Equal(40, tall.Pitch);
        Assert.Equal(tall.ListRoom, tall.Reach.Room, 6);
        Assert.Equal((int)Math.Ceiling((tall.Reach.Room - 0.5) / 40), tall.Rows);
        Assert.Equal(40, ContactsPlan.Of(default, config).Pitch);
    }

    [Fact]
    public void S12450_S14810_a_compact_row_and_the_grids_cells_keep_their_size()
    {
        var compact = new ContactsConfig { Layout = ContactsLayout.Compact, Rows = 6, SearchBox = false };
        // 192 from the card's top edge: six rows of three cells whole, a seventh cut.
        var rows = ContactsPlan.Of(new WidgetArea(300, (6 * 28) + 12), compact);
        Assert.Equal((30.0, 7), (rows.Pitch, rows.Rows));

        // The grid's cells and lines keep their size: their content and its padding in whole cells.
        var grid = new ContactsConfig { Layout = ContactsLayout.Grid };
        var tall = ContactsPlan.Of(new WidgetArea(300, 2000), grid);
        Assert.Equal(80, tall.Pitch, 6);
        Assert.Equal(110, tall.ColumnWidth, 6);
    }

    /// <summary>
    /// [S-14817] Owner, 2026-11-21: "the weather forecast widget grid view items do not go from edge to edge, they have space
    /// everywhere. ... fix this, also for other widgets if applicable." The grid is its cells alone from the card's edges:
    /// no inset round them, no gap between them; the columns its width holds whole, the lines its height reaches.
    /// </summary>
    [Fact]
    public void S14817_the_grids_cells_stand_from_the_card_edges_touching()
    {
        var config = new ContactsConfig { Layout = ContactsLayout.Grid, SearchBox = false };
        var sizing = ContactsPlan.Sizing(config);
        Assert.Equal((330.0, 80.0 * config.GridRows), (sizing.Natural.Width, sizing.Natural.Height));
        Assert.Equal(220.0, sizing.Min.Width);

        // 340 wide reaches four cells of 110, the last cut by the right edge; 250 tall (the view 238 under the inset) reaches
        // four lines, the last cut by the foot.
        var plan = ContactsPlan.Of(new WidgetArea(340, 238), config);
        Assert.Equal((4, 110.0, 80.0, 4), (plan.Columns, plan.ColumnWidth, plan.Pitch, plan.Rows));

        // [S-14410] No most across: a 900 card reaches nine cells, the ninth cut, never six and the rest of the line empty.
        Assert.Equal(9, ContactsPlan.Of(new WidgetArea(900, 288), config).Columns);
    }

    [Fact]
    public void S12450_S14810_the_rows_edges_are_whole_pixels_and_the_last_is_cut_by_the_room()
    {
        // A card 230 tall at 150 %: the room under the search box holds four rows of 40 and part of a fifth.
        var config = new ContactsConfig { Layout = ContactsLayout.List, Lines = 2, Rows = 5, SearchBox = true };
        var plan = ContactsPlan.Of(new WidgetArea(300, 218), config, 1.5);

        Assert.Equal(5, plan.Rows);
        Assert.All(Enumerable.Range(0, plan.Rows), i => Assert.Equal(Math.Round(plan.RowAt(i) * 1.5), plan.RowAt(i) * 1.5, 6));
        Assert.True(plan.Span(plan.Rows) > plan.ListRoom);
        Assert.True(plan.Span(plan.Rows - 1) <= plan.ListRoom);

        // [S-13950] Under the search line, which takes the first line, the rows keep their places.
        var whole = ContactsPlan.Of(new WidgetArea(300, 238), new ContactsConfig { Layout = ContactsLayout.List, Lines = 2, Rows = 5, SearchBox = false }, 1.5);
        var open = whole with { Rows = whole.Rows - 1, First = 1 };
        Assert.Equal(open.Reach.Height(0), open.Top, 6);
        Assert.Equal(open.Reach.Height(1), open.RowAt(0), 6);
        Assert.Equal(whole.Reach.Room - open.Top, open.ListRoom, 6);
    }
}
