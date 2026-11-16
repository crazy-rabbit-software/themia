using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.DesktopItem.Tests;

/// <summary>[S-9500]..[S-9511] An own icon's config and rules (the desktop icon's second source); its commands are the desktop's, <c>DesktopCommandsTests</c>.</summary>
public sealed class OwnIconTests
{
    private static DesktopItemConfig Layout(string label = "")
    {
        var config = new DesktopItemConfig { Label = label };
        config.MakeLayout();
        return config;
    }

    // ---- config ([S-9501]) -------------------------------------------------------------------------

    [Fact]
    public void S9501_a_new_one_opens_nothing_with_two_name_lines_and_a_double_click()
    {
        var config = Assert.IsType<DesktopItemConfig>(WidgetConfig.CreateDefault(new WidgetId("desktopItem")));

        Assert.Equal((string.Empty, string.Empty, false), (config.Target, config.Label, config.IsLayout));
        Assert.Equal((2, ItemClickMode.Double), (config.LabelLines, config.ClickMode));
        Assert.False(string.IsNullOrWhiteSpace(config.ItemId));
        Assert.Empty(config.Repair());
    }

    [Fact]
    public void S9501_a_clone_copies_everything_but_gets_an_id_of_its_own()
    {
        var config = new DesktopItemConfig
        {
            Label = "Mine",
            Target = "https://example.com/",
            IconPath = @"C:\i.ico",
            IconEmoji = "★",
            LabelLines = 1,
            ClickMode = ItemClickMode.Single,
        };

        var copy = Assert.IsType<DesktopItemConfig>(config.Clone());

        Assert.Equal((config.Label, config.Target, config.IconPath, config.IconEmoji, 1, ItemClickMode.Single),
            (copy.Label, copy.Target, copy.IconPath, copy.IconEmoji, copy.LabelLines, copy.ClickMode));
        Assert.NotEqual(config.ItemId, copy.ItemId);

        var layout = Layout("Work");
        var layoutCopy = (DesktopItemConfig)layout.Clone();
        Assert.True(layoutCopy.IsLayout);
        Assert.NotSame(layout.Layout, layoutCopy.Layout);
    }

    [Fact]
    public void S9501_repair_brings_unknown_values_back_and_mends_a_blank_id()
    {
        var config = new DesktopItemConfig { ItemId = " ", ClickMode = (ItemClickMode)5, LabelLines = 9, Target = "https://a.example/" };

        var repairs = config.Repair();

        Assert.Equal(3, repairs.Count);
        Assert.Equal((ItemClickMode.Double, 2), (config.ClickMode, config.LabelLines));
        Assert.False(string.IsNullOrWhiteSpace(config.ItemId));
        Assert.Equal("https://a.example/", config.Target);
    }

    [Fact]
    public void S9501_repair_mends_a_null_target_or_name_and_an_icon_that_is_no_local_file()
    {
        var broken = new DesktopItemConfig { Target = null!, Label = null! };
        Assert.Single(broken.Repair());
        Assert.Equal((string.Empty, string.Empty), (broken.Target, broken.Label));

        var icon = new DesktopItemConfig { Target = @"C:\Games\hl.url", IconPath = "https://example.com/i.ico" };
        Assert.Single(icon.Repair());
        Assert.Null(icon.IconPath);
    }

    [Fact]
    public void S9509_a_target_replaces_a_layout_and_a_layout_a_target()
    {
        var config = new DesktopItemConfig { Target = @"C:\Games\hl.url", IconPath = @"C:\Steam\hl.ico", Label = "Game", IconEmoji = "★" };

        config.MakeLayout();
        Assert.Equal((true, string.Empty, (string?)null), (config.IsLayout, config.Target, config.IconPath));

        config.SetTarget("shell:Downloads");
        Assert.Equal((false, "shell:Downloads"), (config.IsLayout, config.Target));

        // The name and the user's own icon are the shortcut's, not the target's.
        Assert.Equal(("Game", "★"), (config.Label, config.IconEmoji));
    }

