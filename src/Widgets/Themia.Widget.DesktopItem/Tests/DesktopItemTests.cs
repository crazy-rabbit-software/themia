using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.DesktopItem.Tests;

/// <summary>[S-5600]..[S-5607] The desktop item widget's rules.</summary>
public sealed class DesktopItemTests
{
    [Fact]
    public void S5600_a_clone_keeps_its_item_and_is_a_config_of_its_own()
    {
        var config = new DesktopItemConfig { Item = @"C:\Users\a\Desktop\Report.docx" };
        var clone = Assert.IsType<DesktopItemConfig>(config.Clone());
        Assert.NotSame(config, clone);
        Assert.Equal(config.Item, clone.Item);
        Assert.IsAssignableFrom<IDesktopItemConfig>(clone);
    }

    [Fact]
    public void S5600_an_unusable_key_is_cleared_loudly_and_a_good_one_kept()
    {
        var bad = new DesktopItemConfig { Item = "C:\a\u0000b" };
        Assert.Single(bad.Repair());
        Assert.Equal(string.Empty, bad.Item);

        var good = new DesktopItemConfig { Item = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" };
        Assert.Empty(good.Repair());
        Assert.Empty(new DesktopItemConfig().Repair());
    }

    [Fact]
    public void S5601_the_default_cell_less_the_default_gap_holds_a_48_icon_and_two_lines()
    {
        var gap = new DesignState().WidgetGapPx;
        Assert.Equal(new ItemFit(FamilyShape.IconHuge, 2), DesktopItemLayout.For(DesktopItemModule.CellWidth - gap, DesktopItemModule.CellHeight - gap));

        // The gap, 8 + 48 + 4 + 2 × 16 + 8 and two DIPs to spare.
        Assert.True(DesktopItemModule.CellHeight - gap >= (2 * DesktopItemLayout.Inset) + FamilyShape.IconHuge + DesktopItemLayout.Gap + (2 * FamilyType.CaptionLine));
    }

    [Theory]
    [InlineData(90, 84, 48, 1)]
    [InlineData(90, 68, 32, 1)]
    [InlineData(64, 64, 48, 0)]
    [InlineData(48, 48, 32, 0)]
    [InlineData(30, 30, 16, 0)]
    [InlineData(400, 300, 48, 2)]
    public void S5601_smaller_cells_step_down_the_ladder_larger_ones_keep_the_icon(double width, double height, double icon, int lines)
        => Assert.Equal(new ItemFit(icon, lines), DesktopItemLayout.For(width, height));

    [Theory]
    [InlineData(@"C:\Users\a\Desktop\Notes.lnk", "Notes", DesktopItemKind.Shortcut, "Notes", 5)]
    [InlineData(@"C:\Users\a\Desktop\Report.docx", "Report.docx", DesktopItemKind.File, "Report.docx", 6)]
    [InlineData(@"C:\Users\a\Desktop\v2.0 drafts", "v2.0 drafts", DesktopItemKind.Folder, "v2.0 drafts", 11)]
    public void S5607_the_box_holds_the_name_as_shown_with_the_part_before_its_extension_selected(string key, string shown, DesktopItemKind kind, string text, int selected)
        => Assert.Equal((text, selected), Themia.Core.Desktop.DesktopRename.Edit(new Themia.Sdk.DesktopItem(key, shown, kind)));
}
