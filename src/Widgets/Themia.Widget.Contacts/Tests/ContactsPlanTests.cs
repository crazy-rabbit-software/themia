using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Contacts.Tests;

/// <summary>[S-1270]..[S-1275] The contacts widget's ladder on the family's rows and cells.</summary>
public sealed class ContactsPlanTests
{
    private static ContactsLayoutPlan Plan(double width, double height, ContactsLayout layout = ContactsLayout.List, bool search = false)
        => ContactsPlan.Of(new WidgetArea(width, height), layout, 3, search);

    [Theory]
    [InlineData(120, 76, 1, false)]   // XS: the name alone
    [InlineData(200, 106, 1, true)]   // S
    [InlineData(480, 116, 1, true)]   // Wide
    [InlineData(320, 196, 2, true)]   // M: the role (else a way to reach)
    [InlineData(200, 356, 2, true)]   // Tall
    [InlineData(560, 376, 3, true)]   // L: and the address
    [InlineData(800, 516, 3, true)]   // XL
    public void S1270_list_rows_add_lines_with_the_tier_and_the_monogram_from_a_column_of_180(double width, double height, int lines, bool avatar)
    {
        var plan = Plan(width, height);

        Assert.Equal(lines, plan.Lines);
        Assert.Equal(FamilyShape.RowHeight(lines), plan.Pitch);
        Assert.Equal(avatar, plan.Avatar);
    }

    [Fact]
    public void S1270_rows_flow_into_columns_of_240_and_whole_rows_fit_under_the_search_box()
    {
        Assert.Equal(1, Plan(320, 196).Columns);
        Assert.Equal(2, Plan(560, 376).Columns);
        Assert.Equal(3, Plan(800, 516).Columns);

        // 196 less the bottom inset (16) and the search box (32 and 8 under it): 140, two rows of 48.
        Assert.Equal(2, Plan(320, 196, search: true).Rows);
        Assert.Equal(3, Plan(320, 196).Rows);
    }

    [Fact]
    public void S1271_compact_rows_are_one_line_with_the_field_as_their_value_from_240()
    {
        Assert.Equal(1, Plan(560, 376, ContactsLayout.Compact).Lines);
        Assert.False(Plan(200, 356, ContactsLayout.Compact).Aside);
        Assert.True(Plan(320, 196, ContactsLayout.Compact).Aside);
        Assert.False(Plan(320, 196, ContactsLayout.Compact).Avatar);
    }

    [Fact]
    public void S582_the_details_table_keeps_a_line_for_its_titles()
    {
        var plan = Plan(560, 376, ContactsLayout.Details);
        Assert.Equal(ContactsLayout.Details, plan.Layout);
        Assert.Equal(10, plan.Rows);   // (360 - 24) / 32

        // Too narrow for its columns: the list.
        Assert.Equal(ContactsLayout.List, Plan(200, 356, ContactsLayout.Details).Layout);
    }

    [Fact]
    public void S1273_grid_cells_are_the_family_cells_12_apart()
    {
        var m = Plan(320, 196, ContactsLayout.Grid);
        Assert.Equal(32, m.Icon);
        Assert.Equal(80, m.ColumnWidth);            // the icon and 24 either side
        Assert.Equal(3, m.Columns);                 // (288 + 12) / 92
        Assert.Equal(3, m.Rows);                    // (180 + 12) / (52 + 12)

        var l = Plan(560, 376, ContactsLayout.Grid);
        Assert.Equal(48, l.Icon);
        Assert.Equal(5, l.Columns);
    }

    [Fact]
    public void S1270_the_lines_under_a_name_are_the_role_then_a_way_to_reach()
    {
        var full = new Contact { Id = "1", Name = "Alex", JobTitle = "Designer", Company = "Lumen", Email = "a@x.example", Phone = "+1" };
        var reach = new Contact { Id = "2", Name = "Carlos", Email = "c@x.example", Phone = "+2" };
        var bare = new Contact { Id = "3", Name = "Ines" };

        Assert.Equal(("Designer · Lumen", "a@x.example"), ContactsPlan.Lines(full, 3));
        Assert.Equal(("Designer · Lumen", null), ContactsPlan.Lines(full, 2));
        Assert.Equal(("c@x.example", "+2"), ContactsPlan.Lines(reach, 3));
        Assert.Equal((null, null), ContactsPlan.Lines(bare, 3));
        Assert.Equal((null, null), ContactsPlan.Lines(full, 1));
    }
}
