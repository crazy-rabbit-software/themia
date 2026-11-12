using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Email.Tests;

/// <summary>[S-11402] Mail states its rows: each style its own sizes, the height following the rows, their lines and the table's titles.</summary>
public sealed class EmailSizingTests
{
    private static WidgetSizing Sizing(EmailConfig config) => new EmailModule().Descriptor.Sizing(config);

    [Fact]
    public void S11402_a_new_list_is_five_messages_of_two_lines_300_wide()
    {
        var sizing = Sizing(new EmailConfig());
        Assert.Equal(new SizeDip(300, 36 + (5 * 40) + 12), sizing.Natural);
        Assert.Equal(220, sizing.Min.Width);
        Assert.Equal(520, sizing.Max.Width);
        Assert.Empty(sizing.Problems());
    }

    [Theory]
    [InlineData(EmailStyle.List, 3, 3)]
    [InlineData(EmailStyle.Compact, 3, 2)]
    [InlineData(EmailStyle.Table, 3, 1)]
    public void S11402_a_message_has_at_most_the_lines_its_style_draws(EmailStyle style, int chosen, int drawn)
        => Assert.Equal(drawn, new EmailConfig { Style = style, Lines = chosen }.RowsShown.Lines);

    [Fact]
    public void S11402_without_subject_and_account_a_compact_message_is_one_line()
        => Assert.Equal(1, new EmailConfig { Style = EmailStyle.Compact, Lines = 2, ListFields = [] }.RowsShown.Lines);

    [Fact]
    public void S11402_the_table_is_320_to_720_and_its_titles_are_a_setting_not_the_height()
    {
        var with = Sizing(new EmailConfig { Style = EmailStyle.Table });
        var without = Sizing(new EmailConfig { Style = EmailStyle.Table, ColumnTitles = false });

        Assert.Equal((320, 420, 720), (with.Min.Width, with.Natural.Width, with.Max.Width));
        Assert.Equal(KitPlanner.ColumnHeaderHeight, with.Natural.Height - without.Natural.Height);
        Assert.Equal(36 + (5 * 28) + 12, without.Natural.Height);
    }

    [Fact]
    public void S11402_the_table_plans_its_titles_from_the_setting_at_any_height()
    {
        var config = new EmailConfig { Style = EmailStyle.Table, ColumnTitles = true };
        var plan = KitPlanner.Plan(new Themia.Core.Widgets.WidgetArea(420, 60), MailSpec.Spec(EmailStyle.Table), MailSpec.Choices(config));
        Assert.True(plan.ColumnHeader);
        Assert.Equal(1, plan.Lines);
    }

    [Fact]
    public void S11402_dragging_the_bottom_edge_sets_rows_and_a_copy_keeps_them()
    {
        var config = new EmailConfig { Rows = 5 };
        var next = (EmailConfig)new EmailModule().Descriptor.SizeSets!(config, new SizeDip(300, 36 + (3 * 40) + 12));
        Assert.Equal(3, next.Rows);
        Assert.Equal(5, config.Rows);
        Assert.Equal(3, ((EmailConfig)next.Clone()).Rows);
    }

    [Fact]
    public void S11402_rows_and_lines_out_of_range_are_repaired()
    {
        var config = new EmailConfig { Rows = 0, Lines = 9 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal((1, 3), (config.Rows, config.Lines));
    }
}
