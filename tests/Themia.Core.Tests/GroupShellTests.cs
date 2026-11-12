using Themia.Core.Geometry;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2454] [S-2457] Groups in the shell: the keys, the card menu's lines, and cards joined while dragged.</summary>
public sealed class GroupShellTests
{
    private const int VkG = 0x47;

    [Fact]
    public void S2454_ctrl_g_groups_and_ctrl_shift_g_ungroups()
    {
        Assert.Equal(EditCommand.Group, EditKeys.Decide(VkG, shift: false, ctrl: true, alt: false).Command);
        Assert.Equal(EditCommand.Ungroup, EditKeys.Decide(VkG, shift: true, ctrl: true, alt: false).Command);
        Assert.Equal(EditCommand.None, EditKeys.Decide(VkG, shift: false, ctrl: false, alt: false).Command);
    }

    [Fact]
    public void S2454_the_card_menu_offers_only_the_group_commands_that_apply()
    {
        static List<ShellCommand> Commands(CardMenuFacts facts) => [.. ShellMenus.ForCard(facts).Select(i => i.Command)];
        var plain = new CardMenuFacts(EditMode: true, CanBringToFront: true, CanSendToBack: true, SelectionCount: 1, CanPaste: false);

        Assert.DoesNotContain(ShellCommand.Group, Commands(plain));
        Assert.DoesNotContain(ShellCommand.Ungroup, Commands(plain));

        var many = Commands(plain with { SelectionCount = 3, CanGroup = true });
        Assert.Contains(ShellCommand.Group, many);
        Assert.DoesNotContain(ShellCommand.LeaveGroup, many);

        var entered = Commands(plain with { CanLeaveGroup = true });
        Assert.Equal([ShellCommand.LeaveGroup], entered.Where(c => c is ShellCommand.Group or ShellCommand.Ungroup or ShellCommand.LeaveGroup));

        Assert.DoesNotContain(ShellCommand.Group, Commands(plain with { EditMode = false, CanGroup = true }));
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