    // ---- setup ([S-9511]) -----------------------------------------------------------------------

    [Fact]
    public void S9511_it_waits_for_setup_while_it_has_neither_a_target_nor_a_layout_and_sets_up_in_place()
    {
        Assert.True(new DesktopItemConfig().OpensNothing);
        Assert.True(new DesktopItemConfig { Target = "  " }.OpensNothing);
        Assert.False(new DesktopItemConfig { Target = "https://example.com/" }.OpensNothing);
        Assert.False(Layout().OpensNothing);

        ISetupRule rule = new DesktopItemConfig();
        Assert.True(rule.SetsUpInPlace);
        Assert.False(string.IsNullOrWhiteSpace(rule.SetupSentence));
    }

    // ---- names ([S-9502]) and renaming ([S-9507]) --------------------------------------------------

    [Theory]
    [InlineData(@"C:\Docs\report.pdf", "report")]
    [InlineData(@"C:\Docs", "Docs")]
    [InlineData("https://www.example.com/x", "example.com")]
    [InlineData("shell:Downloads", "Downloads")]
    [InlineData("ms-settings:network-wifi", "Network wifi")]
    [InlineData("ms-settings:", "Settings")]
    public void S9502_without_a_label_the_name_is_the_targets_own(string target, string expected)
        => Assert.Equal(expected, OwnIconRules.Name(new DesktopItemConfig { Target = target }));

    [Fact]
    public void S9502_a_label_wins_a_layout_nobody_named_is_an_app_layout_and_an_empty_one_the_kinds_name()
    {
        Assert.Equal("Mine", OwnIconRules.Name(new DesktopItemConfig { Target = @"C:\Docs\report.pdf", Label = " Mine " }));
        Assert.Equal("App layout", OwnIconRules.Name(Layout()));
        Assert.Equal("Work", OwnIconRules.Name(Layout("Work")));
        Assert.Equal("Shortcut", OwnIconRules.Name(new DesktopItemConfig()));
    }

    [Fact]
    public void S9507_a_typed_name_is_the_label_and_the_targets_own_name_or_nothing_clears_it()
    {
        var config = new DesktopItemConfig { Target = "https://b.example/", Label = "Bee" };

        Assert.False(OwnIconRules.Rename(config, " Bee "));
        Assert.True(OwnIconRules.Rename(config, "Honey"));
        Assert.Equal("Honey", config.Label);

        Assert.True(OwnIconRules.Rename(config, "b.example"));
        Assert.Equal(string.Empty, config.Label);

        config.Label = "Bee";
        Assert.True(OwnIconRules.Rename(config, "  "));
        Assert.Equal(string.Empty, config.Label);
        Assert.False(OwnIconRules.Rename(config, null));
    }

    // ---- the broken state ([S-9504]) ---------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\Windows\notepad.exe", true)]
    [InlineData("shell:RecycleBinFolder", true)]
    [InlineData(@"\\server\share\file.txt", false)]
    [InlineData("https://example.com/", false)]
    [InlineData("ms-settings:display", false)]
    [InlineData("javascript:alert(1)", false)]
    public void S9504_network_paths_web_settings_and_refused_targets_are_never_probed(string target, bool expected)
        => Assert.Equal(expected, LaunchTargets.MayProbe(LaunchTargets.Classify(target)));

    [Fact]
    public void S9504_only_a_probeable_local_path_is_checked()
    {
        var asked = new List<string>();
        TargetHealth Probe(string path)
        {
            asked.Add(path);
            return TargetHealth.Missing;
        }

        Assert.Equal(TargetHealth.Missing, OwnIconRules.Check(LaunchTargets.Classify(@"C:\gone.txt"), Probe));
        Assert.Equal(TargetHealth.Unknown, OwnIconRules.Check(LaunchTargets.Classify(@"\\evil\share\x"), Probe));
        Assert.Equal(TargetHealth.Unknown, OwnIconRules.Check(LaunchTargets.Classify("https://example.com/"), Probe));
        Assert.Equal(TargetHealth.Unknown, OwnIconRules.Check(LaunchTargets.Classify("shell:Downloads"), Probe));
        Assert.Equal(TargetHealth.Unknown, OwnIconRules.Check(LaunchTargets.Classify("ms-settings:display"), Probe));

        Assert.Equal([@"C:\gone.txt"], asked);
    }

