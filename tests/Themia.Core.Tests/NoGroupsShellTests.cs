using Themia.Core.Geometry;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2450] There are no groups (owner, 2026-11-17: "actually remove the group feature"): no key, no card menu line
/// groups widgets; [S-2457] cards a drag carries still join among themselves.
/// </summary>
public sealed class NoGroupsShellTests
{
    private const int VkG = 0x47;

    [Fact]
    public void S2450_ctrl_g_and_ctrl_shift_g_do_nothing()
    {
        Assert.Equal(EditCommand.None, EditKeys.Decide(VkG, shift: false, ctrl: true, alt: false).Command);
        Assert.Equal(EditCommand.None, EditKeys.Decide(VkG, shift: true, ctrl: true, alt: false).Command);
    }

    [Fact]
    public void S2450_the_card_menu_offers_no_group_line()
    {
        var facts = new CardMenuFacts(EditMode: true, CanBringToFront: true, CanSendToBack: true, SelectionCount: 3, CanPaste: false);
        Assert.DoesNotContain(Enum.GetNames<ShellCommand>(), n => n.Contains("Group", StringComparison.Ordinal));
        Assert.Equal(
            [ShellCommand.Cut, ShellCommand.Copy, ShellCommand.Paste, ShellCommand.Delete, ShellCommand.WidgetActions, ShellCommand.Separator,
             ShellCommand.BringToFront, ShellCommand.SendToBack, ShellCommand.Separator, ShellCommand.ToggleEditMode, ShellCommand.ToggleLockWidgets],
            ShellMenus.ForCard(facts).Select(i => i.Command));
    }

    [Fact]
    public void S2457_cards_join_only_within_their_set_so_a_dragged_pair_stays_one_panel()
    {
        // Two dragged cards side by side (set 1) over a still card they touch (set 0).
        PixelRect?[] boxes = [new PixelRect(0, 0, 100, 50), new PixelRect(100, 0, 100, 50), new PixelRect(0, 50, 100, 50)];

        var outlines = CardJoins.Outlines(boxes, 0.25, [1, 1, 0]);

        Assert.False(outlines[0].RoundTopRight);
        Assert.False(outlines[1].RoundTopLeft);
        Assert.True(outlines[0].RoundBottomLeft);
        Assert.True(outlines[0].DrawsBottom);
        Assert.Equal(CardOutline.Free, outlines[2]);
    }

    [Fact]
    public void S2457_one_set_per_box()
    {
        Assert.Throws<ArgumentException>(() => CardJoins.Outlines([new PixelRect(0, 0, 1, 1)], 0.25, [0, 1]));
    }
}
