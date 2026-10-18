using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Geometry;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Email.Tests;

/// <summary>[S-1282]..[S-1287] The mail widget's ladder (`MailLayout`), moved from Core's MailDriveLayoutTests, unchanged.</summary>
public sealed class EmailLayoutTests
{
    // ---- [S-1282]..[S-1287] mail -------------------------------------------------------------------

    [Fact]
    public void S1283_a_wide_strip_and_a_very_wide_tile_go_one_line_per_message()
    {
        var config = new EmailConfig();

        Assert.True(MailLayout.Rows(config, 480, 116).Inline);    // Wide strip
        Assert.False(MailLayout.Rows(config, 480, 300).Inline);   // wide but tall: two-line rows still pay
        Assert.True(MailLayout.Rows(config, 800, 516).Inline);    // XL
        Assert.False(MailLayout.Rows(config, 320, 196).Inline);   // M
        Assert.False(MailLayout.Rows(config, 200, 356).Inline);   // Tall

        // The row view is one line already; it keeps its columns.
        Assert.False(MailLayout.Rows(new EmailConfig { Style = EmailStyle.Table }, 800, 516).Inline);
    }

    [Fact]
    public void S1283_one_line_rows_add_preview_then_account_as_they_widen()
    {
        var config = new EmailConfig();

        var narrow = MailLayout.Rows(config, 430, 116);
        Assert.False(narrow.Preview);

        var strip = MailLayout.Rows(config, 480, 116);
        Assert.True(strip.Preview);
        Assert.False(strip.Account);

        var wider = MailLayout.Rows(config, 560, 116);
        Assert.True(wider.Preview);
        Assert.True(wider.Account);

        // The account tag is the account's name alone at every size: no provider after it
        // (retired, concise widgets).
        var xl = MailLayout.Rows(config, 800, 516);
        Assert.True(xl.Account);
        Assert.Equal(1, xl.PreviewLines);

        // The compact view never shows a preview, on one line or two.
        Assert.False(MailLayout.Rows(new EmailConfig { Style = EmailStyle.Compact }, 800, 516).Preview);
    }

    [Fact]
    public void S1282_the_envelope_shows_from_a_medium_tile()
    {
        var config = new EmailConfig();
        Assert.False(MailLayout.Rows(config, 200, 106).Envelope);
        Assert.True(MailLayout.Rows(config, 320, 196).Envelope);
        Assert.True(MailLayout.Rows(config, 480, 116).Envelope);
    }

    [Fact]
    public void S1286_every_widget_asks_for_the_whole_shelf_whatever_its_size()
        => Assert.Equal(MailLimits.PerFolder, EmailConfig.FetchCount);

    [Fact]
    public void S1287_a_wide_row_view_adds_the_preview_after_a_lone_subject()
    {
        var chosen = EmailConfig.DefaultColumns; // From, Subject, Date

        Assert.Equal(chosen, MailLayout.Columns(chosen, 600, 8).Select(c => c.Column));
        Assert.Equal(
            [MailColumn.From, MailColumn.Subject, MailColumn.Preview, MailColumn.Date],
            MailLayout.Columns(chosen, 760, 8).Select(c => c.Column));

        // A user who chose the preview keeps their own order; one without a subject gets none added.
        MailColumn[] own = [MailColumn.Preview, MailColumn.From, MailColumn.Subject];
        Assert.Equal(own, MailLayout.Columns(own, 760, 8).Select(c => c.Column));
        MailColumn[] noSubject = [MailColumn.From, MailColumn.Date];
        Assert.Equal(noSubject, MailLayout.Columns(noSubject, 760, 8).Select(c => c.Column));
    }

    // ---- [S-1284] quick actions overlay the time ----------------------------------------------------

    [Theory]
    [InlineData(720, 0, 3)]
    [InlineData(720, 96, 3)]
    [InlineData(1100, 120, 2)]
    [InlineData(1600, 0, 3)]
    public void S1284_revealing_the_quick_actions_moves_nothing_but_the_slot_content(double inner, double account, int buttons)
    {
        var actions = MailLayout.QuickActionsWidth(buttons, button: 22, gap: 4);

        // The row is laid out once; hover and keyboard focus only swap what the trailing slot shows.
        var boxes = MailLayout.InlineBoxes(inner, 12, account, actions);

        // The slot is wide enough for either, so neither the time nor the actions overflow it ...
        Assert.True(boxes.Slot >= MailLayout.InlineTimeWidth);
        Assert.True(boxes.Slot >= actions, $"the actions ({actions}) overflow the slot ({boxes.Slot}) and push the line");

        // ... and the columns fill the row exactly, so nothing beside the slot changes width.
        var gaps = (account > 0 ? 3 : 2) * 12;
        Assert.Equal(inner, boxes.Sender + boxes.Line + boxes.Account + boxes.Slot + gaps, 6);
        Assert.Equal(account, boxes.Account);
    }

    [Fact]
    public void S1284_a_row_without_actions_keeps_the_narrow_time_slot()
    {
        Assert.Equal(0, MailLayout.QuickActionsWidth(0, 22, 4));
        Assert.Equal(MailLayout.InlineTimeWidth, MailLayout.InlineBoxes(800, 12, 0, 0).Slot);
        Assert.Equal(74, MailLayout.QuickActionsWidth(3, 22, 4));
    }
}
