using Themia.App.Services.Files;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5559] The names a menu line carries for the icon of a kind of file that does not exist yet.</summary>
public sealed class TypeIconNameTests
{
    [Theory]
    [InlineData(".txt", "type:.txt")]
    [InlineData("txt", "type:.txt")]
    [InlineData(".DOCX", "type:.docx")]
    [InlineData(" .zip ", "type:.zip")]
    public void S5559_an_extension_names_its_types_icon(string extension, string expected)
        => Assert.Equal(expected, TypeIconName.Of(extension));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData(".a b")]
    [InlineData(".a.b")]
    [InlineData(".*")]
    [InlineData(@".a\b")]
    [InlineData(".a/b")]
    [InlineData(".a:b")]
    public void S5559_what_is_no_extension_names_no_icon(string? extension)
        => Assert.Null(TypeIconName.Of(extension));

    [Fact]
    public void S5559_a_name_reads_back_as_the_folder_or_its_extension()
    {
        Assert.Equal((true, string.Empty), TypeIconName.Parse(TypeIconName.Folder));
        Assert.Equal((false, ".lnk"), TypeIconName.Parse(TypeIconName.Shortcut));
        Assert.Equal((false, ".txt"), TypeIconName.Parse(TypeIconName.Of("TXT")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Users\Sample\Desktop\Report.txt")]
    [InlineData("shell:RecycleBinFolder")]
    [InlineData("type:")]
    [InlineData("type:txt")]
    [InlineData("type:.a b")]
    [InlineData(@"type:..\x")]
    [InlineData("TYPE:.txt")]
    public void S5559_a_path_or_a_malformed_name_is_no_type(string? name)
        => Assert.Null(TypeIconName.Parse(name));

    [Fact]
    public void S5559_no_path_can_start_as_a_type_name_does()
    {
        // A drive is one letter and a shell name starts with "shell:" or "::": neither is "type:".
        Assert.False(Path.IsPathFullyQualified(TypeIconName.Folder));
        Assert.StartsWith(TypeIconName.Prefix, TypeIconName.Shortcut, StringComparison.Ordinal);
    }
}
