using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The trust boundary in front of the shell. Every value here is one the app can genuinely be
/// handed: typed into a settings box, or written into the config JSON in the user's profile by
/// anything running as the user.
/// <para>
/// The rule is an allow list of three shapes, so most of these assert a <i>refusal</i>. That is the
/// point: a deny list would have been wrong the day Windows registered the next protocol handler.
/// </para>
/// </summary>
public class LaunchTargetTests
{
    // ---- the three shapes that are allowed -----------------------------------------------------

    [Theory]
    [InlineData("https://example.com/", "https://example.com/")]
    [InlineData("http://example.com/", "http://example.com/")]
    [InlineData("HTTPS://Example.COM/Path", "https://example.com/Path")]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("https://example.com/search?q=a+b", "https://example.com/search?q=a+b")]
    public void An_http_url_is_accepted_in_its_normalised_form(string raw, string expected)
    {
        var target = LaunchTargets.Classify(raw);

        Assert.True(target.IsValid);
        Assert.Equal(LaunchKind.Web, target.Kind);
        Assert.Equal(expected, target.Value);
    }

    [Theory]
    [InlineData(@"C:\Windows\notepad.exe")]
    [InlineData(@"C:\")]
    [InlineData(@"D:/projects/themia")]
    [InlineData(@"\\server\share\folder")]
    [InlineData(@"c:\lower\drive\letter")]
    public void A_fully_qualified_path_is_accepted_unchanged(string path)
    {
        var target = LaunchTargets.Classify(path);

        Assert.True(target.IsValid);
        Assert.Equal(LaunchKind.LocalPath, target.Kind);
        Assert.Equal(path, target.Value);
    }

    [Theory]
    [InlineData("shell:RecycleBinFolder")]
    [InlineData("shell:My Pictures")]
    [InlineData("shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    public void A_shell_namespace_name_is_accepted(string raw)
    {
        var target = LaunchTargets.Classify(raw);

        Assert.True(target.IsValid);
        Assert.Equal(LaunchKind.ShellNamespace, target.Kind);
        Assert.Equal(raw, target.Value);
    }

    [Fact]
    public void The_shell_prefix_is_normalised_but_the_name_keeps_its_case()
    {
        var target = LaunchTargets.Classify("SHELL:My Pictures");

        Assert.True(target.IsValid);
        Assert.Equal("shell:My Pictures", target.Value);
    }

    // ---- the schemes that are not -------------------------------------------------------------

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("file://server/share")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("ms-settings:windowsupdate")]
    [InlineData("steam://run/440")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("ftp://example.com/file")]
    [InlineData("search-ms:query=x")]
    public void Every_scheme_but_http_and_https_is_refused(string raw)
    {
        var target = LaunchTargets.Classify(raw);

        Assert.False(target.IsValid);
        Assert.Equal(LaunchRefusal.UnsupportedScheme, target.Refusal);
        Assert.Equal(string.Empty, target.Value);
    }

    [Fact]
    public void A_url_carrying_credentials_is_refused_rather_than_stripped()
    {
        // Reads as the first host and goes to the second. A phishing shape and nothing else.
        var target = LaunchTargets.Classify("https://real.example.com@attacker.example/");

        Assert.False(target.IsValid);
        Assert.Equal(LaunchRefusal.MalformedUrl, target.Refusal);
    }

    // ---- paths that are not ---------------------------------------------------------------------

    [Theory]
    [InlineData("notepad.exe")]
    [InlineData(@"docs\report.txt")]
    [InlineData(@"..\..\windows\system32")]
    [InlineData("C:relative")]
    [InlineData(@"\single-leading-separator")]
    public void A_relative_path_is_refused_rather_than_resolved(string path)
    {
        // The working directory of a desktop widget host is an implementation detail. Resolving
        // against it would open something the user never named, differently depending on how the
        // app was started.
        var target = LaunchTargets.Classify(path);

        Assert.False(target.IsValid);
        Assert.Equal(LaunchRefusal.NotRooted, target.Refusal);
    }

    [Theory]
    [InlineData(@"C:\Windows\*")]
    [InlineData(@"C:\Windows\?")]
    [InlineData(@"C:\a|b")]
    [InlineData(@"\\?\C:\Windows")]
    [InlineData(@"C:\file.txt:stream")]
    public void A_path_with_a_wildcard_or_an_illegal_character_is_refused(string path)
    {
        var target = LaunchTargets.Classify(path);

        Assert.False(target.IsValid);
        Assert.Equal(LaunchRefusal.InvalidPathCharacters, target.Refusal);
    }

    [Theory]
    [InlineData("C:\\Windows\\notepad.exe\0C:\\evil.exe")]
    [InlineData("https://example.com/\nhttps://evil.example/")]
    [InlineData("C:\\a\tb")]
    public void A_control_character_is_refused_before_anything_is_parsed(string raw)
    {
        // A NUL would truncate the string at the API, so what was checked and what was launched
        // would be two different strings.
        var target = LaunchTargets.Classify(raw);

        Assert.False(target.IsValid);
        Assert.Equal(LaunchRefusal.ControlCharacters, target.Refusal);
    }

    // ---- shell names that are not ---------------------------------------------------------------

    [Fact]
    public void A_bare_shell_prefix_is_refused()
    {
        var target = LaunchTargets.Classify("shell:");

        Assert.False(target.IsValid);
        Assert.Equal(LaunchRefusal.EmptyShellName, target.Refusal);
    }

    [Theory]
    [InlineData(@"shell:..\..\Windows")]
    [InlineData("shell:Downloads&calc")]
    [InlineData("shell:Down\"loads")]
    [InlineData("shell:%USERPROFILE%")]
    [InlineData(@"shell:C:\Windows")]
    [InlineData("shell:Downloads|more")]
    public void A_shell_name_carrying_separators_or_shell_syntax_is_refused(string raw)
    {
        var target = LaunchTargets.Classify(raw);

        Assert.False(target.IsValid);
        Assert.Equal(LaunchRefusal.InvalidShellName, target.Refusal);
    }

    // ---- nothing at all --------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void Whitespace_and_quotes_alone_count_as_nothing(string? raw)
    {
        var target = LaunchTargets.Classify(raw);

        Assert.False(target.IsValid);
        Assert.Equal(LaunchRefusal.Empty, target.Refusal);
    }

    [Fact]
    public void The_quotes_explorer_puts_around_a_copied_path_are_stripped()
    {
        var target = LaunchTargets.Classify("\"C:\\Program Files\\App\\app.exe\"");

        Assert.True(target.IsValid);
        Assert.Equal(@"C:\Program Files\App\app.exe", target.Value);
    }

    [Fact]
    public void A_url_longer_than_the_ceiling_is_refused()
    {
        var raw = "https://example.com/" + new string('a', LaunchTargets.MaxUrlLength);

        Assert.Equal(LaunchRefusal.TooLong, LaunchTargets.Classify(raw).Refusal);
    }

    [Fact]
    public void A_path_longer_than_any_windows_path_is_refused()
    {
        var raw = @"C:\" + new string('a', LaunchTargets.MaxPathLength);

        Assert.Equal(LaunchRefusal.TooLong, LaunchTargets.Classify(raw).Refusal);
    }

    [Fact]
    public void Every_refusal_has_a_sentence()
    {
        foreach (LaunchRefusal refusal in Enum.GetValues<LaunchRefusal>())
        {
            var message = LaunchTargets.RefusalMessage(refusal);

            if (refusal == LaunchRefusal.None)
            {
                Assert.Equal(string.Empty, message);
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(message));
        }
    }

    // ---- what a row is called --------------------------------------------------------------------

    [Fact]
    public void A_label_wins_over_everything_the_target_could_say()
    {
        Assert.Equal("Work mail", LaunchTargets.DisplayName("  Work mail  ", "https://mail.example.com/"));
    }

    [Theory]
    [InlineData("https://www.example.com/a/b", "example.com")]
    [InlineData("https://keep.google.com/", "keep.google.com")]
    [InlineData("http://EXAMPLE.com", "example.com")]
    public void A_web_target_is_named_by_its_host_without_the_www(string target, string expected)
    {
        Assert.Equal(expected, LaunchTargets.DisplayName(null, target));
    }

    [Theory]
    [InlineData(@"C:\Program Files\App\app.exe", "app")]
    [InlineData(@"C:\Users\Someone\Documents", "Documents")]
    [InlineData(@"C:\Users\Someone\notes.tar.gz", "notes.tar")]
    [InlineData(@"C:\", @"C:\")]
    public void A_path_is_named_by_its_own_name_without_the_extension(string target, string expected)
    {
        Assert.Equal(expected, LaunchTargets.DisplayName(string.Empty, target));
    }

    [Theory]
    [InlineData("shell:RecycleBinFolder", "Recycle Bin")]
    [InlineData("shell:MyComputerFolder", "This PC")]
    [InlineData("shell:SomethingNobodyNamed", "SomethingNobodyNamed")]
    public void A_shell_target_is_named_by_its_friendly_label_where_there_is_one(string target, string expected)
    {
        Assert.Equal(expected, LaunchTargets.DisplayName(null, target));
    }

    [Fact]
    public void A_refused_target_still_shows_the_raw_value_rather_than_a_blank_row()
    {
        // The row that needs fixing is exactly the one that must not disappear.
        Assert.Equal("javascript:alert(1)", LaunchTargets.DisplayName(null, "javascript:alert(1)"));
    }

    [Fact]
    public void Every_well_known_shell_name_is_one_the_rule_accepts()
    {
        foreach (var name in LaunchTargets.WellKnownShellNames)
        {
            var target = LaunchTargets.Classify(LaunchTargets.ShellTarget(name));

            Assert.True(target.IsValid, name);
            Assert.Equal(LaunchKind.ShellNamespace, target.Kind);
            Assert.False(string.IsNullOrWhiteSpace(LaunchTargets.ShellDisplayName(target.Value)));
        }
    }

    // ---- [S-1402] a packaged app's Apps-folder item ------------------------------------------------

    [Theory]
    [InlineData(@"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude")]
    [InlineData(@"shell:AppsFolder\5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App")]
    [InlineData(@"shell:appsfolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
    public void S1402_issue_189_a_packaged_apps_folder_item_is_a_shell_target(string raw)
    {
        var target = LaunchTargets.Classify(raw);

        Assert.Equal(LaunchKind.ShellNamespace, target.Kind);
        Assert.StartsWith(@"shell:AppsFolder\", target.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"shell:AppsFolder\..\..\Windows")]
    [InlineData(@"shell:AppsFolder\C:\Windows\calc.exe")]
    [InlineData(@"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude\x")]
    [InlineData(@"shell:AppsFolder\Claude_short!Claude")]
    [InlineData(@"shell:AppsFolder\Claude_pzs8sxrjxfjjc!")]
    [InlineData(@"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Cl&aude")]
    [InlineData(@"shell:AppsFolder\Claude_PZS8SXRJXFJJC!Claude")]
    [InlineData(@"shell:AppsFolder\")]
    [InlineData(@"shell:Downloads\x")]
    public void S1402_any_other_shell_name_with_a_separator_is_still_refused(string raw)
    {
        var target = LaunchTargets.Classify(raw);

        Assert.False(target.IsValid);
        Assert.Equal(LaunchRefusal.InvalidShellName, target.Refusal);
    }

    [Theory]
    [InlineData(@"::{4234d49b-0245-4df3-b780-3893943456e1}\Claude_pzs8sxrjxfjjc!Claude", @"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude")]
    [InlineData("Claude_pzs8sxrjxfjjc!Claude", @"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude")]
    [InlineData(@"::{4234d49b-0245-4df3-b780-3893943456e1}\{6D809377-6AF0-444B-8957-A3773F02200E}\Notepad++\notepad++.exe", null)]
    [InlineData(@"::{4234d49b-0245-4df3-b780-3893943456e1}\Microsoft.Windows.Explorer", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void S1402_issue_189_a_start_menu_items_parsing_name_gives_its_apps_folder_target(string? parsing, string? expected)
        => Assert.Equal(expected, LaunchTargets.AppsFolderTarget(parsing));
}
