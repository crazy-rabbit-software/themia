using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Geometry;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1280]..[S-1294] Mail and OneDrive using their whole tile; the shared list keys and fade.</summary>
public sealed class MailDriveLayoutTests
{
    // ---- [S-1288] list keys ------------------------------------------------------------------------

    [Theory]
    [InlineData(WidgetKey.Down, 3, 10, ListCommand.Focus, 4)]
    [InlineData(WidgetKey.Down, 9, 10, ListCommand.Focus, 9)]
    [InlineData(WidgetKey.Up, 0, 10, ListCommand.Focus, 0)]
    [InlineData(WidgetKey.Up, 5, 10, ListCommand.Focus, 4)]
    [InlineData(WidgetKey.PageDown, 3, 25, ListCommand.Focus, 13)]
    [InlineData(WidgetKey.PageDown, 20, 25, ListCommand.Focus, 24)]
    [InlineData(WidgetKey.PageUp, 4, 25, ListCommand.Focus, 0)]
    [InlineData(WidgetKey.Home, 7, 10, ListCommand.Focus, 0)]
    [InlineData(WidgetKey.End, 2, 10, ListCommand.Focus, 9)]
    [InlineData(WidgetKey.Enter, 2, 10, ListCommand.Open, 2)]
    [InlineData(WidgetKey.Delete, 2, 10, ListCommand.Delete, 2)]
    [InlineData(WidgetKey.Rename, 2, 10, ListCommand.Rename, 2)]
    [InlineData(WidgetKey.Menu, 2, 10, ListCommand.Menu, 2)]
    public void S1288_keys_move_and_act_on_the_focused_row(WidgetKey key, int index, int count, ListCommand command, int target)
        => Assert.Equal(new ListKeyResult(command, target), ListKeys.Decide(key, index, count));

    [Fact]
    public void S1288_with_no_focus_movement_lands_on_an_end_and_actions_do_nothing()
    {
        Assert.Equal(new ListKeyResult(ListCommand.Focus, 0), ListKeys.Decide(WidgetKey.Down, -1, 5));
        Assert.Equal(new ListKeyResult(ListCommand.Focus, 0), ListKeys.Decide(WidgetKey.Up, -1, 5));
        Assert.Equal(new ListKeyResult(ListCommand.Focus, 4), ListKeys.Decide(WidgetKey.End, -1, 5));
        Assert.Equal(ListCommand.None, ListKeys.Decide(WidgetKey.Enter, -1, 5).Command);
        Assert.Equal(ListCommand.None, ListKeys.Decide(WidgetKey.Delete, -1, 5).Command);
    }

    [Theory]
    [InlineData(WidgetKey.Left)]
    [InlineData(WidgetKey.Right)]
    [InlineData(WidgetKey.Tab)]
    [InlineData(WidgetKey.ShiftTab)]
    [InlineData(WidgetKey.Space)]
    public void S1560_keys_a_list_has_no_use_for_go_on_to_the_surface(WidgetKey key)
    {
        Assert.Equal(ListCommand.None, ListKeys.Decide(key, 2, 10).Command);
        Assert.Equal(ListCommand.None, ListKeys.Decide(key, -1, 10).Command);
    }

    [Fact]
    public void S1560_the_menu_key_needs_a_focused_row()
        => Assert.Equal(ListCommand.None, ListKeys.Decide(WidgetKey.Menu, -1, 5).Command);

    [Fact]
    public void S1288_escape_releases_even_an_empty_list_and_a_shrunk_list_acts_on_its_last_row()
    {
        Assert.Equal(ListCommand.Release, ListKeys.Decide(WidgetKey.Escape, 3, 0).Command);
        Assert.Equal(ListCommand.None, ListKeys.Decide(WidgetKey.Down, 0, 0).Command);
        Assert.Equal(new ListKeyResult(ListCommand.Open, 2), ListKeys.Decide(WidgetKey.Enter, 7, 3));

        Assert.Equal(2, ListKeys.AfterRemoval(2, 5));
        Assert.Equal(3, ListKeys.AfterRemoval(4, 4));
        Assert.Equal(-1, ListKeys.AfterRemoval(0, 0));
    }

    // ---- [S-1281] fade -----------------------------------------------------------------------------

    [Fact]
    public void S1281_rows_fade_toward_the_bottom_only_while_more_is_below()
    {
        Assert.Equal(1, ListFade.Opacity(0, 40, 200, moreBelow: true));
        Assert.Equal(1, ListFade.Opacity(160, 200, 200, moreBelow: false));
        Assert.Equal(ListFade.Floor, ListFade.Opacity(200, 240, 200, moreBelow: true));

        var near = ListFade.Opacity(170, 210, 200, moreBelow: true);
        var nearer = ListFade.Opacity(180, 220, 200, moreBelow: true);
        Assert.InRange(near, ListFade.Floor, 1);
        Assert.True(nearer < near);

        Assert.True(ListFade.MoreBelow(200, 400));
        Assert.False(ListFade.MoreBelow(400.2, 400));
    }

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
    public void S1286_a_big_tile_fetches_enough_whole_rows_to_fill_it()
    {
        var config = new EmailConfig { MaxCount = 10 };
        Assert.Equal(10, config.FetchCount(4));
        Assert.Equal(16, config.FetchCount(16));
        Assert.Equal(50, config.FetchCount(80));
    }

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