    // ---- the Recycle Bin ([S-9510]) ----------------------------------------------------------------

    [Theory]
    [InlineData("shell:RecycleBinFolder", true)]
    [InlineData("SHELL:recyclebinfolder", true)]
    [InlineData("shell:MyComputerFolder", false)]
    [InlineData(@"C:\$Recycle.Bin", false)]
    public void S9510_only_the_bin_is_the_bin(string target, bool expected)
        => Assert.Equal(expected, OwnIconRules.IsRecycleBin(target));

    [Fact]
    public void S9510_the_bins_tooltip_says_how_much_it_holds()
    {
        Assert.Equal("Recycle Bin", RecycleBinState.Tooltip(Locale.En, "Recycle Bin", null));
        Assert.Equal("Recycle Bin — empty", RecycleBinState.Tooltip(Locale.En, "Recycle Bin", new RecycleBinState(0, 0)));
        Assert.StartsWith("Recycle Bin — 1 item (", RecycleBinState.Tooltip(Locale.En, "Recycle Bin", new RecycleBinState(1, 2048)), StringComparison.Ordinal);
        Assert.StartsWith("Recycle Bin — 3 items (", RecycleBinState.Tooltip(Locale.En, "Recycle Bin", new RecycleBinState(3, 2048)), StringComparison.Ordinal);
    }

    // ---- the kind on the Target line ([S-9509]) ----------------------------------------------------

    [Fact]
    public void S9509_the_kind_names_what_a_target_is_in_the_users_language()
    {
        Assert.Equal("Web address", OwnIconRules.KindLabel(Locale.En, LaunchTargets.Classify("https://a.example/"), TargetHealth.Unknown));
        Assert.Equal("Windows location", OwnIconRules.KindLabel(Locale.En, LaunchTargets.Classify("shell:Desktop"), TargetHealth.Unknown));
        Assert.Equal("Settings page", OwnIconRules.KindLabel(Locale.En, LaunchTargets.Classify("ms-settings:display"), TargetHealth.Unknown));
        Assert.Equal("Folder", OwnIconRules.KindLabel(Locale.En, LaunchTargets.Classify(@"C:\Windows"), TargetHealth.Folder));
        Assert.Equal("File", OwnIconRules.KindLabel(Locale.En, LaunchTargets.Classify(@"C:\x.txt"), TargetHealth.File));
        Assert.Equal("Path", OwnIconRules.KindLabel(Locale.En, LaunchTargets.Classify(@"C:\x.txt"), TargetHealth.Unknown));
        Assert.Equal("Missing", OwnIconRules.KindLabel(Locale.En, LaunchTargets.Classify(@"C:\x.txt"), TargetHealth.Missing));
        Assert.Equal("Not allowed", OwnIconRules.KindLabel(Locale.En, LaunchTargets.Classify("ftp://a"), TargetHealth.Unknown));
        Assert.Equal("App layout", OwnIconRules.LayoutKindLabel(Locale.En));

        Assert.Equal("Webadresse", OwnIconRules.KindLabel(Locale.De, LaunchTargets.Classify("https://a.example/"), TargetHealth.Unknown));
        Assert.Equal("Ordner", OwnIconRules.KindLabel(Locale.De, LaunchTargets.Classify(@"C:\Windows"), TargetHealth.Folder));
        Assert.Equal("Fehlt", OwnIconRules.KindLabel(Locale.De, LaunchTargets.Classify(@"C:\x.txt"), TargetHealth.Missing));
        Assert.Equal("App-Layout", OwnIconRules.LayoutKindLabel(Locale.De));
    }
}
