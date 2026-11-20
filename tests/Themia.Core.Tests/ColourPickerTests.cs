using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1574]..[S-1577] The colour picker as Karpo's: its swatches' lines beside its mixer, the recent colours, the presets, the hex box.</summary>
[Collection(GlobalLocaleCollection.Name)]
public sealed class ColourPickerTests
{
    private const double Step = ColourPicker.Swatch + ColourPicker.Gap;

    [Fact]
    public void S1574_a_wide_body_stands_the_mixer_beside_six_swatches_a_line()
    {
        // The page's card: 548 less the words' column (48) and the end inset (16).
        var plan = ColourPicker.Lay(484, first: 3, presets: 12, sliders: 2);

        Assert.True(plan.Beside);
        Assert.Equal(ColourPicker.Columns, plan.Across);
        Assert.Equal(3, plan.FirstShown);

        // The first line from the top; the presets on the two lines under it, six each.
        Assert.All(plan.Swatches.Take(3), spot => Assert.Equal((0, 0d), (spot.Line, spot.Y)));
        Assert.Equal(new SwatchSpot(3, 0, Step, 1), plan.Swatches[3]);
        Assert.Equal(new SwatchSpot(8, 5 * Step, Step, 1), plan.Swatches[8]);
        Assert.Equal(new SwatchSpot(9, 0, 2 * Step, 2), plan.Swatches[9]);
        Assert.Equal(15, plan.Swatches.Count);

        // The mixer from the top, right of the left half and the pad.
        Assert.Equal(Math.Floor((484 - ColourPicker.Pad) / 2) + ColourPicker.Pad, plan.MixerX);
        Assert.Equal(0, plan.MixerY);
        Assert.Equal(484, plan.MixerX + plan.MixerWidth);

        // As tall as its taller column: the mixer (spectrum, gap, two sliders) against three lines of swatches.
        var mixer = ColourPicker.Spectrum + ColourPicker.SpectrumGap + (2 * ColourPicker.Slider);
        Assert.Equal(Math.Max(mixer, (3 * Step) - ColourPicker.Gap), plan.Height);
    }

    [Fact]
    public void S1574_a_narrow_body_stands_the_mixer_under_the_swatches()
    {
        var plan = ColourPicker.Lay(ColourPicker.BesideLeast - 1, first: 1, presets: 12, sliders: 1);

        Assert.False(plan.Beside);
        Assert.Equal(0, plan.MixerX);
        Assert.Equal(ColourPicker.BesideLeast - 1, plan.MixerWidth);
        Assert.Equal((3 * Step) - ColourPicker.Gap + ColourPicker.Pad, plan.MixerY);
        Assert.Equal(plan.MixerY + ColourPicker.Spectrum + ColourPicker.SpectrumGap + ColourPicker.Slider, plan.Height);
    }

    [Fact]
    public void S1574_the_first_line_is_one_line_and_stands_even_when_empty_so_the_presets_never_move()
    {
        var crowded = ColourPicker.Lay(484, first: 2 + ColourPicker.MostRecent, presets: 12, sliders: 1);
        var empty = ColourPicker.Lay(484, first: 0, presets: 12, sliders: 1);

        Assert.Equal(ColourPicker.Columns, crowded.FirstShown);
        Assert.Equal(0, empty.FirstShown);
        Assert.Equal(crowded.Swatches[crowded.FirstShown].Y, empty.Swatches[0].Y);
        Assert.Equal(Step, empty.Swatches[0].Y);
    }

    [Fact]
    public void S1574_fewer_fit_on_a_line_where_the_column_is_narrower()
    {
        var plan = ColourPicker.Lay(4 * Step, first: 1, presets: 12, sliders: 0);

        Assert.Equal(4, plan.Across);
        Assert.Equal(3, plan.Swatches.Max(s => s.Line)); // the first line (0), then twelve in fours: lines 1 to 3
    }

    [Fact]
    public void S1575_the_recent_colours_are_newest_first_each_once_one_line_at_most()
    {
        IReadOnlyList<string> stored = ["#112233", "#445566"];

        var kept = ColourPicker.Remember(stored, "#abcdef");
        Assert.Equal(["#ABCDEF", "#112233", "#445566"], kept);

        // Kept again: it moves to the front, never twice.
        Assert.Equal(["#445566", "#ABCDEF", "#112233"], ColourPicker.Remember(kept, "445566"));

        // A seventh drops the oldest.
        var full = Enumerable.Range(1, ColourPicker.MostRecent).Select(i => $"#0000{i:X2}").ToList();
        var next = ColourPicker.Remember(full, "#FF0000");
        Assert.Equal(ColourPicker.MostRecent, next.Count);
        Assert.Equal("#FF0000", next[0]);
        Assert.DoesNotContain(full[^1], next);

        // What is no colour is not remembered.
        Assert.Equal(["#112233", "#445566"], ColourPicker.Remember(stored, "nope"));
    }

    [Fact]
    public void S1575_a_stored_list_is_read_as_colours_only_each_once_one_line_at_most()
    {
        Assert.Empty(ColourPicker.Recent(null));
        Assert.Equal(["#112233", "#FFFFFF"], ColourPicker.Recent(["#112233", null, "", "zz", "112233", "#fff"]));
        Assert.Equal(ColourPicker.MostRecent, ColourPicker.Recent(Enumerable.Range(0, 20).Select(i => $"#0000{i:X2}")).Count);
    }

