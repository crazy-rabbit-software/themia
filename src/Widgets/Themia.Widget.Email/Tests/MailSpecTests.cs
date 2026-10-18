using Themia.Core.Accounts;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Email.Tests;

/// <summary>[S-6420] [S-544] Mail on the kit: a message's row and its menu, as the kit builds every item menu.</summary>
public sealed class MailSpecTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 14, 15, 0, 0, TimeSpan.FromHours(2));

    private static MailMessage Message(MailFolder folder = MailFolder.Inbox, bool read = false)
        => new("work", HostProviders.Microsoft, folder, "m1", "Quarterly numbers", "Dana Ruiz", "dana@example.com", At, read, "Hi all", string.Empty);

    private static List<string> Labels(IReadOnlyList<KitMenuLine> lines) => [.. lines.Select(l => l.Action?.Label ?? "-")];

    [Fact]
    public void S544_a_changeable_message_offers_its_commands_in_the_kits_one_order()
    {
        var lines = KitMenu.Lines(MailSpec.Actions(Message(), canChange: true));

        Assert.Equal(
            ["Open", "Reply", "Mark as read", "Archive", "Move to Junk", "Copy subject", "-", "Delete"],
            Labels(lines));

        // [S-7011] The kit's icon row and keys: Copy and Delete in the row, Enter, Ctrl+C and Delete shown.
        Assert.Equal([QuickCommand.Copy, QuickCommand.Delete], lines.Where(l => l.Quick is not null).Select(l => l.Quick!.Value));
        Assert.Equal(KeyChord.Plain(ChordKey.Enter), lines[0].Shortcut);
        Assert.Equal(KeyChord.Ctrl(ChordKey.C), lines.Single(l => l.Action?.Kind == KitActionKind.CopyText).Shortcut);
        Assert.Equal(KeyChord.Plain(ChordKey.Delete), lines[^1].Shortcut);
    }

    [Fact]
    public void S544_a_read_only_account_offers_open_reply_and_copy()
        => Assert.Equal(["Open", "Reply", "Copy subject"], Labels(KitMenu.Lines(MailSpec.Actions(Message(), canChange: false))));

    [Fact]
    public void S544_junk_has_no_archive_and_no_move_to_junk_and_a_read_message_marks_unread()
    {
        var labels = Labels(KitMenu.Lines(MailSpec.Actions(Message(MailFolder.Junk, read: true), canChange: true)));

        Assert.Equal(["Open", "Reply", "Mark as unread", "Copy subject", "-", "Delete"], labels);
    }

    [Fact]
    public void S1284_the_quick_actions_are_the_mark_archive_and_delete()
    {
        var inline = MailSpec.Actions(Message(), canChange: true).Where(a => a.Inline).Select(a => MailSpec.Change(a.Id));

        Assert.Equal([MailChange.MarkRead, MailChange.Archive, MailChange.Delete], inline);
        Assert.DoesNotContain(MailSpec.Actions(Message(), canChange: false), a => a.Inline);
    }

    [Fact]
    public void S6420_a_double_click_or_enter_opens_the_message_where_the_user_reads_mail()
        => Assert.Equal(MailSpec.OpenId, KitMenu.Primary(MailSpec.Actions(Message(), canChange: false))?.Id);

    [Fact]
    public void S1282_an_unread_message_is_emphasised_with_the_closed_envelope_and_the_accent()
    {
        var account = new MailAccount("work", HostProviders.Microsoft, "Work", "me@example.com", null);
        var unread = MailSpec.Item(Message(), account);
        var read = MailSpec.Item(Message(read: true), null);

        Assert.True(unread.Emphasis);
        Assert.Equal(KitTone.Accent, unread.Tone);
        Assert.Equal(nameof(KitIcon.Mail), unread.Lead.Value);
        Assert.Equal("Work", unread.Badges![0].Text);
        Assert.Contains("Dana Ruiz <dana@example.com>", unread.Tooltip, StringComparison.Ordinal);

        Assert.False(read.Emphasis);
        Assert.Equal(KitTone.Neutral, read.Tone);
        Assert.Equal(nameof(KitIcon.MailRead), read.Lead.Value);
        Assert.Null(read.Badges);
        Assert.Equal("Dana Ruiz", read.Title);
        Assert.Equal("Quarterly numbers", read.Subtitle);
    }

    [Fact]
    public void S1286_every_widget_asks_for_the_whole_shelf_whatever_its_size()
        => Assert.Equal(MailLimits.PerFolder, EmailConfig.FetchCount);
}
