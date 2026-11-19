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
        Assert.Equal(40, natural.Pitch);
        Assert.True(ContactsPlan.Of(new WidgetArea(300, 2000), config).Pitch > 60);
        Assert.Equal(40, ContactsPlan.Of(default, config).Pitch);
    }

    [Fact]
    public void S12450_a_compact_row_stretches_the_same_way_and_the_grid_keeps_its_cells()
    {
        var compact = new ContactsConfig { Layout = ContactsLayout.Compact, Rows = 6, SearchBox = false };
        var rows = ContactsPlan.Of(new WidgetArea(300, (6 * 28) + 12), compact);
        Assert.Equal(28, rows.Pitch);

        var grid = new ContactsConfig { Layout = ContactsLayout.Grid };
        Assert.Equal(ContactsPlan.CellHeight + 12, ContactsPlan.Of(new WidgetArea(300, 2000), grid).Pitch);
    }
}
