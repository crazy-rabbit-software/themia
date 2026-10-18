using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Clock.Tests;

/// <summary>The Clock's word style ([S-6020]..[S-6024]).</summary>
public sealed class WordClockTests
{
    [Theory]
    [InlineData(10, 0, 10, 0)]
    [InlineData(10, 2, 10, 0)]
    [InlineData(10, 3, 10, 5)]
    [InlineData(10, 13, 10, 15)]
    [InlineData(10, 57, 10, 55)]
    [InlineData(10, 58, 11, 0)]
    [InlineData(23, 58, 0, 0)]
    public void S6021_a_time_reads_as_the_nearest_five_minutes(int hour, int minute, int saidHour, int saidMinute)
        => Assert.Equal((saidHour, saidMinute), WordClock.Round(hour, minute));

    [Theory]
    [InlineData(10, 0, "It's ten o'clock")]
    [InlineData(10, 15, "It's quarter past ten")]
    [InlineData(10, 30, "It's half past ten")]
    [InlineData(10, 35, "It's twenty-five to eleven")]
    [InlineData(10, 45, "It's quarter to eleven")]
    [InlineData(0, 5, "It's five past twelve")]
    [InlineData(12, 55, "It's five to one")]
    [InlineData(23, 58, "It's twelve o'clock")]
    public void S6020_english(int hour, int minute, string expected)
        => Assert.Equal(expected, WordClock.Compose(Locale.En, hour, minute));

    [Theory]
    [InlineData(1, 0, "Es ist ein Uhr")]
    [InlineData(1, 5, "Es ist fünf nach eins")]
    [InlineData(10, 15, "Es ist Viertel nach zehn")]
    [InlineData(10, 25, "Es ist fünf vor halb elf")]
    [InlineData(10, 30, "Es ist halb elf")]
    [InlineData(10, 45, "Es ist Viertel vor elf")]
    [InlineData(23, 55, "Es ist fünf vor zwölf")]
    public void S6020_german_says_half_of_the_next_hour_and_ein_uhr(int hour, int minute, string expected)
        => Assert.Equal(expected, WordClock.Compose(Locale.De, hour, minute));

    [Fact]
    public void S6020_spanish_agrees_the_verb_with_the_hour()
    {
        Assert.StartsWith("Es la una", WordClock.Compose(Locale.Es, 1, 15), StringComparison.Ordinal);
        Assert.StartsWith("Son las diez", WordClock.Compose(Locale.Es, 10, 15), StringComparison.Ordinal);
    }

    [Fact]
    public void S6020_french_says_midi_and_quarter_to_the_next_hour()
    {
        Assert.Contains("midi", WordClock.Compose(Locale.Fr, 12, 0), StringComparison.Ordinal);
        Assert.Contains("onze heures moins le quart", WordClock.Compose(Locale.Fr, 10, 45), StringComparison.Ordinal);
    }

    [Fact]
    public void S6020_japanese_counts_in_its_own_numerals_with_the_part_of_the_day()
    {
        var text = WordClock.Compose(Locale.Ja, 22, 15);
        Assert.Contains("午後", text, StringComparison.Ordinal);
        Assert.DoesNotContain(text, char.IsAsciiDigit);
    }

    [Fact]
    public void S6022_the_sentence_starts_with_a_capital()
    {
        Assert.True(char.IsUpper(WordClock.Compose(Locale.Ru, 10, 15)[0]));
        Assert.True(char.IsUpper(WordClock.Compose(Locale.Fil, 10, 15)[0]));
    }

    [Fact]
    public void S6023_every_language_has_whole_word_clock_strings()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            Assert.Null(WordClock.Problem(locale));
        }
    }

    [Fact]
    public void S6023_every_language_fills_every_step_of_the_day_with_no_marker_left()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            var said = new HashSet<string>(StringComparer.Ordinal);
            for (var hour = 0; hour < 24; hour++)
            {
                for (var minute = 0; minute < 60; minute += 5)
                {
                    var text = WordClock.Compose(locale, hour, minute);
                    Assert.False(string.IsNullOrWhiteSpace(text));
                    Assert.DoesNotContain('[', text);
                    Assert.DoesNotContain('{', text);
                    said.Add(text);
                }
            }

            // A 12-hour language says each sentence twice a day, a 24-hour one once: never fewer.
            Assert.True(said.Count >= 144, $"{locale}: only {said.Count} different sentences in a day");
        }
    }

    [Fact]
    public void S6023_a_malformed_hour_list_is_refused()
    {
        Assert.Null(WordClock.Parse("one|two"));
        Assert.Null(WordClock.Parse("a|b|c|d|e|f|g|h|i|j|k|"));
        Assert.NotNull(WordClock.Parse("a|b|c|d|e|f|g|h|i|j|k|l;a|b|c|d|e|f|g|h|i|j|k|l|m|n|o|p|q|r|s|t|u|v|w|x"));
    }

    [Fact]
    public void S6024_the_type_fits_the_longest_sentence_and_grows_with_the_box()
    {
        var sample = WordClock.Longest(Locale.En);
        Assert.True(sample.Length >= "It's twenty-five past eleven".Length - 2);

        var small = WordClock.Fit(150, 80, sample, FluentType.Caption);
        var large = WordClock.Fit(600, 400, sample, FluentType.Caption);

        Assert.True(large.FontSize > small.FontSize);
        Assert.True(WordClock.Lines(sample, 150, small) * small.LineHeight <= 80);
        Assert.True(WordClock.Lines(sample, 600, large) * large.LineHeight <= 400);
    }

    [Fact]
    public void S6024_text_without_spaces_wraps_by_its_characters()
    {
        var step = FluentType.Body;
        var text = new string('時', 20);

        Assert.True(WordClock.Lines(text, FluentType.EstimateWidth(step, 1.75 * 5), step) >= 4);
    }

    [Fact]
    public void S6013_the_clock_says_the_words_in_its_own_zone()
    {
        var config = new ClockConfig { TimeZoneId = "Asia/Tokyo", Style = ClockStyle.Word };
        var utc = new DateTimeOffset(2026, 1, 15, 1, 15, 0, TimeSpan.Zero); // 10:15 in Tokyo

        Assert.Equal("It's quarter past ten", ClockFormatter.Words(utc, config, Locale.En));
    }
}
