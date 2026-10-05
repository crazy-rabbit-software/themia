using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The decisions that shape the app list, tested without a Start menu to point at: what counts as
/// an application, which duplicate wins, and how the filter box narrows the result.
/// </summary>
public class InstalledAppRulesTests
{
    private static InstalledApp App(string name, string category = "", string? target = null)
        => new(name, category, target ?? $@"C:\Menu\{name}.lnk");

    [Theory]
    [InlineData("Uninstall Foo")]
    [InlineData("uninstall foo")]
    [InlineData("Foo Uninstaller")]
    [InlineData("UNINST")]
    [InlineData("Remove Foo")]
    public void The_housekeeping_shortcuts_that_ship_beside_an_app_are_not_apps(string name)
    {
        // An "Uninstall Foo" row one mis-click away on the desktop is a hazard, not a feature.
        Assert.False(InstalledAppRules.IsLauncherShortcut(name));
    }

    [Theory]
    [InlineData("Visual Studio Code")]
    [InlineData("Notepad")]
    [InlineData("7-Zip File Manager")]
    public void An_ordinary_shortcut_is_an_app(string name)
    {
        Assert.True(InstalledAppRules.IsLauncherShortcut(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_shortcut_with_no_name_is_not_an_app(string? name)
    {
        Assert.False(InstalledAppRules.IsLauncherShortcut(name));
    }

    [Theory]
    [InlineData("Startup", true)]
    [InlineData("startup", true)]
    [InlineData("STARTUP", true)]
    [InlineData("Accessories", false)]
    [InlineData(null, false)]
    public void The_startup_folder_is_not_a_category_of_apps(string? folder, bool skipped)
    {
        // Its contents are things Windows runs at sign-in, not things to launch.
        Assert.Equal(skipped, InstalledAppRules.IsSkippedFolder(folder));
    }

    [Fact]
    public void The_first_of_a_duplicate_name_wins()
    {
        // The caller scans the user's Start menu before the machine's, so a per-user install
        // shadows the all-users one.
        var arranged = InstalledAppRules.Arrange(
            [
                App("Foo", "User", @"C:\User\Foo.lnk"),
                App("foo", "Machine", @"C:\Machine\foo.lnk"),
            ],
            100);

        Assert.Single(arranged);
        Assert.Equal(@"C:\User\Foo.lnk", arranged[0].Target);
    }

    [Fact]
    public void The_list_is_sorted_by_name_regardless_of_case()
    {
        var arranged = InstalledAppRules.Arrange([App("zeta"), App("Alpha"), App("beta")], 100);

        Assert.Equal(["Alpha", "beta", "zeta"], arranged.Select(a => a.Name));
    }

    [Fact]
    public void A_nameless_entry_never_reaches_the_list()
    {
        var arranged = InstalledAppRules.Arrange([App(string.Empty), App("Alpha")], 100);

        Assert.Single(arranged);
    }

    [Fact]
    public void The_list_is_capped_after_sorting_rather_than_before()
    {
        // Capping the scan order would give the first three the filesystem returned, which is not
        // a list anybody can predict.
        var arranged = InstalledAppRules.Arrange([App("d"), App("a"), App("c"), App("b")], 2);

        Assert.Equal(["a", "b"], arranged.Select(a => a.Name));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void A_count_from_a_hand_edited_config_is_clamped(int maxCount)
    {
        var arranged = InstalledAppRules.Arrange([App("a"), App("b")], maxCount);

        Assert.InRange(arranged.Count, 1, InstalledAppRules.MaxEntries);
    }

    [Fact]
    public void An_empty_scan_arranges_to_an_empty_list()
    {
        Assert.Empty(InstalledAppRules.Arrange([], 100));
    }

    [Fact]
    public void Every_shortcut_the_scan_keeps_is_a_target_the_launcher_would_accept()
    {
        // The list ends at ShellLauncher, so the shape the scan produces has to be one the rule
        // accepts — otherwise every row would be refused at the click.
        var app = App("Notepad", "Accessories", @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Notepad.lnk");

        var classified = LaunchTargets.Classify(app.Target);

        Assert.True(classified.IsValid);
        Assert.Equal(LaunchKind.LocalPath, classified.Kind);
    }
}
