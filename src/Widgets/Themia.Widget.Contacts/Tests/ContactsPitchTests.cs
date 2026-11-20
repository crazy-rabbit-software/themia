using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Contacts.Tests;

/// <summary>[S-12450] [S-12807] The contacts' rows reach their room's top and the foot's inset with their text, at most half again.</summary>
public sealed class ContactsPitchTests
{
    [Fact]
    public void S12450_rows_fill_the_view_touching()
    {
        var config = new ContactsConfig { Layout = ContactsLayout.List, Lines = 2, Rows = 5, SearchBox = true };
        var sizing = ContactsPlan.Sizing(config);

        // The view at the natural box: the card less its header.
        var natural = ContactsPlan.Of(new WidgetArea(300, sizing.Natural.Height - 12), config);
        Assert.Equal((40.0, 5), (natural.Pitch, natural.Rows));

        // [S-14500] A tall view shows the rows it holds at their optimal, each stretched less than one more row's worth.
        var tall = ContactsPlan.Of(new WidgetArea(300, 2000), config);
        Assert.InRange(tall.Pitch, 40, 80);
        Assert.Equal(1972, tall.Rows * tall.Pitch, 6);
        Assert.Equal(40, ContactsPlan.Of(default, config).Pitch);
    }

    [Fact]
    public void S12450_a_compact_row_stretches_the_same_way_and_the_grid_keeps_its_cells()
    {
        var compact = new ContactsConfig { Layout = ContactsLayout.Compact, Rows = 6, SearchBox = false };
        // 192 from the card's top edge: six rows of three cells hold, each 32.
        var rows = ContactsPlan.Of(new WidgetArea(300, (6 * 28) + 12), compact);
        Assert.Equal((32.0, 6), (rows.Pitch, rows.Rows));

        // The grid's lines stretch a little too, its cells' gap kept.
        var grid = new ContactsConfig { Layout = ContactsLayout.Grid };
        Assert.InRange(ContactsPlan.Of(new WidgetArea(300, 2000), grid).Pitch, ContactsPlan.CellHeight + 12, 2 * (ContactsPlan.CellHeight + 12));
    }

    [Fact]
    public void S12450_S14500_the_rows_shown_end_on_the_rooms_end_on_whole_pixels_none_peeking_in_under_them()
    {
        // A card 230 tall at 150 %: the room under the search box holds four rows stretched past their 40. Laid each at the
        // pitch, they rounded a device pixel short apiece and the fifth peeked into the list under them (render of
        // 2026-11-20); each now takes its share on whole device pixels and the rows shown end exactly on the room's end.
        var config = new ContactsConfig { Layout = ContactsLayout.List, Lines = 2, Rows = 5, SearchBox = true };
        var plan = ContactsPlan.Of(new WidgetArea(300, 218), config, 1.5);
        var span = Enumerable.Range(0, plan.Rows).Sum(plan.RowAt);

        Assert.Equal(4, plan.Rows);
        Assert.Equal(plan.Reach.Room, span, 6);
        Assert.Equal(span, plan.Span(plan.Rows), 6);
        Assert.All(Enumerable.Range(0, plan.Rows), i => Assert.Equal(Math.Round(plan.RowAt(i) * 1.5), plan.RowAt(i) * 1.5, 6));
    }
}
