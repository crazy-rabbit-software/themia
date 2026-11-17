using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Contacts.Tests;

/// <summary>[S-12450] [S-12410] The contacts' rows run down to the card's edge: the foot's inset is theirs to stretch into, at most half again.</summary>
public sealed class ContactsPitchTests
{
    [Fact]
    public void S12450_rows_stretch_over_the_foot_s_inset_and_never_past_half_again()
    {
        var config = new ContactsConfig { Layout = ContactsLayout.List, Lines = 2, Rows = 5, SearchBox = true };
        var sizing = ContactsPlan.Sizing(config);

        // The view at the natural box: the card less its header.
        var natural = ContactsPlan.Of(new WidgetArea(300, sizing.Natural.Height - 12), config);
        Assert.Equal(42, natural.Pitch);
        Assert.Equal(60, ContactsPlan.Of(new WidgetArea(300, 2000), config).Pitch);
        Assert.Equal(40, ContactsPlan.Of(default, config).Pitch);
    }

    [Fact]
    public void S12450_a_compact_row_stretches_the_same_way_and_the_grid_keeps_its_cells()
    {
        var compact = new ContactsConfig { Layout = ContactsLayout.Compact, Rows = 6, SearchBox = false };
        Assert.Equal(30, ContactsPlan.Of(new WidgetArea(300, (6 * 28) + 12), compact).Pitch);

        var grid = new ContactsConfig { Layout = ContactsLayout.Grid };
        Assert.Equal(ContactsPlan.CellHeight + 12, ContactsPlan.Of(new WidgetArea(300, 2000), grid).Pitch);
    }
}
