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
}
