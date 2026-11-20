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

        // The grid's cells and lines keep their size, its cells' gap kept.
        var grid = new ContactsConfig { Layout = ContactsLayout.Grid };
        var tall = ContactsPlan.Of(new WidgetArea(300, 2000), grid);
        Assert.Equal(ContactsPlan.CellHeight + 12, tall.Pitch, 6);
        Assert.Equal(ContactsPlan.CellWidth, tall.ColumnWidth, 6);
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
