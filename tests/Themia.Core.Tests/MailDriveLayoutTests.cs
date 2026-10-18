using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Geometry;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1281] [S-1288] The shared list keys and fade (the mail widget's own ladder is its module's).</summary>
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
}
