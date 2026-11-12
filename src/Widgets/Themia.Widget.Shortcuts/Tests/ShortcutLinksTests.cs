using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Shortcuts.Tests;

/// <summary>[S-11934] A shortcut on its view's links: an executable is its app, anything else none.</summary>
public sealed class ShortcutLinksTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Microsoft VS Code\Code.exe", "code")]
    [InlineData(@"C:\Windows\explorer.EXE", "explorer")]
    public void S11934_an_executable_is_its_app(string target, string key)
        => Assert.Equal(key, ShortcutRules.AppKey(LaunchTargets.Classify(target)));

    [Theory]
    [InlineData(@"C:\Users\a\Documents")]
    [InlineData(@"C:\Users\a\Documents\report.docx")]
    [InlineData(@"C:\Users\a\Desktop\Code.lnk")]
    [InlineData("https://example.com/app.exe")]
    [InlineData("shell:RecycleBinFolder")]
    [InlineData("code.exe")]
    [InlineData("")]
    public void S11934_anything_else_is_no_app(string target)
        => Assert.Null(ShortcutRules.AppKey(LaunchTargets.Classify(target)));

    [Theory]
    [InlineData(@"C:\Users\a\Documents", @"C:\USERS\A\DOCUMENTS")]
    [InlineData(@"C:\Users\a\Documents\report.docx", @"C:\USERS\A\DOCUMENTS\REPORT.DOCX")]
    [InlineData(@"C:\Users\a\Desktop\Code.lnk", @"C:\USERS\A\DESKTOP\CODE.LNK")]
    public void S11949_a_file_or_folder_is_its_file_by_its_stored_path(string target, string key)
        => Assert.Equal(new Themia.Core.Links.FileLink(key), ShortcutRules.FileKey(LaunchTargets.Classify(target)));

    [Theory]
    [InlineData(@"C:\Program Files\Microsoft VS Code\Code.exe")]
    [InlineData("https://example.com/report.pdf")]
    [InlineData("shell:RecycleBinFolder")]
    [InlineData("")]
    public void S11949_an_app_a_site_or_a_shell_place_is_no_file(string target)
        => Assert.Null(ShortcutRules.FileKey(LaunchTargets.Classify(target)));
}
