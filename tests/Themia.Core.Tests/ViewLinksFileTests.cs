using Themia.Core.Links;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11947] A file a widget names by a path is one only where the path is a full one.</summary>
public sealed class ViewLinksFileTests
{
    [Theory]
    [InlineData(@"C:\Users\Ada\Documents\Budget 2027.xlsx", @"C:\USERS\ADA\DOCUMENTS\BUDGET 2027.XLSX")]
    [InlineData(@"  c:\users\ada\Documents\  ", @"C:\USERS\ADA\DOCUMENTS")]
    [InlineData(@"\\server\share\notes.txt", @"\\SERVER\SHARE\NOTES.TXT")]
    public void S11947_a_full_path_is_its_file(string path, string key)
        => Assert.Equal(new FileLink(key), LinkKeys.LocalFileLink(path));

    [Theory]
    [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("shell:RecycleBinFolder")]
    [InlineData("https://example.com/report.pdf")]
    [InlineData("report.pdf")]
    [InlineData(@"C:report.pdf")]
    [InlineData("   ")]
    [InlineData(null)]
    public void S11947_anything_else_is_no_file(string? path)
        => Assert.Null(LinkKeys.LocalFileLink(path));

    [Fact]
    public void S11947_the_same_file_written_two_ways_is_one_key()
        => Assert.Equal(LinkKeys.LocalFileLink(@"C:\Users\Ada\Report.docx"), LinkKeys.LocalFileLink("c:/users/ada/REPORT.docx"));
}
