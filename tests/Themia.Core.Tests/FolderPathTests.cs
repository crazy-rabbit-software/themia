using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The trust boundary in front of the filesystem call. The path reaches it from a text box and from
/// a JSON file in the user's profile that anyone can hand-edit, so every one of these is a value
/// the widget can genuinely be handed.
/// </summary>
public class FolderPathTests
{
    private const string Fallback = @"C:\Users\Someone";

    [Theory]
    [InlineData(@"C:\Users\Someone\Documents")]
    [InlineData(@"C:\")]
    [InlineData(@"D:/projects/themia")]
    [InlineData(@"\\server\share\folder")]
    [InlineData(@"c:\lower\drive\letter")]
    public void A_fully_qualified_path_is_accepted_unchanged(string path)
    {
        var result = FolderPath.Validate(path);

        Assert.True(result.IsValid);
        Assert.Equal(path, result.Path);
    }

    [Fact]
    public void An_empty_path_means_the_profile_folder()
    {
        var result = FolderPath.Resolve(string.Empty, Fallback);

        Assert.True(result.IsValid);
        Assert.Equal(Fallback, result.Path);
        Assert.True(result.UsedFallback);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Whitespace_counts_as_empty_rather_than_as_a_path(string? configured)
    {
        var result = FolderPath.Resolve(configured, Fallback);

        Assert.True(result.IsValid);
        Assert.Equal(Fallback, result.Path);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public void An_empty_path_with_no_fallback_is_a_named_problem_not_a_guess()
    {
        var result = FolderPath.Resolve(string.Empty, string.Empty);

        Assert.False(result.IsValid);
        Assert.Equal(FolderPathProblem.Empty, result.Problem);
        Assert.Equal(string.Empty, result.Path);
    }

    [Fact]
    public void A_configured_path_is_used_in_preference_to_the_fallback()
    {
        var result = FolderPath.Resolve(@"D:\Pictures", Fallback);

        Assert.True(result.IsValid);
        Assert.Equal(@"D:\Pictures", result.Path);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public void S11608_a_path_naming_the_environment_s_variables_is_read_as_they_stand_and_then_checked()
    {
        static string Expand(string s) => s.Replace("%USERPROFILE%", @"C:\Users\Robin", StringComparison.OrdinalIgnoreCase);

        var documents = FolderPath.Resolve(@"%USERPROFILE%\Documents", Fallback, Expand);
        Assert.True(documents.IsValid);
        Assert.Equal(@"C:\Users\Robin\Documents", documents.Path);

        // A variable the environment lacks stays as written, which is not a rooted path: refused, not guessed.
        Assert.Equal(FolderPathProblem.NotRooted, FolderPath.Resolve(@"%NOWHERE%\Documents", Fallback, Expand).Problem);

        // Without an expander a value is taken as it is.
        Assert.Equal(FolderPathProblem.NotRooted, FolderPath.Resolve(@"%USERPROFILE%\Documents", Fallback).Problem);
    }

    [Theory]
    [InlineData(@"C:\Users\Some<one>")]
    [InlineData("C:\\Users\\Some\"one\"")]
    [InlineData(@"C:\Users\pipe|d")]
    [InlineData(@"C:\Users\*")]
    [InlineData(@"C:\Users\what?")]
    public void A_path_with_characters_windows_forbids_is_refused(string path)
    {
        var result = FolderPath.Validate(path);

        Assert.False(result.IsValid);
        Assert.Equal(FolderPathProblem.InvalidCharacters, result.Problem);
        Assert.Equal(string.Empty, result.Path);
    }

    [Fact]
    public void A_wildcard_never_reaches_the_enumeration_call()
    {
        // "*" and "?" are legal in a search pattern, so a path carrying one that reached
        // EnumerateFileSystemInfos would list something other than the folder it names.
        Assert.Equal(FolderPathProblem.InvalidCharacters, FolderPath.Validate(@"C:\Users\*\Documents").Problem);
        Assert.Equal(FolderPathProblem.InvalidCharacters, FolderPath.Validate(@"C:\Users\??").Problem);
    }

    [Fact]
    public void A_nul_or_control_character_is_refused_rather_than_truncated_at_the_api()
    {
        Assert.Equal(FolderPathProblem.InvalidCharacters, FolderPath.Validate("C:\\Users\0\\etc").Problem);
        Assert.Equal(FolderPathProblem.InvalidCharacters, FolderPath.Validate("C:\\Users\u0007").Problem);
    }

    [Fact]
    public void A_stream_or_extended_length_colon_is_refused()
    {
        Assert.Equal(FolderPathProblem.InvalidCharacters, FolderPath.Validate(@"C:\file.txt:stream").Problem);
        Assert.Equal(FolderPathProblem.InvalidCharacters, FolderPath.Validate(@"\\?\C:\Users").Problem);
    }

    [Theory]
    [InlineData("documents")]
    [InlineData(@"docs\reports")]
    [InlineData(@".\here")]
    [InlineData(@"..\..\up")]
    [InlineData(@"\rooted\but\no\drive")]
    [InlineData("C:")]
    [InlineData(@"C:relative")]
    public void A_relative_path_is_refused_and_never_resolved_against_the_process(string path)
    {
        var result = FolderPath.Validate(path);

        Assert.False(result.IsValid);
        Assert.Equal(FolderPathProblem.NotRooted, result.Problem);

        // The point of the whole check: nothing plausible-looking comes back in its place.
        Assert.Equal(string.Empty, result.Path);
    }

    [Fact]
    public void A_path_longer_than_windows_allows_is_refused()
    {
        var path = @"C:\" + new string('a', FolderPath.MaxLength);

        var result = FolderPath.Validate(path);

        Assert.False(result.IsValid);
        Assert.Equal(FolderPathProblem.TooLong, result.Problem);
    }

    [Fact]
    public void A_long_but_legal_path_is_accepted_because_reachability_is_the_filesystems_answer()
    {
        // Past the classic 260 limit, which may or may not apply depending on a machine setting
        // this function cannot see. "Does not exist" is the honest answer there, and it comes from
        // the filesystem, not from here.
        var path = @"C:\" + new string('a', 400);

        Assert.True(FolderPath.Validate(path).IsValid);
    }

    [Fact]
    public void Quotes_from_explorers_copy_as_path_are_trimmed()
    {
        var result = FolderPath.Validate("\"C:\\Program Files\\Themia\"");

        Assert.True(result.IsValid);
        Assert.Equal(@"C:\Program Files\Themia", result.Path);
    }

    [Theory]
    [InlineData(@"C:\Users\Someone\Documents", "Documents")]
    [InlineData(@"C:\Users\Someone\Documents\", "Documents")]
    [InlineData(@"C:\", @"C:\")]
    [InlineData("C:", @"C:\")]
    [InlineData(@"D:/projects/themia", "themia")]
    [InlineData(@"\\server\share", "share")]
    public void The_header_shows_the_folders_own_name(string path, string expected)
    {
        Assert.Equal(expected, FolderPath.DisplayName(path));
    }
}
