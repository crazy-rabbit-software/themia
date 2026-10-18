using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Shortcuts.Tests;

/// <summary>[S-1400] [S-1401] [S-1410] What a drop onto the Shortcuts widget adds, and the drag's reorder. Moved with the widget from Core's ShortcutsDropLayoutTests, unchanged.</summary>
public sealed class ShortcutDropTests
{
    private static string? NoLink(string _) => null;

    private static string? NoText(string _) => null;

    private static List<ShortcutItem> Items(params string[] ids)
        => ids.Select(id => new ShortcutItem { Id = id, Target = @"C:\" + id }).ToList();

    private static string Order(List<ShortcutItem> items) => string.Concat(items.Select(i => i.Id));

    [Fact]
    public void S1400_each_dropped_path_becomes_an_item_by_reference()
    {
        var result = ShortcutDrop.FromDrop([@"C:\Work\report.docx", @"D:\Photos"], [], [], NoLink, NoText);

        Assert.Equal([@"C:\Work\report.docx", @"D:\Photos"], result.Items.Select(i => i.Target));
        Assert.All(result.Items, item => Assert.Equal(string.Empty, item.Label));
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void S1400_urls_count_only_when_no_files_came_with_the_drop()
    {
        var withFiles = ShortcutDrop.FromDrop([@"C:\a.txt"], ["https://example.com"], [], NoLink, NoText);
        var urlsOnly = ShortcutDrop.FromDrop([], ["https://example.com/page", "javascript:alert(1)"], [], NoLink, NoText);

        Assert.Equal([@"C:\a.txt"], withFiles.Items.Select(i => i.Target));
        Assert.Equal(["https://example.com/page"], urlsOnly.Items.Select(i => i.Target));
        Assert.Equal(1, urlsOnly.Skipped);
    }

    [Fact]
    public void S1400_refused_and_already_held_targets_are_skipped_and_counted()
    {
        var existing = new[] { ShortcutItem.Create(@"C:\Work\report.docx") };

        var result = ShortcutDrop.FromDrop(
            [@"c:\work\REPORT.docx", "relative\\path.txt", @"C:\b.txt", @"C:\b.txt"],
            [],
            existing,
            NoLink,
            NoText);

        Assert.Equal([@"C:\b.txt"], result.Items.Select(i => i.Target));
        Assert.Equal(3, result.Skipped);
    }

    [Fact]
    public void S1401_a_url_file_becomes_its_web_address_named_after_the_file()
    {
        var item = ShortcutDrop.FromPath(@"C:\Users\me\Desktop\Docs.url", NoLink,
            _ => "[InternetShortcut]\r\nURL=https://learn.microsoft.com/\r\nIconFile=C:\\icons\\docs.ico\r\n");

        Assert.NotNull(item);
        Assert.Equal("https://learn.microsoft.com/", item.Target);
        Assert.Equal("Docs", item.Label);
        Assert.Null(item.IconPath);
    }

    [Fact]
    public void S1401_a_custom_protocol_url_file_is_kept_as_the_file_with_its_icon()
    {
        var item = ShortcutDrop.FromPath(@"C:\Users\me\Desktop\Half-Life.url", NoLink,
            _ => "[InternetShortcut]\nIDList=\nURL=steam://rungameid/70\nIconIndex=0\nIconFile=C:\\Steam\\steam\\games\\hl.ico\n");

        Assert.NotNull(item);
        Assert.Equal(@"C:\Users\me\Desktop\Half-Life.url", item.Target);
        Assert.Equal("Half-Life", item.Label);
        Assert.Equal(@"C:\Steam\steam\games\hl.ico", item.IconPath);
    }

    [Fact]
    public void S1401_an_unreadable_url_file_is_a_plain_file_item()
    {
        var item = ShortcutDrop.FromPath(@"C:\x\Broken.url", NoLink, NoText);

        Assert.NotNull(item);
        Assert.Equal(@"C:\x\Broken.url", item.Target);
        Assert.Null(item.IconPath);
    }

    [Fact]
    public void S1401_url_file_parsing_reads_only_the_internet_shortcut_section()
    {
        var parsed = ShortcutDrop.ParseUrlFile("[Other]\nURL=https://wrong.example\n[internetshortcut]\nurl = https://right.example \nIconFile=\n");

        Assert.Equal("https://right.example", parsed.Url);
        Assert.Null(parsed.IconFile);
        Assert.Equal(default, ShortcutDrop.ParseUrlFile(null));
    }

    [Fact]
    public void S1401_a_link_is_kept_by_its_target_and_named_as_the_link()
    {
        var resolved = ShortcutDrop.FromPath(@"C:\Start\Visual Studio Code.lnk", _ => @"C:\Apps\Code\Code.exe", NoText);
        var unresolved = ShortcutDrop.FromPath(@"C:\Start\Chrome (Work).lnk", NoLink, NoText);
        var refused = ShortcutDrop.FromPath(@"C:\Start\Odd.lnk", _ => "http://not-a-path", NoText);

        Assert.Equal(@"C:\Apps\Code\Code.exe", resolved!.Target);
        Assert.Equal("Visual Studio Code", resolved.Label);
        Assert.Equal(@"C:\Start\Chrome (Work).lnk", unresolved!.Target);
        Assert.Equal(@"C:\Start\Odd.lnk", refused!.Target);
    }

    [Fact]
    public void S1410_an_item_moves_before_the_one_it_is_dropped_on_or_to_the_end()
    {
        var items = Items("a", "b", "c", "d");

        Assert.True(ShortcutDrop.Reorder(items, ["d"], "b"));
        Assert.Equal("adbc", Order(items));

        Assert.True(ShortcutDrop.Reorder(items, ["a"], null));
        Assert.Equal("dbca", Order(items));
    }

    [Fact]
    public void S1410_a_drop_that_changes_nothing_writes_nothing()
    {
        var items = Items("a", "b", "c");

        Assert.False(ShortcutDrop.Reorder(items, ["b"], "b"));
        Assert.False(ShortcutDrop.Reorder(items, ["b"], "c"));
        Assert.False(ShortcutDrop.Reorder(items, ["c"], null));
        Assert.False(ShortcutDrop.Reorder(items, [], "a"));
        Assert.False(ShortcutDrop.Reorder(items, ["a"], "gone"));
        Assert.Equal("abc", Order(items));
    }

    [Fact]
    public void S1410_a_multi_selection_moves_together_in_its_own_order()
    {
        var items = Items("a", "b", "c", "d", "e");

        Assert.True(ShortcutDrop.Reorder(items, ["d", "b"], "a"));
        Assert.Equal("bdace", Order(items));

        // Before a dragged item = before the next one that is not dragged.
        items = Items("a", "b", "c", "d", "e");
        Assert.True(ShortcutDrop.Reorder(items, ["b", "d"], "d"));
        Assert.Equal("acbde", Order(items));
    }

    [Fact]
    public void S1410_the_insert_point_is_the_half_of_the_item_under_the_pointer()
    {
        string[] order = ["a", "b", "c"];

        Assert.Equal("b", ShortcutDrop.InsertBefore(order, 1, 0.2));
        Assert.Equal("c", ShortcutDrop.InsertBefore(order, 1, 0.7));
        Assert.Null(ShortcutDrop.InsertBefore(order, 2, 0.9));
        Assert.Null(ShortcutDrop.InsertBefore(order, -1, 0));
    }
}
