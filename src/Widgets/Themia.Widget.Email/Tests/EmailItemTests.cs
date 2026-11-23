using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Email.Tests;

/// <summary>[S-16971] Mail on the item base: a message as an item, its sizes and its config's shape.</summary>
public sealed class EmailItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 42, 0, TimeSpan.Zero);

    private static KitItem Message(bool unread, string? preview = "See the attached deck.")
        => new("m1", "Dana Ruiz", "Quarterly numbers", preview, When: KitWhen.Instant(Now.AddMinutes(-21)), Emphasis: unread, Badges: [new KitBadge("Work")], Tooltip: "Dana");

    [Fact]
    public void S16971_a_message_is_its_sender_over_its_subject_its_age_at_the_end_its_account_the_note()
    {
        var item = MailSpec.Base(Message(unread: true), preview: false, Now, Locale.En);

        Assert.Equal(("Dana Ruiz", "Quarterly numbers", "Work", (Themia.Sdk.Items.ItemLead?)null), (item.Title, item.Subtitle, item.Note, item.Lead));
        Assert.Equal(KitText.When(Locale.En, KitWhen.Instant(Now.AddMinutes(-21)), KitTime.Compact, Now), item.End!.Value);

        // [S-2270] Unread is the name's weight and role alone.
        Assert.Equal((true, false), (item.Strong, item.Muted));
        var read = MailSpec.Base(Message(unread: false), preview: false, Now, Locale.En);
        Assert.Equal((false, true), (read.Strong, read.Muted));
    }

    [Fact]
    public void S16971_the_preview_follows_the_subject_where_it_is_on()
    {
        Assert.Equal("Quarterly numbers – See the attached deck.", MailSpec.Base(Message(true), preview: true, Now, Locale.En).Subtitle);
        Assert.Equal("Quarterly numbers", MailSpec.Base(Message(true, preview: null), preview: true, Now, Locale.En).Subtitle);
    }

    [Fact]
    public void S16971_a_new_list_is_five_messages_and_leaves_the_account_out()
    {
        var config = new EmailConfig();
        Assert.Equal([ItemPart.Note], config.HiddenParts);
        var sizing = new EmailModule().Descriptor.Sizing(config);

        // No lead, no reading: the sender over the subject in the insets, 60 a message.
        Assert.Equal(new SizeDip(300, 5 * 60), sizing.Natural);
        Assert.Empty(sizing.Problems());
        Assert.Equal(30 + (5 * 30), new EmailModule().Descriptor.Sizing(new EmailConfig { Style = ItemStyle.Table }).Natural.Height);
    }

    [Fact]
    public void S16971_a_copy_keeps_everything_and_out_of_range_is_repaired()
    {
        var config = new EmailConfig { Rows = 7, Gap = 2, Style = ItemStyle.Grid, ShowPreview = true, ColumnTitles = false, PartOrder = [ItemPart.End], AccountIds = ["w"] };
        var copy = Assert.IsType<EmailConfig>(config.Clone());
        Assert.Equal((7, 2, ItemStyle.Grid, true, false, ItemPart.End, "w"), (copy.Rows, copy.Gap, copy.Style, copy.ShowPreview, copy.ColumnTitles, copy.PartOrder.Single(), copy.AccountIds!.Single()));

        var wild = new EmailConfig { Rows = 99, Gap = -3 };
        Assert.NotEmpty(wild.Repair());
        Assert.Equal((20, 0), (wild.Rows, wild.Gap));
    }
}