    [Fact]
    public void S1576_two_lines_of_presets_twelve_of_the_table_the_picker_names_its_colours_from()
    {
        Assert.Same(ColorPresets.Accents, ColourPicker.Presets(ColorPresets.Windows));
        Assert.Same(ColorPresets.SurfaceSwatches, ColourPicker.Presets(ColorPresets.Surfaces));
        Assert.Equal(ColourPicker.Columns * ColourPicker.PresetLines, ColorPresets.Accents.Count);
        Assert.Equal(ColourPicker.Columns * ColourPicker.PresetLines, ColorPresets.SurfaceSwatches.Count);

        // Every one is a named colour of its table: a preset says its name.
        Assert.All(ColorPresets.Accents, preset => Assert.Contains(preset, ColorPresets.Windows));
        Assert.All(ColorPresets.SurfaceSwatches, preset => Assert.Contains(preset, ColorPresets.Surfaces));
        Assert.Contains(ColorPresets.Accents, preset => preset.Hex == "#0078D4");
        Assert.Contains(ColorPresets.SurfaceSwatches, preset => preset.Hex == "#202020");
        Assert.Contains(ColorPresets.SurfaceSwatches, preset => preset.Hex == "#F3F3F3");

        // Any other table (a module's inks): its first two lines.
        IReadOnlyList<ColorPreset> inks = [.. ColorPresets.Windows.Skip(3)];
        Assert.Equal(inks.Take(12), ColourPicker.Presets(inks));
    }

    [Fact]
    public void S1575_the_recent_colours_are_kept_in_the_preferences_and_a_hand_edited_list_is_repaired()
    {
        var state = AppState.CreateDefault();
        Assert.Empty(state.Preferences.RecentColours);

        state.Preferences.RecentColours = ["#112233", "not a colour", "#112233", "#ABCDEF"];
        var repairs = StateRepair.Repair(state);
        Assert.Equal(["#112233", "#ABCDEF"], state.Preferences.RecentColours);
        Assert.Contains(repairs, r => r.Contains("recent colours", StringComparison.Ordinal));

        // A valid list is no repair; it round-trips and is copied by Clone.
        Assert.DoesNotContain(StateRepair.Repair(state), r => r.Contains("recent colours", StringComparison.Ordinal));
        var json = JsonSerializer.Serialize(state.Preferences, AppStateJsonContext.Default.AppPreferences);
        Assert.Equal(["#112233", "#ABCDEF"], JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppPreferences)!.RecentColours);
        var copy = state.Preferences.Clone();
        Assert.NotSame(state.Preferences.RecentColours, copy.RecentColours);
        Assert.Equal(state.Preferences.RecentColours, copy.RecentColours);

        // A hand-edited null is an empty list.
        state.Preferences.RecentColours = null!;
        StateRepair.Repair(state);
        Assert.Empty(state.Preferences.RecentColours);
    }

    [Theory]
    [InlineData(0, 1, 1, 255, 0, 0)]
    [InlineData(120, 1, 1, 0, 255, 0)]
    [InlineData(240, 1, 1, 0, 0, 255)]
    [InlineData(360, 1, 1, 255, 0, 0)]
    [InlineData(-120, 1, 1, 0, 0, 255)]
    [InlineData(206, 1, 0.83, 0, 120, 212)]
    [InlineData(30, 0, 0.5, 128, 128, 128)]
    [InlineData(30, 2, -1, 0, 0, 0)]
    public void S1574_a_hue_saturation_and_value_are_the_colour_they_stand_for(double h, double s, double v, int r, int g, int b)
        => Assert.Equal(((byte)r, (byte)g, (byte)b), ColourPicker.FromHsv(h, s, v));

    [Theory]
    [InlineData("#0078D4")]
    [InlineData("#E81123")]
    [InlineData("#FFB900")]
    [InlineData("#107C10")]
    [InlineData("#744DA9")]
    [InlineData("#202020")]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    public void S1574_a_colour_round_trips_through_its_hue_saturation_and_value(string hex)
    {
        Assert.True(Themia.Core.Widgets.HexColor.TryParse(hex, out var r, out var g, out var b));
        var (h, s, v) = ColourPicker.ToHsv(r, g, b);
        Assert.Equal((r, g, b), ColourPicker.FromHsv(h, s, v));
    }

    [Fact]
    public void S1574_a_grey_keeps_the_hue_the_slider_stood_at()
    {
        Assert.Equal(200, ColourPicker.ToHsv(128, 128, 128, heldHue: 200).Hue);
        Assert.Equal(0, ColourPicker.ToHsv(0, 0, 0).Saturation);
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(200, 96, 1, 0)]
    [InlineData(100, 48, 0.5, 0.5)]
    [InlineData(-10, 500, 0, 0)]
    [InlineData(900, -5, 1, 1)]
    public void S1574_the_spectrum_is_saturation_across_and_value_up_held_at_its_edges(double x, double y, double s, double v)
        => Assert.Equal((s, v), ColourPicker.SpectrumAt(x, y, 200, 96));

    [Theory]
    [InlineData("#1e3a5f", "#1E3A5F")]
    [InlineData("1E3A5F", "#1E3A5F")]
    [InlineData(" #fff ", "#FFFFFF")]
    [InlineData("#12345", null)]
    [InlineData("#1E3A5FCC", null)]
    [InlineData("blue", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void S1577_the_hex_box_takes_a_colour_with_or_without_its_hash_in_either_case(string? typed, string? colour)
        => Assert.Equal(colour, ColourPicker.Typed(typed));
}
