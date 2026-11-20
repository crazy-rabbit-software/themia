using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3536] [S-3537] settings › Language &amp; region: the language and the regional formats, as the page held them.</summary>
public sealed partial class SettingsMenuTests
{
    [Fact]
    public void S3536_language_and_region_holds_the_language_then_the_three_formats()
    {
        var language = At<SettingsSubmenu>(new FakeHost(), SettingsMenu.Keys.Language);
        var keys = language.Lines.Select(l => l is SettingsSeparator ? "-" : l.Key).ToArray();

        Assert.Equal(["language/display", "language/formats"], keys);
        Assert.Equal(
            ["language/clock", "language/weekStart", "language/temperature", SettingsMenu.Keys.FormatsRegion, "-", SettingsMenu.Keys.FormatsReset],
            At<SettingsSubmenu>(new FakeHost(), "language", "language/formats").Lines.Select(l => l is SettingsSeparator ? "-" : l.Key));
        Assert.Equal("Language & region", language.Text);
        Assert.All(language.Lines.OfType<SettingsChoice>(), c => Assert.Contains(c.Options, o => o.Holds));
    }

    /// <summary>
    /// [S-2319] Karpo's Formats card (KarpoMenu.Formats): its head says whether the formats follow Windows or are
    /// the user's; Windows' region settings open from it; Reset all, greyed while everything follows, puts every
    /// format back to Windows' in one change.
    /// </summary>
    [Fact]
    public void S2319_formats_say_whether_they_follow_windows_open_its_region_settings_and_reset_all()
    {
        var host = new FakeHost();
        var formats = At<SettingsSubmenu>(host, "language", SettingsMenu.Keys.Formats);
        Assert.Equal("Default (Windows)", formats.Value);
        Assert.Equal(Glyphs.DateAndTime, formats.Glyph);
        Assert.True(At<SettingsCommand>(host, "language", SettingsMenu.Keys.Formats, SettingsMenu.Keys.FormatsReset).Disabled);

        At<SettingsCommand>(host, "language", SettingsMenu.Keys.Formats, SettingsMenu.Keys.FormatsRegion).Run();
        Assert.Equal([SettingsMenu.RegionSettings], host.FakeAbout.Opened);

        At<SettingsChoice>(host, "language", SettingsMenu.Keys.Formats, "language/clock").Options.Single(o => o.Key == "language/clock/TwentyFour").Pick();
        Assert.Equal("Custom", At<SettingsSubmenu>(host, "language", SettingsMenu.Keys.Formats).Value);
        var reset = At<SettingsCommand>(host, "language", SettingsMenu.Keys.Formats, SettingsMenu.Keys.FormatsReset);
        Assert.False(reset.Disabled);

        var before = host.Mutations;
        reset.Run();
        Assert.Equal(before + 1, host.Mutations);
        Assert.Equal(ClockHours.System, host.State.Preferences.Region.Clock);
        Assert.Equal("Default (Windows)", At<SettingsSubmenu>(host, "language", SettingsMenu.Keys.Formats).Value);
    }

    [Fact]
    public void S3536_the_languages_are_windows_setting_first_then_each_in_its_own_words()
    {
        var host = new FakeHost { WindowsLanguage = Locale.De };
        var display = At<SettingsChoice>(host, "language", "language/display");

        Assert.Equal(41, display.Options.Count);
        Assert.Equal("Default (Deutsch)", display.Options[0].Text);
        Assert.True(display.Options[0].Holds);

        // [S-2372] Following Windows, the line says so in Karpo's one form, with what it comes to (its §518).
        Assert.Equal("Default (Deutsch)", display.Value);
        Assert.Equal("Default (Deutsch)", At<SettingsSubmenu>(host, "language").Value);
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

        At<SettingsChoice>(host, "language", "language/formats", line).Options.Single(o => o.Key == option).Pick();

        Assert.Equal([field], Changed(before, host.State.Preferences.Region));
        Assert.Equal([false], host.RelayoutFlags);
    }

    [Fact]
    public void S3536_each_format_offers_what_windows_says_first()
    {
        var host = new FakeHost();
        foreach (var format in At<SettingsSubmenu>(host, "language", "language/formats").Lines.OfType<SettingsChoice>())
        {
            Assert.StartsWith("Default (", format.Options[0].Text, StringComparison.Ordinal);
            Assert.True(format.Options[0].Holds);

            // [S-2372] While it follows Windows the line says the very words of its option, "Default (…)" (Karpo's §518).
            Assert.Equal(format.Options[0].Text, format.Value);
        }

        At<SettingsChoice>(host, "language", "language/formats", "language/clock").Options.Single(o => o.Key == "language/clock/TwentyFour").Pick();
        Assert.Equal("24-hour", At<SettingsChoice>(host, "language", "language/formats", "language/clock").Value);
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
