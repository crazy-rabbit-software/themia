using Themia.App.Widgets.Meters;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11000]..[S-11009] The system atoms' shared rules: the sizes of a reading, a history, items and facts, and the pick.</summary>
public sealed class SystemAtomsTests
{
    private static readonly ReadoutText Percent = ReadoutRules.Split("100%");

    private static readonly ReadoutText Rate = ReadoutRules.Split("1023 KB/s");

    public static TheoryData<MeterLook> ReadingLooks() =>
        [MeterLook.Number, MeterLook.Numbers, MeterLook.Gauge, MeterLook.Ring, MeterLook.Rows, MeterLook.Battery, MeterLook.Signal];

    [Theory]
    [MemberData(nameof(ReadingLooks))]
    public void S11001_every_drawing_of_a_reading_holds_and_stays_within_320_wide(MeterLook look)
    {
        foreach (var worst in new[] { Percent, Rate, Percent with { Lead = true } })
        {
            var sizing = MeterSizing.Reading(look, worst, rows: 2);
            Assert.Empty(sizing.Problems());
            // [S-12833] The Number look's least is its figures alone at 9 in the inset.
            Assert.True(look == MeterLook.Number ? sizing.Min.Width == Math.Min(MeterSizing.ReadingMinWidth, Math.Ceiling(MeterSizing.Face(worst).LeastBox().Width + 24)) : sizing.Min.Width >= MeterSizing.ReadingMinWidth);
            // [S-12833] The reading alone holds at its natural width and draws scaled down under it.
            var held = look == MeterLook.Number ? sizing.Natural.Width : sizing.Min.Width;
            Assert.True(held >= MeterSizing.ReadingWidth(MeterSizing.Identified(look, worst)) + 24, $"{look}: the reading is cut at its least");
            Assert.True(sizing.Max.Width <= 480);
        }
    }

    [Fact]
    public void S11001_the_number_is_the_header_the_reading_at_28_and_the_inset_and_two_stand_8_apart()
    {
        var one = MeterSizing.Reading(MeterLook.Number, Percent);

        // [S-12833] [S-15752] The reading alone is 60 tall at its natural size and scales down as one, its glyph, figures and
        // unit together, to its unit at 9 in the inset; nothing gives way.
        var face = MeterSizing.Face(Percent).LeastBox();
        Assert.Equal((Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(face.Height + 24)), 60), (one.Min.Height, one.Max.Height));
        Assert.InRange(one.Min.Height, 33, 45);
        Assert.Equal(MeterSizing.Face(Percent).Sizes.Min(), MeterSizing.Face(Percent).Main);
        var least = Math.Max(120, Math.Ceiling((MeterSizing.ReadingWidth(Percent with { Lead = true }) + 24) / 4) * 4);
        Assert.Equal((Math.Min(least, Math.Ceiling(face.Width + 24)), 160, 480), (one.Min.Width, one.Natural.Width, one.Max.Width));

        // [S-15748] The rows right under the reading's line (its first ink on the content's top), at their one size, to their foot.
        Assert.Equal(12 + 36 - LineInk.Of(FamilyType.Big28).Over + 60, MeterSizing.Reading(MeterLook.Rows, Percent, rows: 2).Natural.Height, 6);
    }

    [Theory]
    [InlineData(MeterLook.Number, true)]
    [InlineData(MeterLook.Gauge, true)]
    [InlineData(MeterLook.Rows, true)]
    [InlineData(MeterLook.Signal, true)]
    [InlineData(MeterLook.Numbers, true)]
    [InlineData(MeterLook.Battery, false)]
    [InlineData(MeterLook.Ring, false)]
    public void S12801_a_reading_leads_with_its_kinds_glyph_unless_its_look_draws_the_charge(MeterLook look, bool leads)
    {
        // A bare "31 %" in a group under one Header says which meter it is by its glyph, never by a word.
        Assert.Equal(leads, MeterSizing.Leads(look));
        Assert.Equal(leads, MeterSizing.Identified(look, Percent).Lead);
        Assert.Equal(leads, MeterSizing.Identified(look, Percent with { Lead = true }).Lead);

        // The sizing counts the glyph where it is drawn: never narrower than the reading with it.
        var sizing = MeterSizing.Reading(look, Percent, rows: 2);
        var held = look == MeterLook.Number ? sizing.Natural.Width : sizing.Min.Width; // [S-12833] the reading alone scales down
        Assert.True(held >= MeterSizing.ReadingWidth(MeterSizing.Identified(look, Percent)) + 24, $"{look}: the glyph and the reading fit");
    }

    [Fact]
    public void S6264_both_directions_stand_on_one_line_as_tall_as_one_reading_and_never_cut()
    {
        var both = MeterSizing.Reading(MeterLook.Numbers, Rate);
        var one = MeterSizing.Reading(MeterLook.Number, Rate);
        Assert.True(both.FixedHeight);
        Assert.Equal(one.Natural.Height, both.Natural.Height);

        // Two arrow-led readings and the gap between them fit inside the card's inset at its least.
        var line = (2 * MeterSizing.ReadingWidth(Rate with { Lead = true })) + MeterSizing.NumbersGap;
        Assert.Equal(line, MeterSizing.NumbersWidth(Rate));
        Assert.True(both.Min.Width >= line + 24);
        Assert.True(both.Min.Width < line + 24 + 4, "the least is the line itself, up to the next 4");
        Assert.Equal(both.Min.Width, both.Natural.Width);
        Assert.Equal(MeterSizing.NumbersMaxWidth, both.Max.Width);
    }

    [Fact]
    public void S11003_a_dial_at_its_least_holds_the_reading_and_the_box_gives_the_largest_that_fits()
    {
        var (gauge, _) = MeterSizing.GaugeDiameters(Percent);
        Assert.True(gauge - 48 >= MeterSizing.ReadingWidth(Percent));
        Assert.True((gauge / 2) - 8 >= 36);
        Assert.Equal(0, gauge % 4);

        var (ring, _) = MeterSizing.RingDiameters(Percent);
        Assert.True(ChartRules.Hole(ring).Width >= MeterSizing.ReadingWidth(Percent));

        Assert.Equal(100, MeterSizing.GaugeIn(100, 200));
        Assert.Equal(98, MeterSizing.GaugeIn(300, 50));
        Assert.Equal(80, MeterSizing.RingIn(120, 80));
    }

    [Fact]
    public void S11004_a_picture_takes_the_room_under_the_reading_up_to_its_most()
    {
        Assert.Equal(40, MeterSizing.BatteryIn(200, 44 + 40).Height);
        Assert.Equal(MeterSizing.BodyMax, MeterSizing.BatteryIn(400, 400).Height);
        Assert.Equal(30, MeterSizing.BatteryIn(64, 400).Height);
        Assert.Equal(32, MeterSizing.SignalIn(200, 44 + 32));
    }

    [Fact]
    public void S11005_items_hold_and_S11006_rows_of_items_follow_their_count()
    {
        foreach (var sizing in new[] { MeterSizing.Columns, MeterSizing.Tiles, MeterSizing.Graphs, MeterSizing.ItemRows(4) })
        {
            Assert.Empty(sizing.Problems());
        }

        // [S-14500] Four rows at their optimal (30) edge to edge; every cell taller too, the rows stretched to fill it.
        Assert.Equal(4 * 30, MeterSizing.ItemRows(4).Natural.Height);
        Assert.Equal(4 * 30, MeterSizing.ItemRows(4).Min.Height);
        Assert.Equal(137, Themia.Sdk.SizeStretch.Relax(MeterSizing.ItemRows(4)).FitHeight(137));
    }
}
