using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Widget.Contacts;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-11406] The contacts widget's plan and sizes from its config, each layout its own.</summary>
public sealed class ContactsPlanTests
{
    private static ContactsLayoutPlan Plan(ContactsConfig config, double width = 300) => ContactsPlan.Of(new WidgetArea(width, 400), config);

    [Fact]
    public void S11406_a_new_list_is_five_people_of_two_lines_under_the_search_box()
    {
        var config = new ContactsConfig();
        var plan = Plan(config);
        var sizing = ContactsPlan.Sizing(config);

        Assert.Equal((ContactsLayout.List, 1, 2, true, 5), (plan.Layout, plan.Columns, plan.Lines, plan.Avatar, plan.Rows));
        Assert.Equal(new Themia.Sdk.SizeDip(300, 36 + ContactsPlan.SearchHeight + (5 * 40) + 12), sizing.Natural);
        Assert.Equal((220, 520), (sizing.Min.Width, sizing.Max.Width));
        Assert.Empty(sizing.Problems());
    }

    [Fact]
    public void S11406_the_plan_is_the_same_at_any_width()
    {
        var config = new ContactsConfig { Lines = 3 };
        Assert.Equal(Plan(config, 220) with { ColumnWidth = 0 }, Plan(config, 520) with { ColumnWidth = 0 });
        Assert.Equal(3, Plan(config).Lines);
    }

    [Fact]
    public void S11406_without_the_box_the_list_is_shorter_by_it()
        => Assert.Equal(ContactsPlan.SearchHeight, ContactsPlan.Sizing(new ContactsConfig()).Natural.Height - ContactsPlan.Sizing(new ContactsConfig { SearchBox = false }).Natural.Height);

    [Fact]
    public void S11406_compact_is_one_line_with_its_field_and_details_has_its_titles_and_column_widths()
    {
        var compact = Plan(new ContactsConfig { Layout = ContactsLayout.Compact });
        Assert.Equal((1, true, false), (compact.Lines, compact.Aside, compact.Avatar));

        var details = new ContactsConfig { Layout = ContactsLayout.Details, SearchBox = false };
        var sizing = ContactsPlan.Sizing(details);
        Assert.Equal(36 + ContactsPlan.HeaderHeight + (5 * 28) + 12, sizing.Natural.Height);
        Assert.Equal(24 + (details.EffectiveColumns.Count * 88), sizing.Min.Width);
        Assert.Equal(ContactsLayout.Details, Plan(details, 200).Layout);
    }

    [Fact]
    public void S11406_the_grid_is_one_size_its_cells_and_lines_the_config_s()
    {
        var config = new ContactsConfig { Layout = ContactsLayout.Grid, SearchBox = false, GridColumns = 3, GridRows = 2 };
        var sizing = ContactsPlan.Sizing(config);
        var plan = Plan(config);

        Assert.True(sizing.FixedWidth && sizing.FixedHeight);
        Assert.Equal(new Themia.Sdk.SizeDip(24 + (3 * 96) + (2 * 12), 36 + (2 * ContactsPlan.CellHeight) + 12 + 12), sizing.Natural);
        Assert.Equal((48.0, 3, 2), (plan.Icon, plan.Columns, plan.Rows));
        Assert.Same(config, ContactsPlan.SizeSets(config, new Themia.Sdk.SizeDip(400, 400)));
    }

    [Fact]
    public void S11406_dragging_sets_rows_and_out_of_range_is_repaired()
    {
        var next = (ContactsConfig)ContactsPlan.SizeSets(new ContactsConfig { SearchBox = false }, new Themia.Sdk.SizeDip(300, 36 + (8 * 40) + 12));
        Assert.Equal(8, next.Rows);

        var wild = new ContactsConfig { Rows = 0, Lines = 9, GridColumns = 1, GridRows = 30 };
        Assert.NotEmpty(wild.Repair());
        Assert.Equal((1, 3, 2, 6), (wild.Rows, wild.Lines, wild.GridColumns, wild.GridRows));
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
