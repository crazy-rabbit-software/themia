using Xunit;

namespace Themia.Widget.DesktopItem.Tests;

/// <summary>[S-9508] What a drop onto a shortcut that waits for setup makes it.</summary>
public sealed class OwnIconDropTests
{
    private static string? NoLink(string _) => null;

    private static string? NoText(string _) => null;

    [Fact]
    public void S9508_the_first_dropped_path_becomes_the_target_by_reference()
    {
        var dropped = OwnIconDrop.FromDrop([@"C:\Work\report.docx", @"D:\Photos"], [], NoLink, NoText);

        Assert.NotNull(dropped);
        Assert.Equal(@"C:\Work\report.docx", dropped.Target);
        Assert.Equal(string.Empty, dropped.Label);
        Assert.Null(dropped.IconPath);
    }

    [Fact]
    public void S9508_urls_count_only_when_no_files_came_with_the_drop()
    {
        var withFiles = OwnIconDrop.FromDrop([@"C:\a.txt"], ["https://example.com"], NoLink, NoText);
        var urlsOnly = OwnIconDrop.FromDrop([], ["javascript:alert(1)", "https://example.com/page"], NoLink, NoText);

        Assert.Equal(@"C:\a.txt", withFiles!.Target);
        Assert.Equal("https://example.com/page", urlsOnly!.Target);
    }

    [Fact]
    public void S9508_a_refused_thing_is_passed_over_and_a_drop_of_nothing_usable_is_nothing()
    {
        Assert.Equal(@"C:\b.txt", OwnIconDrop.FromDrop(["relative\\path.txt", @"C:\b.txt"], [], NoLink, NoText)!.Target);
        Assert.Null(OwnIconDrop.FromDrop(["relative\\path.txt"], ["https://example.com/"], NoLink, NoText));
        Assert.Null(OwnIconDrop.FromDrop([], ["ftp://example.com/", "ms-settings:display"], NoLink, NoText));
        Assert.Null(OwnIconDrop.FromDrop([], [], NoLink, NoText));
    }

    [Fact]
    public void S9508_a_drop_sets_the_shortcut_up_and_keeps_a_name_the_user_gave()
    {
        var unnamed = new DesktopItemConfig();
        new DroppedTarget("https://learn.microsoft.com/", "Docs", null).ApplyTo(unnamed);
        Assert.Equal(("https://learn.microsoft.com/", "Docs"), (unnamed.Target, unnamed.Label));
        Assert.False(unnamed.OpensNothing);

        var named = new DesktopItemConfig { Label = "Mine" };
        new DroppedTarget(@"C:\Games\hl.url", "Half-Life", @"C:\Steam\hl.ico").ApplyTo(named);
        Assert.Equal((@"C:\Games\hl.url", "Mine", @"C:\Steam\hl.ico"), (named.Target, named.Label, named.IconPath));
    }

    [Fact]
    public void S9508_a_url_file_becomes_its_web_address_named_after_the_file()
    {
        var item = OwnIconDrop.FromPath(@"C:\Users\me\Desktop\Docs.url", NoLink,
            _ => "[InternetShortcut]\r\nURL=https://learn.microsoft.com/\r\nIconFile=C:\\icons\\docs.ico\r\n");

        Assert.NotNull(item);
        Assert.Equal("https://learn.microsoft.com/", item.Target);
        Assert.Equal("Docs", item.Label);
        Assert.Null(item.IconPath);
    }

    [Fact]
    public void S9508_a_url_file_to_a_settings_page_becomes_that_page()
    {
        var item = OwnIconDrop.FromPath(@"C:\Users\me\Desktop\Display.url", NoLink, _ => "[InternetShortcut]\nURL=ms-settings:display\n");

        Assert.Equal(("ms-settings:display", "Display"), (item!.Target, item.Label));
    }

