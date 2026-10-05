using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The palette's parser and its three notations. Everything a swatch copies goes through here, and
/// a palette is user input twice over — typed into settings and then read back off disk — so the
/// malformed cases matter as much as the good ones.
/// </summary>
public class ColorFormatsTests
{
    // -----------------------------------------------------------------------------------------
    // Parsing: the notations that are accepted
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("#0078D4", 0, 120, 212)]
    [InlineData("0078d4", 0, 120, 212)]                 // the hash is optional, as in HexColor
    [InlineData("#abc", 170, 187, 204)]                 // the three-digit shorthand
    [InlineData("  #0078D4  ", 0, 120, 212)]            // surrounding space is not a malformed colour
    [InlineData("rgb(0, 120, 212)", 0, 120, 212)]
    [InlineData("rgb(0,120,212)", 0, 120, 212)]         // commas without spaces
    [InlineData("rgb(0 120 212)", 0, 120, 212)]         // the modern space-separated syntax
    [InlineData("RGB(0, 120, 212)", 0, 120, 212)]       // the function name is not case sensitive
    [InlineData("rgba(0, 120, 212, 0.5)", 0, 120, 212)] // the alpha is read and then dropped
    [InlineData("hsl(0, 0%, 100%)", 255, 255, 255)]
    [InlineData("hsl(0, 0%, 0%)", 0, 0, 0)]
    [InlineData("hsl(0, 100%, 50%)", 255, 0, 0)]
    [InlineData("hsl(120, 100%, 50%)", 0, 255, 0)]
    [InlineData("hsl(240, 100%, 50%)", 0, 0, 255)]
    [InlineData("hsla(240, 100%, 50%, 0.2)", 0, 0, 255)]
    public void A_colour_in_any_supported_notation_parses(string text, int r, int g, int b)
    {
        Assert.True(ColorFormats.TryParse(text, out var color));
        Assert.Equal(new RgbColor((byte)r, (byte)g, (byte)b), color);
    }

    // -----------------------------------------------------------------------------------------
    // Parsing: everything that is not a colour
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("blue")]                        // named colours are deliberately not supported
    [InlineData("#")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#gggggg")]
    [InlineData("rgb(0, 120)")]                 // too few channels
    [InlineData("rgb(0, 120, 212, 4, 5)")]      // too many
    [InlineData("rgb(0, 120, 300)")]            // out of range is malformed, not clamped
    [InlineData("rgb(-1, 120, 212)")]
    [InlineData("rgb(a, b, c)")]
    [InlineData("rgb(0, 120, 212")]             // unclosed
    [InlineData("0, 120, 212")]                 // bare numbers are not a notation
    [InlineData("rgbx(0, 120, 212)")]           // the function name must be exact
    [InlineData("hsl(120, 100, 50)")]           // saturation and lightness need their percent or a fraction
    [InlineData("hsl(120, 150%, 50%)")]
    [InlineData("hsl()")]
    [InlineData("javascript:alert(1)")]
    public void Anything_that_is_not_a_colour_is_refused_rather_than_guessed(string? text)
    {
        Assert.False(ColorFormats.TryParse(text, out var color));
        Assert.Equal(default, color);
    }

    // -----------------------------------------------------------------------------------------
    // Formatting
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(ColorNotation.Hex, "#0078D4")]
    [InlineData(ColorNotation.Rgb, "rgb(0, 120, 212)")]
    [InlineData(ColorNotation.Hsl, "hsl(206, 100%, 42%)")]
    public void Each_notation_writes_the_shape_it_promises(ColorNotation notation, string expected)
        => Assert.Equal(expected, ColorFormats.Format(new RgbColor(0, 120, 212), notation));

    [Fact]
    public void An_unknown_notation_falls_back_to_hex_rather_than_writing_nothing()
        => Assert.Equal("#0078D4", ColorFormats.Format(new RgbColor(0, 120, 212), (ColorNotation)99));

    [Theory]
    [InlineData(ColorNotation.Hex)]
    [InlineData(ColorNotation.Rgb)]
    [InlineData(ColorNotation.Hsl)]
    public void Everything_this_writes_it_can_read_back(ColorNotation notation)
    {
        // Not every colour survives the hsl rounding exactly, so this pins the weaker but real
        // guarantee: a value this writes is one it accepts.
        foreach (var seed in new[] { "#0078D4", "#64748B", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#000000", "#FFFFFF" })
        {
            Assert.True(ColorFormats.TryParse(seed, out var color));
            var written = ColorFormats.Format(color, notation);

            Assert.True(ColorFormats.TryParse(written, out _), $"could not read back {written}");
        }
    }

    // -----------------------------------------------------------------------------------------
    // Conversion
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Rgb_survives_a_trip_through_hsl_and_back()
    {
        // A deterministic sweep rather than a random one: the same colours fail on every run.
        for (var r = 0; r < 256; r += 17)
        {
            for (var g = 0; g < 256; g += 37)
            {
                for (var b = 0; b < 256; b += 51)
                {
                    var original = new RgbColor((byte)r, (byte)g, (byte)b);
                    var round = ColorFormats.FromHsl(ColorFormats.ToHsl(original));

                    Assert.Equal(original, round);
                }
            }
        }
    }

    [Fact]
    public void A_grey_has_no_hue_and_no_saturation()
    {
        var hsl = ColorFormats.ToHsl(new RgbColor(128, 128, 128));

        Assert.Equal(0, hsl.H);
        Assert.Equal(0, hsl.S);
        Assert.Equal(128 / 255.0, hsl.L, 6);
    }

    [Theory]
    [InlineData(720, 255, 0, 0)]    // a hue past a full turn wraps
    [InlineData(-120, 0, 0, 255)]   // and so does a negative one
    public void An_out_of_range_hue_wraps_rather_than_failing(double hue, int r, int g, int b)
        => Assert.Equal(new RgbColor((byte)r, (byte)g, (byte)b), ColorFormats.FromHsl(new HslColor(hue, 1, 0.5)));

    [Fact]
    public void A_non_finite_component_becomes_a_colour_rather_than_a_NaN_on_the_render_path()
    {
        var color = ColorFormats.FromHsl(new HslColor(double.NaN, double.PositiveInfinity, double.NaN));

        Assert.Equal(new RgbColor(0, 0, 0), color);
    }

    // -----------------------------------------------------------------------------------------
    // Legibility
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_light_swatch_asks_for_dark_text_and_a_dark_one_for_light()
    {
        Assert.True(ColorFormats.WantsDarkText(new RgbColor(255, 255, 255)));
        Assert.True(ColorFormats.WantsDarkText(new RgbColor(231, 229, 228)));   // Stone
        Assert.False(ColorFormats.WantsDarkText(new RgbColor(0, 0, 0)));
        Assert.False(ColorFormats.WantsDarkText(new RgbColor(15, 23, 42)));     // Ink
    }

    [Fact]
    public void Luminance_runs_from_black_to_white()
    {
        Assert.Equal(0, ColorFormats.RelativeLuminance(new RgbColor(0, 0, 0)), 6);
        Assert.Equal(1, ColorFormats.RelativeLuminance(new RgbColor(255, 255, 255)), 6);
    }
}
