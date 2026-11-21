using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14319] A running reading is held to its format's widest form (owner, 2026-11-20: "the clock right now changes its
/// layout depending on the time that it shows because not all of the chars have the same width").
/// </summary>
public sealed class FigureTemplateTests
{
    [Theory]
    [InlineData("10:42:18", "88:88:88")]
    [InlineData("1:11", "8:88")]
    [InlineData("12:34.5", "88:88.8")]
    [InlineData("--", "--")]
    [InlineData("١٠:٤٢", "٨٨:٨٨")]
    public void S14319_a_template_is_its_text_with_every_digit_its_scripts_eight(string text, string template)
        => Assert.Equal(template, FigureTemplate.Of(text));

    [Theory]
    [InlineData("1:11", "88:88", "\u20071:11")]
    [InlineData("10:08", "88:88", "10:08")]
    [InlineData("9\n41\n18", "88\n88\n88", "\u20079\n41\n18")]
    [InlineData("--", "88:88", "--")]
    [InlineData("3d 5h", "88d 88h", "3d 5h")]
    public void S14319_a_shorter_time_is_padded_at_its_start_to_the_templates_places(string value, string template, string held)
        => Assert.Equal(held, FigureTemplate.Pad(value, template));

    [Fact]
    public void S14319_the_clock_at_1_11_and_at_10_08_is_held_to_one_shape()
    {
        // Every time a 12-hour clock shows is held to one template: padded, each is the template's places exactly, so with
        // tabular figures (a figure space a digit's width) every one is drawn as wide and stood where the template is.
        const string template = "88:88:88";
        foreach (var time in new[] { "1:11:11", "10:08:08", "12:59:59", "9:41:18" })
        {
            var held = FigureTemplate.Pad(time, template);
            Assert.Equal(template.Length, held.Length);
            Assert.Equal(template, FigureTemplate.Of(held));
        }
    }

    [Theory]
    [InlineData("88:88:88", "88888888")]
    [InlineData("88:88.8", "8888888")]
    [InlineData("88d 88h", "88d 88h")]
    [InlineData("\u20078:88", "88888")]
    public void S14319_tabular_figures_set_its_separators_a_figure_wide(string template, string tabular)
        => Assert.Equal(tabular, FigureTemplate.Tabular(template));

    [Fact]
    public void S14319_a_formats_widest_form_is_its_longest_line_by_line()
        => Assert.Equal("88:88:88\n8", FigureTemplate.Widest(["9:41:18\n1", "10:08:08", "1:00"]));
}
