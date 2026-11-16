using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.DesktopItem.Tests;

/// <summary>[S-11934] [S-11949] A desktop icon on its view's links: what the kind declares; an own icon to an executable is its app, to a file or folder that file.</summary>
public sealed class OwnIconLinksTests
{
    [Fact]
    public void S11934_the_kind_points_at_an_app_or_a_file_and_lights_for_both_and_for_the_picked_app()
    {
        var links = new DesktopItemModule().Descriptor.Links;

        Assert.Equal([Themia.Core.Links.LinkTopicId.HoveredApp, Themia.Core.Links.LinkTopicId.HoveredFile], links.Publishes.Order());
        Assert.Equal([Themia.Core.Links.LinkTopicId.HoveredApp, Themia.Core.Links.LinkTopicId.SelectedApp, Themia.Core.Links.LinkTopicId.HoveredFile], links.Follows.Order());
    }

    [Theory]
    [InlineData(@"C:\Program Files\Microsoft VS Code\Code.exe", "code")]
    [InlineData(@"C:\Windows\explorer.EXE", "explorer")]
    public void S11934_an_executable_is_its_app(string target, string key)
        => Assert.Equal(key, OwnIconRules.AppKey(LaunchTargets.Classify(target)));

    [Theory]
    [InlineData(@"C:\Users\a\Documents")]
    [InlineData(@"C:\Users\a\Documents\report.docx")]
    [InlineData(@"C:\Users\a\Desktop\Code.lnk")]
    [InlineData("https://example.com/app.exe")]
    [InlineData("shell:RecycleBinFolder")]
    [InlineData("ms-settings:display")]
    [InlineData("code.exe")]
    [InlineData("")]
    public void S11934_anything_else_is_no_app(string target)
        => Assert.Null(OwnIconRules.AppKey(LaunchTargets.Classify(target)));

    [Theory]
    [InlineData(@"C:\Users\a\Documents", @"C:\USERS\A\DOCUMENTS")]
    [InlineData(@"C:\Users\a\Documents\report.docx", @"C:\USERS\A\DOCUMENTS\REPORT.DOCX")]
    [InlineData(@"C:\Users\a\Desktop\Code.lnk", @"C:\USERS\A\DESKTOP\CODE.LNK")]
    public void S11949_a_file_or_folder_is_its_file_by_its_stored_path(string target, string key)
        => Assert.Equal(new Themia.Core.Links.FileLink(key), OwnIconRules.FileKey(LaunchTargets.Classify(target)));

    [Theory]
    [InlineData(@"C:\Program Files\Microsoft VS Code\Code.exe")]
    [InlineData("https://example.com/report.pdf")]
    [InlineData("shell:RecycleBinFolder")]
    [InlineData("ms-settings:display")]
    [InlineData("")]
    public void S11949_an_app_a_site_or_a_shell_place_is_no_file(string target)
        => Assert.Null(OwnIconRules.FileKey(LaunchTargets.Classify(target)));
}
