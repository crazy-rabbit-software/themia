using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3536] [S-3537] ⚙ › Language &amp; region: the language and the regional formats, as the page held them.</summary>
public sealed partial class SettingsMenuTests
{
    [Fact]
    public void S3536_language_and_region_holds_the_language_then_the_three_formats()
    {
        var language = At<SettingsSubmenu>(new FakeHost(), SettingsMenu.Keys.Language);
        var keys = language.Lines.Select(l => l is SettingsSeparator ? "-" : l.Key).ToArray();

        Assert.Equal(["language/display", "-", "language/clock", "language/weekStart", "language/temperature"], keys);
        Assert.Equal("Language & region", language.Text);
        Assert.All(language.Lines.OfType<SettingsChoice>(), c => Assert.Contains(c.Options, o => o.Holds));
    }

    [Fact]
    public void S3536_the_languages_are_windows_setting_first_then_each_in_its_own_words()
    {
        var host = new FakeHost { WindowsLanguage = Locale.De };
        var display = At<SettingsChoice>(host, "language", "language/display");

        Assert.Equal(31, display.Options.Count);
        Assert.Equal("Windows setting (Deutsch)", display.Options[0].Text);
        Assert.True(display.Options[0].Holds);
        Assert.Equal("Deutsch", display.Value);
        Assert.Equal("Deutsch", At<SettingsSubmenu>(host, "language").Value);
        Assert.Contains(display.Options, o => o.Text == "日本語");
        Assert.Contains(display.Options, o => o.Text == "العربية");
        Assert.Equal(display.Options.Count, display.Options.Select(o => o.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.NotNull(display.Filter);

        // [S-1109] Each language is named in its own words in every language Themia speaks.
        try
        {
            Strings.SetLocale(Locale.Ja);
            Assert.Contains(At<SettingsChoice>(host, "language", "language/display").Options, o => o.Text == "Deutsch");
        }
        finally
        {
            Strings.SetLocale(Locale.En);
        }
    }

    [Fact]
    public void S3536_a_language_picked_is_spoken_and_the_one_that_holds_changes_nothing()
    {
        var host = new FakeHost();
        At<SettingsChoice>(host, "language", "language/display").Options.Single(o => o.Text == "Deutsch").Pick();

        Assert.Equal([AppLanguage.German], host.LanguagesSet);
        Assert.Equal(AppLanguage.German, host.State.Preferences.Language);

        At<SettingsChoice>(host, "language", "language/display").Options.Single(o => o.Holds).Pick();
        Assert.Equal([AppLanguage.German], host.LanguagesSet);
    }

    [Theory]
    [InlineData("language/clock", "language/clock/TwentyFour", nameof(RegionPreferences.Clock))]
    [InlineData("language/weekStart", "language/weekStart/Sunday", nameof(RegionPreferences.WeekStart))]
    [InlineData("language/temperature", "language/temperature/Fahrenheit", nameof(RegionPreferences.Temperature))]
    public void S3536_each_format_writes_its_regions_field_and_nothing_else_with_no_relayout(string line, string option, string field)
    {
        var host = new FakeHost();
        var before = Snapshot(host.State.Preferences.Region);

        At<SettingsChoice>(host, "language", line).Options.Single(o => o.Key == option).Pick();

        Assert.Equal([field], Changed(before, host.State.Preferences.Region));
        Assert.Equal([false], host.RelayoutFlags);
    }

    [Fact]
    public void S3536_each_format_offers_what_windows_says_first()
    {
        var host = new FakeHost();
        foreach (var format in At<SettingsSubmenu>(host, "language").Lines.OfType<SettingsChoice>().Skip(1))
        {
            Assert.StartsWith("Windows setting (", format.Options[0].Text, StringComparison.Ordinal);
            Assert.True(format.Options[0].Holds);

            // [S-3536] The line says what is in force, as the option names it inside its brackets.
            Assert.Equal("Windows setting (" + format.Value + ")", format.Options[0].Text);
        }

        At<SettingsChoice>(host, "language", "language/clock").Options.Single(o => o.Key == "language/clock/TwentyFour").Pick();
        Assert.Equal("24-hour", At<SettingsChoice>(host, "language", "language/clock").Value);
    }

    [Theory]
    [InlineData("deu", "Deutsch")]
    [InlineData("GERMAN", "Deutsch")]
    [InlineData("ja", "日本語")]
    [InlineData("farsi", "فارسی")]
    [InlineData("zh-hant", "繁體中文")]
    public void S3537_the_languages_box_finds_a_language_by_its_name_english_name_or_code(string typed, string found)
    {
        var display = At<SettingsChoice>(new FakeHost(), "language", "language/display");
        var kept = display.Options.Where(o => SettingsMenu.Matches(o.Text, o.Keywords, typed)).Select(o => o.Text).ToList();

        Assert.Contains(found, kept);
        Assert.True(kept.Count < display.Options.Count);
    }

    [Fact]
    public void S3537_an_empty_box_keeps_every_option_and_words_that_match_nothing_keep_none()
    {
        var options = At<SettingsChoice>(new FakeHost(), "language", "language/display").Options;

        Assert.All(options, o => Assert.True(SettingsMenu.Matches(o.Text, o.Keywords, "  ")));
        Assert.DoesNotContain(options, o => SettingsMenu.Matches(o.Text, o.Keywords, "klingon"));
    }
}
