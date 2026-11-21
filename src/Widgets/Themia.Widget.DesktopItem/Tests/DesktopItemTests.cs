using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.DesktopItem.Tests;

/// <summary>[S-5600]..[S-5607] [S-11204] The desktop icon's rules: its two sources, its cell and its page. An own icon's own are <c>OwnIconTests</c>.</summary>
public sealed class DesktopItemTests
{
    [Fact]
    public void S5600_a_clone_keeps_its_item_and_is_a_config_of_its_own()
    {
        var config = new DesktopItemConfig { Item = @"C:\Users\a\Desktop\Report.docx" };
        var clone = Assert.IsType<DesktopItemConfig>(config.Clone());
        Assert.NotSame(config, clone);
        Assert.Equal(config.Item, clone.Item);
        Assert.True(clone.IsDesktopItem);
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
    public void S5600_one_source_a_desktop_item_holds_nothing_of_an_own_icon()
    {
        var both = new DesktopItemConfig
        {
            Item = @"C:\Users\a\Desktop\Report.docx",
            Target = "https://example.com/",
            Label = "Mine",
            IconEmoji = "\u2605",
        };

        Assert.Single(both.Repair(), r => r.Contains("own icon", StringComparison.Ordinal));
        Assert.Equal((@"C:\Users\a\Desktop\Report.docx", string.Empty, string.Empty, null), (both.Item, both.Target, both.Label, both.IconEmoji));
        Assert.True(both.IsDesktopItem);
        Assert.False(both.NeedsSetup(FeatureSet.None));
    }

    [Fact]
    public void S5600_the_two_sources_are_told_apart_by_the_key_alone()
    {
        var item = new DesktopItemConfig { Item = @"C:\Users\a\Desktop\Report.docx" };
        var own = new DesktopItemConfig { Target = @"C:\Windows\notepad.exe" };
        var waiting = new DesktopItemConfig();

        Assert.True(item.IsDesktopItem);
        Assert.False(item.OpensNothing);
        Assert.False(own.IsDesktopItem);
        Assert.False(own.OpensNothing);
        Assert.True(waiting.OpensNothing);
        Assert.True(waiting.NeedsSetup(FeatureSet.None));

        // A desktop item is no layout to the editor and takes no name of the user's.
        Assert.Null(item.FindLayout(item.ItemId));
        Assert.False(item.RenameOwn("Mine"));
        Assert.Equal(string.Empty, item.Label);
        Assert.True(own.RenameOwn("Mine"));
        Assert.Equal("Mine", own.OwnName);
    }

    [Fact]
    public void S11204_one_fixed_cell_per_its_name_lines_with_nothing_to_spare()
    {
        var kind = ModuleCatalog.Of(new WidgetId("desktopItem"));

        // 8 + 48 + 4 + lines × 16 + 8, 76 wide (Explorer's cell); the design's gap is the host's to add.
        var two = kind.Sizing(new DesktopItemConfig());
        Assert.True(two.FixedWidth && two.FixedHeight);
        Assert.Equal(new SizeDip(76, 100), two.Natural);
        Assert.Equal(new SizeDip(76, 84), kind.Sizing(new DesktopItemConfig { LabelLines = 1 }).Natural);
        Assert.Equal(new DesktopCellFit(FamilyShape.IconHuge, 1), DesktopCell.Fit(DesktopIconSize.Medium, 1));

        // [S-5593] The cell is the desktop's at the icon size in force (medium until the host applies another).
        Assert.Equal(DesktopCell.Size(DesktopCell.Current, 2), two.Natural);
    }

    [Fact]
    public void S5611_the_kind_is_a_desktop_icon_and_only_its_tile_on_the_add_page_is_a_shortcut()
    {
        // Regression: the kind was labelled "Shortcut", so a plain desktop file selected in edit mode
        // was titled "Shortcut", and two of them "2 × Shortcut".
        var kind = ModuleCatalog.Of(new WidgetId("desktopItem"));

        Assert.Equal("Desktop icon", kind.Label.Text(Locale.En));
        Assert.Equal("Shortcut", kind.AddLabel!.Value.Text(Locale.En));
        Assert.Equal(kind.Label.Text(), kind.Name);
        Assert.Equal(kind.AddLabel!.Value.Text(), kind.TileName);

        // The add page finds the tile under both names, its own first (the one shown).
        var doc = AddSearch.Doc(kind, []);
        Assert.Equal(kind.TileName, doc.Labels[0]);
        Assert.Contains(kind.Name, doc.Labels);

        // The kind's name is in every language (the forty that the desktop item's label had).
        foreach (var locale in Enum.GetValues<Locale>().Where(l => l != Locale.En))
        {
            Assert.NotEqual("Desktop icon", kind.Label.Text(locale));
        }
    }

    [Fact]
    public void S5600_a_new_one_is_an_own_icon_on_no_card_with_no_header()
    {
        var kind = ModuleCatalog.Of(new WidgetId("desktopItem"));
        var fresh = Assert.IsType<DesktopItemConfig>(kind.CreateDefault());

        Assert.False(fresh.IsDesktopItem);
        Assert.True(fresh.OpensNothing);
        Assert.Equal(WidgetTraits.StartsTransparent | WidgetTraits.NeverJoins, kind.Traits);
        Assert.Equal(typeof(DesktopItemConfig), kind.ConfigType);
    }

    /// <summary>
    /// [S-5612] Regression (owner, 2026-11-19: "the hover and highlight of a desktop icon should always be the same area
    /// that the widget itself has (where the corners are)"): the fill was Explorer's smaller mark, 3 under the cell's top
    /// and as tall as the name. Now it is the card box itself, at every icon size and display scale.
    /// </summary>
    [Theory]
    [InlineData(DesktopIconSize.Small, 1.0)]
    [InlineData(DesktopIconSize.Medium, 1.0)]
    [InlineData(DesktopIconSize.Medium, 1.25)]
    [InlineData(DesktopIconSize.Medium, 1.5)]
    [InlineData(DesktopIconSize.Large, 1.5)]
    [InlineData(DesktopIconSize.Large, 2.0)]
    public void S5612_the_highlight_is_the_icons_whole_card_box_where_its_corners_are(DesktopIconSize size, double scale)
    {
        var screen = new Themia.Core.Geometry.ScreenMetrics(1920 / scale, 1080 / scale);
        var cell = DesktopCell.Grid(size);
        var rect = Themia.Core.Geometry.FracRect.FromDips(0.1, 0.1, cell.Width, cell.Height, screen);
        var card = Themia.Core.Geometry.GridGeometry.PaintedCell(rect, screen, scale);

        var highlight = DesktopCell.Highlight(card.Width, card.Height);

        Assert.Equal(new Themia.Core.Geometry.DipRect(0, 0, card.Width, card.Height), highlight);
        Assert.Equal(card, highlight with { X = card.X, Y = card.Y });
    }

    [Fact]
    public void S9515_a_desktop_icon_never_joins_the_icons_it_touches_and_is_never_clipped_to_a_cards_corners()
    {
        // Regression (owner, 2026-11-15): icons on the widget grid touch, and the join squared the corners
        // they shared, so a highlight had two round corners and two square ones.
        var traits = ModuleCatalog.Of(new WidgetId("desktopItem")).Traits;
        Assert.False(Themia.Core.Widgets.CardJoins.Joins(traits, drawsSurface: true));
        Assert.False(Themia.Core.Widgets.CardJoins.Joins(traits, drawsSurface: false));
        Assert.Equal(0, Themia.Core.Widgets.CardJoins.ClipRadius(traits, drawsSurface: false, 8));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void S11204_a_desktop_items_page_is_its_name_lines_and_nothing_else(int lines)
    {
        var config = new DesktopItemConfig { Item = @"C:\Users\a\Desktop\Report.docx", LabelLines = lines };
        var saves = 0;
        var menu = new Themia.Sdk.WinUI.WidgetMenuLines(() => saves++);

        new DesktopItemModule().MenuLines(menu, config);

        // No target, name, icon or click mode to set: the desktop says what it is. No host is asked either.
        var line = Assert.Single(menu.Lines);
        Assert.Empty(menu.StyleLines);
        Assert.Equal(DesktopItemStr.DesktopItem_NameLines.Text(), line.Text);
        Assert.False(line.SetsUp);
        var options = line.Children!;
        Assert.Equal(2, options.Count);
        Assert.Equal([lines == 1, lines == 2], options.Select(o => o.Checked == true));

        var other = options[lines == 1 ? 1 : 0];
        other.Action!();
        Assert.Equal(lines == 1 ? 2 : 1, config.LabelLines);
        Assert.Equal(1, saves);
        Assert.Equal(new SizeDip(76, lines == 1 ? 100 : 84), ModuleCatalog.Of(new WidgetId("desktopItem")).Sizing(config).Natural);
    }

    [Fact]
    public void S11204_both_sources_share_the_one_name_lines_line()
    {
        var item = new Themia.Sdk.WinUI.WidgetMenuLines(() => { });
        new DesktopItemModule().MenuLines(item, new DesktopItemConfig { Item = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" });
        var own = new Themia.Sdk.WinUI.WidgetMenuLines(() => { });
        var line = DesktopItemModule.NameLines(own, new DesktopItemConfig { Target = "https://example.com/" });

        Assert.Equal(Assert.Single(item.Lines).Text, line.Text);
        Assert.Equal(item.Lines[0].Children!.Select(o => o.Text), line.Children!.Select(o => o.Text));
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
