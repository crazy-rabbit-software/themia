using Themia.Core.Desktop;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5500]..[S-5591] The desktop layer's rules: the known set, the reconcile, the next free cell.</summary>
public sealed partial class DesktopLayerTests
{
    [Fact]
    public void S5522_the_known_set_keeps_distinct_non_empty_keys()
    {
        var state = new AppState();
        state.Desktop.Known = [@"C:\Users\a\Desktop\x.txt", "", @"c:\users\a\desktop\X.TXT", "  ", new string('k', DesktopItemKeys.MaxLength + 1), "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"];

        var repairs = DesktopReconcile.RepairKnown(state);

        Assert.Single(repairs);
        Assert.Equal([@"C:\Users\a\Desktop\x.txt", "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"], state.Desktop.Known);
    }

    private static readonly string[] Folders = [@"C:\Users\a\Desktop", @"C:\Users\Public\Desktop\"];

    private static bool Shows(string key, DesktopItemKind kind, params (string Clsid, int Value)[] settings)
        => DesktopShown.Shows(new DesktopItem(key, "x", kind), Folders, @"C:\Users\a",
            clsid => settings.FirstOrDefault(s => string.Equals(s.Clsid, clsid, StringComparison.OrdinalIgnoreCase)) is { Clsid: not null } hit ? hit.Value : null);

    [Fact]
    public void S5510_files_show_only_from_the_two_desktop_folders()
    {
        Assert.True(Shows(@"C:\Users\a\Desktop\Report.docx", DesktopItemKind.File));
        Assert.True(Shows(@"c:\users\public\desktop\Edge.lnk", DesktopItemKind.Shortcut));
        Assert.True(Shows(@"C:\Users\a\Desktop\Photos", DesktopItemKind.Folder));

        // The root also lists the navigation pane's folders (OneDrive, a library's folder): not the desktop's.
        Assert.False(Shows(@"C:\Users\a\OneDrive", DesktopItemKind.Folder));
        Assert.False(Shows(@"C:\Users\a\Desktop\Photos\deep.jpg", DesktopItemKind.File));
        Assert.False(Shows("Report.docx", DesktopItemKind.File));
    }

    [Fact]
    public void S5510_shell_items_follow_the_desktop_icon_settings_the_recycle_bin_shown_by_default()
    {
        const string ThisPc = "{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
        Assert.True(Shows("::" + DesktopShown.RecycleBin, DesktopItemKind.ShellItem));
        Assert.False(Shows("::" + DesktopShown.RecycleBin, DesktopItemKind.ShellItem, (DesktopShown.RecycleBin, 1)));
        Assert.False(Shows("::" + ThisPc, DesktopItemKind.ShellItem));
        Assert.True(Shows("::" + ThisPc, DesktopItemKind.ShellItem, (ThisPc.ToLowerInvariant(), 0)));

        // Home, Gallery, Libraries: navigation-pane items with no setting are not on the desktop.
        Assert.False(Shows("::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}", DesktopItemKind.ShellItem));
        Assert.False(Shows(@"::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\::{21EC2020-3AEA-1069-A2DD-08002B30309D}", DesktopItemKind.ShellItem, (ThisPc, 0)));
    }

    [Fact]
    public void S5510_the_profile_folder_is_the_users_files_setting()
    {
        Assert.False(Shows(@"C:\Users\a", DesktopItemKind.Folder));
        Assert.True(Shows(@"C:\Users\a\", DesktopItemKind.Folder, (DesktopShown.UsersFiles, 0)));
        Assert.False(Shows(@"C:\Users\a", DesktopItemKind.Folder, (DesktopShown.UsersFiles, 1)));
    }

    [Fact]
    public void S5530_a_shell_item_opens_by_its_shell_name_a_path_as_it_is()
    {
        Assert.Equal("shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", DesktopItemKeys.LaunchTarget("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"));
        Assert.Equal(@"C:\Users\a\Desktop\Report.docx", DesktopItemKeys.LaunchTarget(@"C:\Users\a\Desktop\Report.docx"));
        Assert.True(Themia.Core.Widgets.LaunchTargets.Classify(DesktopItemKeys.LaunchTarget("::{645FF040-5081-101B-9F08-00AA002F954E}")).IsValid);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("0f8fad5bd9cb469fa16570867728950e")]
    [InlineData("A-b_9")]
    public void S5540_an_own_icons_key_is_never_a_desktop_items(string itemId)
    {
        var key = DesktopItemKeys.Own(itemId);

        Assert.Equal("*icon:" + itemId, key);
        Assert.True(DesktopItemKeys.IsOwn(key));
        Assert.True(DesktopItemKeys.IsOwnId(itemId));
        Assert.True(DesktopItemKeys.IsValid(key));

        // Not a shell item, not a path anything could be rooted at, and no launch target.
        Assert.False(DesktopItemKeys.IsShellItem(key));
        Assert.False(Path.IsPathRooted(key));
        Assert.False(Path.IsPathFullyQualified(key));
        Assert.Contains('*', Path.GetInvalidFileNameChars());
        Assert.False(Themia.Core.Widgets.LaunchTargets.Classify(key).IsValid);
    }

    [Fact]
    public void S5540_no_desktop_items_key_reads_as_an_own_icons()
    {
        Assert.False(DesktopItemKeys.IsOwn(@"C:\Users\a\Desktop\Report.docx"));
        Assert.False(DesktopItemKeys.IsOwn("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"));
        Assert.False(DesktopItemKeys.IsOwn(@"\\server\share\x"));
        Assert.False(DesktopItemKeys.IsOwn(@"C:\Users\a\Desktop\*icon:abc"));
        Assert.False(DesktopItemKeys.IsOwn("icon:abc"));
        Assert.False(DesktopItemKeys.IsOwn("*ICON:abc"));
        Assert.False(DesktopItemKeys.IsOwn(string.Empty));
        Assert.False(DesktopItemKeys.IsOwn(null));
        Assert.True(DesktopItemKeys.IsOwn("*icon:"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a b")]
    [InlineData("a:b")]
    [InlineData(@"a\b")]
    [InlineData("a*b")]
    [InlineData("a.b")]
    [InlineData("ä")]
    [InlineData("a\n")]
    public void S1411_an_id_of_anything_but_letters_digits_dash_and_underscore_is_no_own_id(string? itemId)
        => Assert.False(DesktopItemKeys.IsOwnId(itemId));

    [Fact]
    public void S1411_an_own_id_is_one_to_sixty_four_characters()
    {
        Assert.True(DesktopItemKeys.IsOwnId("a"));
        Assert.True(DesktopItemKeys.IsOwnId(new string('a', 64)));
        Assert.False(DesktopItemKeys.IsOwnId(new string('a', 65)));
        Assert.True(DesktopItemKeys.IsOwnId(Guid.NewGuid().ToString("n")));
        Assert.True(DesktopItemKeys.IsOwnId(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void S5540_a_config_is_selected_under_its_items_key_else_its_own_icons()
    {
        var item = new DeskIconFake { Item = @"C:\Users\a\Desktop\x.txt", ItemId = "abc", Target = @"C:\other.txt" };
        var own = new DeskIconFake { ItemId = "abc", Target = @"C:\Users\a\Desktop\x.txt" };
        var twin = new DeskIconFake { ItemId = "abd", Target = @"C:\Users\a\Desktop\x.txt" };

        Assert.Equal(@"C:\Users\a\Desktop\x.txt", DesktopItemKeys.Of(item));
        Assert.Equal("*icon:abc", DesktopItemKeys.Of(own));

        // Two own icons on one target are two icons; neither is the desktop item of that path.
        Assert.NotEqual(DesktopItemKeys.Of(own), DesktopItemKeys.Of(twin));
        Assert.False(DesktopItemKeys.Comparer.Equals(DesktopItemKeys.Of(own), DesktopItemKeys.Of(item)));
    }

    [Fact]
    public void S5521_items_are_ordered_shell_items_then_folders_then_files_by_name()
    {
        var sorted = DesktopItemOrder.Sort(
        [
            new(@"C:\d\b.txt", "b.txt", DesktopItemKind.File),
            new(@"C:\d\A.lnk", "A", DesktopItemKind.Shortcut),
            new(@"C:\d\zeta", "zeta", DesktopItemKind.Folder),
            new("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin", DesktopItemKind.ShellItem),
            new(@"C:\d\Alpha", "Alpha", DesktopItemKind.Folder),
        ]);

        Assert.Equal(["Recycle Bin", "Alpha", "zeta", "A", "b.txt"], sorted.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void S5603_a_missing_items_name_is_its_last_part_without_lnk()
    {
        Assert.Equal("Notes", DesktopItemKeys.FallbackName(@"C:\Users\a\Desktop\Notes.lnk"));
        Assert.Equal("Report.docx", DesktopItemKeys.FallbackName(@"C:\Users\a\Desktop\Report.docx"));
        Assert.Equal("Photos", DesktopItemKeys.FallbackName(@"C:\Users\a\Desktop\Photos\"));
        Assert.Equal(".lnk", DesktopItemKeys.FallbackName(@"C:\d\.lnk"));
    }

    [Fact]
    public void S5522_a_null_known_set_is_an_empty_one_said_once()
    {
        var state = new AppState { Desktop = null! };
        Assert.Single(DesktopReconcile.RepairKnown(state));
        Assert.Empty(state.Desktop.Known);

        state.Desktop.Known = null!;
        Assert.Single(DesktopReconcile.RepairKnown(state));
        Assert.Empty(state.Desktop.Known);

        Assert.Empty(DesktopReconcile.RepairKnown(state));
    }
}
