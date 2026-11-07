using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Shortcuts.Tests;

/// <summary>[S-1417]..[S-1419] A shortcut's own icon (issue 157): a picture or an emoji over what Windows gives it.</summary>
public sealed class ShortcutIconsTests
{
    // ---- [S-1419] what may be an icon ---------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\Pictures\rocket.png", true)]
    [InlineData(@"C:\Pictures\photo.JPG", true)]
    [InlineData(@"C:\Pictures\photo.jpeg", true)]
    [InlineData(@"C:\Pictures\app.ico", true)]
    [InlineData(@"C:\Pictures\logo.svg", true)]
    [InlineData(@"C:\Pictures\old.bmp", true)]
    [InlineData(@"C:\Pictures\still.gif", true)]
    [InlineData(@"C:\Windows\notepad.exe", false)]
    [InlineData(@"C:\Pictures\photo.webp", false)]
    [InlineData(@"C:\Pictures\no-extension", false)]
    [InlineData(@"\\server\share\rocket.png", false)]
    [InlineData(@"pictures\rocket.png", false)]
    [InlineData("https://example.com/rocket.png", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void S1419_a_picture_is_a_local_file_of_a_picture_type(string path, bool expected)
        => Assert.Equal(expected, ShortcutIcons.IsPicture(path));

    [Theory]
    [InlineData("🚀", "🚀")]
    [InlineData("  🚀  ", "🚀")]
    [InlineData("👍🏽", "👍🏽")]
    [InlineData("👩🏽‍💻", "👩🏽‍💻")]
    [InlineData("👨‍👩‍👧‍👦", "👨‍👩‍👧‍👦")]
    [InlineData("🇨🇭", "🇨🇭")]
    [InlineData("❤️", "❤️")]
    [InlineData("A", "A")]
    [InlineData("★", "★")]
    public void S1419_one_emoji_or_character_is_kept_trimmed(string typed, string expected)
        => Assert.Equal(expected, ShortcutIcons.Emoji(typed));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("🚀🚀")]
    [InlineData("ab")]
    [InlineData("🚀 x")]
    [InlineData("\u200D")]
    [InlineData("\u0007")]
    [InlineData("\uD83D")]
    [InlineData("\uE000")]
    public void S1419_anything_but_one_emoji_is_refused(string typed)
        => Assert.Null(ShortcutIcons.Emoji(typed));

    [Fact]
    public void S1419_a_null_emoji_is_refused()
        => Assert.Null(ShortcutIcons.Emoji(null));

    // ---- [S-1418] which icon wins -------------------------------------------------------------------

    private static ShortcutItem Item(string? picture = null, string? emoji = null, string? named = null) => new()
    {
        Id = "a",
        Target = @"C:\Games\game.url",
        IconPicture = picture,
        IconEmoji = emoji,
        IconPath = named,
    };

    [Fact]
    public void S1418_the_users_picture_wins_over_everything_while_it_reads()
        => Assert.Equal(ShortcutIconSource.Picture, ShortcutIcons.Choose(Item(picture: @"C:\p.png", named: @"C:\g.ico"), pictureReadable: true));

    [Fact]
    public void S1418_a_picture_that_is_gone_falls_back_to_what_windows_gives()
    {
        Assert.Equal(ShortcutIconSource.Named, ShortcutIcons.Choose(Item(picture: @"C:\p.png", named: @"C:\g.ico"), pictureReadable: false));
        Assert.Equal(ShortcutIconSource.Automatic, ShortcutIcons.Choose(Item(picture: @"C:\p.png"), pictureReadable: false));
    }

    [Fact]
    public void S1418_the_users_emoji_wins_over_the_named_and_the_automatic_icon()
        => Assert.Equal(ShortcutIconSource.Emoji, ShortcutIcons.Choose(Item(emoji: "🚀", named: @"C:\g.ico"), pictureReadable: false));

    [Fact]
    public void S1418_with_nothing_of_the_users_a_url_named_icon_wins_over_the_automatic_one()
    {
        Assert.Equal(ShortcutIconSource.Named, ShortcutIcons.Choose(Item(named: @"C:\g.ico"), pictureReadable: false));
        Assert.Equal(ShortcutIconSource.Automatic, ShortcutIcons.Choose(Item(), pictureReadable: false));
    }

    // ---- [S-1417] setting and resetting -------------------------------------------------------------

    [Fact]
    public void S1417_a_picture_replaces_an_emoji_and_an_emoji_a_picture()
    {
        var item = Item(emoji: "🚀");

        Assert.True(ShortcutIcons.SetPicture(item, @"C:\p.png"));
        Assert.Equal((@"C:\p.png", (string?)null), (item.IconPicture, item.IconEmoji));

        Assert.True(ShortcutIcons.SetEmoji(item, " 🎮 "));
        Assert.Equal(((string?)null, "🎮"), (item.IconPicture, item.IconEmoji));
    }

    [Fact]
    public void S1417_a_refused_or_unchanged_icon_changes_nothing()
    {
        var item = Item(emoji: "🚀");

        Assert.False(ShortcutIcons.SetEmoji(item, "🚀"));
        Assert.False(ShortcutIcons.SetEmoji(item, "two words"));
        Assert.False(ShortcutIcons.SetPicture(item, @"\\server\share\p.png"));
        Assert.False(ShortcutIcons.SetPicture(item, @"C:\tool.exe"));
        Assert.Equal("🚀", item.IconEmoji);
        Assert.Null(item.IconPicture);
    }

    [Fact]
    public void S1417_reset_clears_the_users_icon_and_keeps_the_urls_own()
    {
        var item = Item(picture: @"C:\p.png", named: @"C:\g.ico");

        Assert.True(ShortcutIcons.Reset(item));
        Assert.Null(item.IconPicture);
        Assert.Null(item.IconEmoji);
        Assert.Equal(@"C:\g.ico", item.IconPath);
        Assert.False(ShortcutIcons.Reset(item));
    }

    // ---- [S-1419] load repair -----------------------------------------------------------------------

    [Fact]
    public void S1419_repair_drops_a_picture_that_is_not_a_local_picture_and_says_so()
    {
        var config = new ShortcutsConfig
        {
            Items =
            [
                new ShortcutItem { Id = "a", Target = "https://a.example/", IconPicture = @"\\server\share\p.png" },
                new ShortcutItem { Id = "b", Target = "https://b.example/", IconPicture = @"C:\tool.exe" },
                new ShortcutItem { Id = "c", Target = "https://c.example/", IconPicture = @"C:\ok.png" },
            ],
        };

        var repairs = config.Repair();

        Assert.Equal(2, repairs.Count(r => r.Contains("custom icon", StringComparison.Ordinal)));
        Assert.Equal([null, null, @"C:\ok.png"], config.Items.Select(i => i.IconPicture));
    }

    [Fact]
    public void S1419_repair_drops_an_emoji_that_is_not_one()
    {
        var config = new ShortcutsConfig
        {
            Items =
            [
                new ShortcutItem { Id = "a", Target = "https://a.example/", IconEmoji = "not an emoji" },
                new ShortcutItem { Id = "b", Target = "https://b.example/", IconEmoji = " 🚀" },
                new ShortcutItem { Id = "c", Target = "https://c.example/", IconEmoji = "🚀" },
            ],
        };

        var repairs = config.Repair();

        // Untrimmed is not the shape the menu writes: a hand edit, refused like any other.
        Assert.Equal(2, repairs.Count(r => r.Contains("emoji", StringComparison.Ordinal)));
        Assert.Equal([null, null, "🚀"], config.Items.Select(i => i.IconEmoji));
    }

    [Fact]
    public void S1419_repair_keeps_the_picture_of_an_item_with_both()
    {
        var config = new ShortcutsConfig
        {
            Items = [new ShortcutItem { Id = "a", Target = "https://a.example/", IconPicture = @"C:\ok.png", IconEmoji = "🚀" }],
        };

        var repairs = config.Repair();

        Assert.Single(repairs);
        Assert.Equal(@"C:\ok.png", config.Items[0].IconPicture);
        Assert.Null(config.Items[0].IconEmoji);
    }

    [Fact]
    public void S1417_a_clone_keeps_the_users_icon()
    {
        var copy = Item(picture: @"C:\p.png").Clone();
        Assert.Equal(@"C:\p.png", copy.IconPicture);

        Assert.Equal("🚀", Item(emoji: "🚀").Clone().IconEmoji);
    }

    // ---- [S-1417] the Icon submenu ------------------------------------------------------------------

    private sealed class NoLayouts : IAppLayouts
    {
        public bool CanEdit => true;

        public void Edit(string itemId)
        {
        }

        public void Launch(LayoutNode? layout, nint anchor)
        {
        }
    }

    private static (WidgetMenuLines Lines, List<WidgetTextAsk> Asked, Func<int> Saves) Menu()
    {
        var saves = 0;
        var asked = new List<WidgetTextAsk>();
        var lines = new WidgetMenuLines(() => saves++, asked.Add);
        return (lines, asked, () => saves);
    }

    [Fact]
    public void S1417_every_shortcut_and_layout_has_an_icon_submenu_in_its_list_line()
    {
        var config = new ShortcutsConfig
        {
            Items = [new ShortcutItem { Id = "a", Target = "https://a.example/" }, ShortcutItem.CreateLayout("Work")],
        };
        var lines = new WidgetMenuLines(() => { }, _ => { });
        ShortcutsMenu.Lines(lines, config, new NoLayouts());

        var list = Assert.Single(lines.Lines, l => l.Text == ShortcutsStr.WidgetsA_Catalog_Shortcuts_Label.Text());
        foreach (var row in list.Children!)
        {
            Assert.Contains(row.Children!, l => l.Text == ShortcutsStr.WidgetsC_Shortcuts_Icon_Label.Text());
        }
    }

    [Fact]
    public void S1417_the_icon_submenu_offers_picture_emoji_and_reset_off_while_automatic()
    {
        var item = Item();
        var (lines, _, _) = Menu();
        var icon = ShortcutsMenu.Icon(lines, item);

        Assert.Equal(
            [ShortcutsStr.WidgetsC_Shortcuts_Icon_Picture.Text(), ShortcutsStr.WidgetsC_Shortcuts_Icon_Emoji.Text(), "-", ShortcutsStr.WidgetsC_Shortcuts_Icon_Automatic.Text()],
            icon.Children!.Select(l => l.IsSeparator ? "-" : l.Text));
        Assert.True(icon.Children![3].Disabled);
        Assert.Null(icon.Value);
    }

    [Fact]
    public void S1417_emoji_asks_refuses_a_word_and_saves_one_emoji()
    {
        var item = Item();
        var (lines, asked, saves) = Menu();

        var emoji = Assert.Single(ShortcutsMenu.Icon(lines, item).Children!, l => l.Text == ShortcutsStr.WidgetsC_Shortcuts_Icon_Emoji.Text());
        Assert.True(emoji.Asks);
        emoji.Action!();

        var ask = Assert.Single(asked);
        Assert.Equal(ShortcutsStr.WidgetsC_Shortcuts_Icon_EmojiRefused.Text(), ask.Refuse("word"));
        Assert.Null(ask.Refuse("🚀"));

        ask.Done("🚀");
        Assert.Equal("🚀", item.IconEmoji);
        Assert.Equal(1, saves());
    }

    [Fact]
    public void S1417_the_icon_line_shows_the_choice_and_reset_goes_back_to_automatic()
    {
        var item = Item(emoji: "🚀");
        var (lines, _, saves) = Menu();
        var icon = ShortcutsMenu.Icon(lines, item);

        Assert.Equal("🚀", icon.Value);
        Assert.Equal("p.png", ShortcutsMenu.Icon(lines, Item(picture: @"C:\pics\p.png")).Value);

        var reset = icon.Children![3];
        Assert.False(reset.Disabled);
        reset.Action!();

        Assert.Null(item.IconEmoji);
        Assert.Equal(1, saves());
        Assert.Null(ShortcutsMenu.Icon(lines, item).Value);
    }
}
