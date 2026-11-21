using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-14930]..[S-14935] The text widget's base rules: its size, its room, its words for none and what it opens.</summary>
public sealed class TextRulesTests
{
    private sealed class Text : WidgetConfig
    {
        public override WidgetConfig Clone() => new Text();
    }

    private static TextKind Kind(int lines = 1, bool lead = true)
        => new("t", "k", Glyphs.Music, LocalText.Fixed("T"), LocalText.Fixed("D"), [], () => new Text()) { Lines = lines, Lead = lead };

    // ---- [S-14930] the size -----------------------------------------------------------------------------

    [Fact]
    public void S14930_a_new_card_is_its_line_in_the_inset_160_to_420_wide()
    {
        var sizing = TextRules.Sizing(Kind());

        Assert.Equal(new SizeDip(240, 12 + 28 + 12), sizing.Natural);
        Assert.Equal(new SizeDip(420, 52), sizing.Max);
        Assert.Equal(12 + 56 + 12, TextRules.Sizing(Kind(lines: 2)).Natural.Height);
    }

    [Fact]
    public void S14930_it_scales_down_to_its_line_at_the_legibility_floor_never_under_the_least_widget()
    {
        var sizing = TextRules.Sizing(Kind());
        var least = Math.Max(WidgetSizing.LeastScaled, Math.Ceiling((TextRules.Ink(1) * ScaleFloor.Text / FamilyType.TitleSize) + 24));

        Assert.Equal(least, sizing.Min.Height);
        Assert.True(sizing.Min.Height < sizing.Natural.Height);
        Assert.True(sizing.Min.Width < TextRules.MinWidth);
    }

    [Fact]
    public void S14930_its_lines_are_one_to_three()
    {
        Assert.Equal(1, TextRules.Lines(0));
        Assert.Equal(2, TextRules.Lines(2));
        Assert.Equal(TextRules.MostLines, TextRules.Lines(9));
        Assert.Equal(TextRules.Sizing(Kind(lines: 3)).Natural, TextRules.Sizing(Kind(lines: 7)).Natural);
    }

    // ---- [S-14931] the line's room ------------------------------------------------------------------------

    [Fact]
    public void S14931_at_its_own_size_or_larger_the_line_takes_the_room_less_its_lead()
    {
        var lead = TextRules.LeadRoom(lead: true);

        Assert.Equal(20 + FamilySpace.GlyphGap, lead);
        Assert.Equal(216 - lead, TextRules.Room(216, 28, 1, lead: true));
        Assert.Equal(216 - lead, TextRules.Room(216, 400, 1, lead: true));
        Assert.Equal(216, TextRules.Room(216, 400, 1, lead: false));
    }

    [Fact]
    public void S14931_a_shorter_room_scales_the_face_down_so_its_line_may_be_as_long_as_the_width_holds_at_that_scale()
    {
        var half = TextRules.Ink(1) / 2;

        Assert.Equal(432, TextRules.Room(216, half, 1, lead: false), 6);

        // Never past the legibility floor's scale: 9 of the step's 20.
        Assert.Equal(216 / ScaleFloor.Least(FamilyType.TitleSize), TextRules.Room(216, 1, 1, lead: false), 6);
    }

    [Fact]
    public void S14931_a_room_not_laid_out_is_unbounded()
    {
        Assert.Equal(double.PositiveInfinity, TextRules.Room(0, 28, 1, lead: true));
        Assert.Equal(double.PositiveInfinity, TextRules.Room(216, double.NaN, 1, lead: true));
    }

    [Fact]
    public void S14931_somebody_elses_text_is_one_line()
    {
        Assert.Equal("Rain in 15 min", TextRules.Line("  Rain \r\n in\t15   min "));
        Assert.Equal(string.Empty, TextRules.Line(" \n "));
        Assert.Equal(string.Empty, TextRules.Line(null));
    }

    // ---- [S-14932] none --------------------------------------------------------------------------------------

    [Fact]
    public void S14932_no_text_is_the_kinds_words_for_none_unavailable()
    {
        Assert.Equal(("No album", false), TextRules.Drawn(TextShown.None("Nothing playing"), "No album"));
        Assert.Equal(("No album", false), TextRules.Drawn(new TextShown("   "), "No album"));
    }

    [Fact]
    public void S14932_a_text_is_drawn_as_its_read_says()
    {
        Assert.Equal(("Long Winter", true), TextRules.Drawn(new TextShown("Long  Winter"), "No album"));
        Assert.Equal(("Nothing playing", false), TextRules.Drawn(new TextShown("Nothing playing", false), string.Empty));
    }

    // ---- [S-14934] the kind ----------------------------------------------------------------------------------

    [Fact]
    public void S14934_a_kind_knows_its_config_type_and_states_its_lead_and_tone()
    {
        var kind = Kind();

        Assert.Equal(typeof(Text), kind.ConfigType);
        Assert.True(kind.Lead);
        Assert.Equal(TextTone.Primary, kind.Tone);
        Assert.Equal(FamilyType.Title, TextRules.StepOf(TextTone.Primary));
        Assert.False(TextRules.StepOf(TextTone.Secondary).SemiBold);
    }

    // ---- [S-14935] what a click opens ------------------------------------------------------------------------

    [Fact]
    public void S14935_the_player_is_the_app_the_text_came_from_and_nothing_without_one()
    {
        var open = TextOpens.Player.Target(new Text(), new TextShown("Northern Lights") { Owner = "Spotify.exe" });

        Assert.Equal(CardOpenKind.App, open?.Kind);
        Assert.Equal("Spotify.exe", open?.Value);
        Assert.Null(TextOpens.Player.Target(new Text(), new TextShown("Northern Lights")));
        Assert.Null(TextOpens.Player.Target(new Text(), new TextShown("x") { Owner = "a&b" }));
    }

    [Fact]
    public void S14935_the_weather_is_the_metrics_forecast_for_the_place()
    {
        Assert.Equal(MetricOpens.Weather.Label, TextOpens.Weather.Label);
        Assert.Null(TextOpens.Weather.Target(new Text(), new TextShown("Rain in 15 min")));
    }
}