    [Fact]
    public void S9508_a_custom_protocol_url_file_is_kept_as_the_file_with_its_icon()
    {
        var item = OwnIconDrop.FromPath(@"C:\Users\me\Desktop\Half-Life.url", NoLink,
            _ => "[InternetShortcut]\nIDList=\nURL=steam://rungameid/70\nIconIndex=0\nIconFile=C:\\Steam\\steam\\games\\hl.ico\n");

        Assert.NotNull(item);
        Assert.Equal(@"C:\Users\me\Desktop\Half-Life.url", item.Target);
        Assert.Equal("Half-Life", item.Label);
        Assert.Equal(@"C:\Steam\steam\games\hl.ico", item.IconPath);
    }

    [Fact]
    public void S9508_an_unreadable_url_file_is_a_plain_file()
    {
        var item = OwnIconDrop.FromPath(@"C:\x\Broken.url", NoLink, NoText);

        Assert.NotNull(item);
        Assert.Equal(@"C:\x\Broken.url", item.Target);
        Assert.Null(item.IconPath);
    }

    [Fact]
    public void S9508_url_file_parsing_reads_only_the_internet_shortcut_section()
    {
        var parsed = OwnIconDrop.ParseUrlFile("[Other]\nURL=https://wrong.example\n[internetshortcut]\nurl = https://right.example \nIconFile=\n");

        Assert.Equal("https://right.example", parsed.Url);
        Assert.Null(parsed.IconFile);
        Assert.Equal(default, OwnIconDrop.ParseUrlFile(null));
    }

    [Fact]
    public void S9508_a_link_is_kept_by_its_target_and_named_as_the_link()
    {
        var resolved = OwnIconDrop.FromPath(@"C:\Start\Visual Studio Code.lnk", _ => @"C:\Apps\Code\Code.exe", NoText);
        var unresolved = OwnIconDrop.FromPath(@"C:\Start\Chrome (Work).lnk", NoLink, NoText);
        var refused = OwnIconDrop.FromPath(@"C:\Start\Odd.lnk", _ => "http://not-a-path", NoText);

        Assert.Equal(@"C:\Apps\Code\Code.exe", resolved!.Target);
        Assert.Equal("Visual Studio Code", resolved.Label);
        Assert.Equal(@"C:\Start\Chrome (Work).lnk", unresolved!.Target);
        Assert.Equal(@"C:\Start\Odd.lnk", refused!.Target);
    }

    [Fact]
    public void S9508_issue_189_a_start_menu_app_without_a_shortcut_becomes_its_apps_folder_item()
    {
        var item = OwnIconDrop.FromStartMenu(
            [("Claude", @"::{4234d49b-0245-4df3-b780-3893943456e1}\Claude_pzs8sxrjxfjjc!Claude")],
            _ => null, NoLink, NoText);

        Assert.NotNull(item);
        Assert.Equal(@"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude", item.Target);
        Assert.Equal("Claude", item.Label);
    }

    [Fact]
    public void S9508_a_start_menu_app_with_a_shortcut_is_that_shortcut()
    {
        const string link = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Notepad++.lnk";

        var item = OwnIconDrop.FromStartMenu(
            [("Notepad++", @"::{4234d49b-0245-4df3-b780-3893943456e1}\{6D809377-6AF0-444B-8957-A3773F02200E}\Notepad++\notepad++.exe")],
            name => name == "Notepad++" ? link : null,
            NoLink, NoText);

        Assert.Equal(link, item!.Target);
    }

    [Fact]
    public void S9508_a_start_menu_app_with_neither_is_passed_over()
    {
        Assert.Null(OwnIconDrop.FromStartMenu(
            [("Explorer", @"::{4234d49b-0245-4df3-b780-3893943456e1}\Microsoft.Windows.Explorer")],
            _ => null, NoLink, NoText));

        var second = OwnIconDrop.FromStartMenu(
            [("Explorer", @"::{4234d49b-0245-4df3-b780-3893943456e1}\Microsoft.Windows.Explorer"), ("Claude", "Claude_pzs8sxrjxfjjc!Claude")],
            _ => null, NoLink, NoText);
        Assert.Equal(@"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude", second!.Target);
    }
}
