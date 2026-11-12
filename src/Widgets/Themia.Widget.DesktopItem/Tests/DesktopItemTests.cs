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
    public void S11204_one_fixed_cell_per_its_name_lines_with_nothing_to_spare()
    {
        var kind = ModuleCatalog.Of(new WidgetId("desktopItem"));

        // 8 + 48 + 4 + lines × 16 + 8, 90 wide; the design's gap is the host's to add.
        var two = kind.Sizing(new DesktopItemConfig());
        Assert.True(two.FixedWidth && two.FixedHeight && !two.IsLegacy);
        Assert.Equal(new SizeDip(90, 100), two.Natural);
        Assert.Equal(new SizeDip(90, 84), kind.Sizing(new DesktopItemConfig { LabelLines = 1 }).Natural);
        Assert.Equal(new ItemFit(FamilyShape.IconHuge, 1), DesktopItemLayout.Fit(1));
    }

    [Fact]
    public void S11204_name_lines_out_of_range_are_repaired_and_a_clone_keeps_them()
    {
        var config = new DesktopItemConfig { LabelLines = 5 };
        Assert.Single(config.Repair());
        Assert.Equal(2, config.LabelLines);
        Assert.Equal(1, ((DesktopItemConfig)new DesktopItemConfig { LabelLines = 1 }.Clone()).LabelLines);
    }

    [Theory]
    [InlineData(@"C:\Users\a\Desktop\Notes.lnk", "Notes", DesktopItemKind.Shortcut, "Notes", 5)]
    [InlineData(@"C:\Users\a\Desktop\Report.docx", "Report.docx", DesktopItemKind.File, "Report.docx", 6)]
    [InlineData(@"C:\Users\a\Desktop\v2.0 drafts", "v2.0 drafts", DesktopItemKind.Folder, "v2.0 drafts", 11)]
    public void S5607_the_box_holds_the_name_as_shown_with_the_part_before_its_extension_selected(string key, string shown, DesktopItemKind kind, string text, int selected)
        => Assert.Equal((text, selected), Themia.Core.Desktop.DesktopRename.Edit(new Themia.Sdk.DesktopItem(key, shown, kind)));
}
