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

    private sealed class Facts : FactsConfig
    {
        public static readonly IReadOnlyList<FactOption> All =
        [
            new("a", Str.Details_Cpu_Speed.Local()),
            new("b", Str.Details_Cpu_Cores.Local()),
            new("c", Str.Details_Cpu_Threads.Local()),
            new("d", Str.Details_Cpu_Handles.Local()),
            new("e", Str.Details_Cpu_Uptime.Local()),
            new("f", Str.Details_Cpu_Processes.Local()),
        ];

        public Facts()
            : base(All)
        {
        }

        public override WidgetConfig Clone()
        {
            var copy = new Facts();
            CopyTo(copy);
            return copy;
        }
    }

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
            Assert.True(sizing.Min.Width >= MeterSizing.ReadingMinWidth);
            Assert.True(sizing.Min.Width >= MeterSizing.ReadingWidth(MeterSizing.Identified(look, worst)) + 24, $"{look}: the reading is cut at its least");
            Assert.True(sizing.Max.Width <= 480);
        }
    }

    [Fact]
    public void S11001_the_number_is_the_header_the_reading_at_28_and_the_inset_and_two_stand_8_apart()
    {
        var one = MeterSizing.Reading(MeterLook.Number, Percent);
        Assert.Equal((60, 60), (one.Min.Height, one.Max.Height));
        var least = Math.Max(120, Math.Ceiling((MeterSizing.ReadingWidth(Percent with { Lead = true }) + 24) / 4) * 4);
        Assert.Equal((least, 160, 480), (one.Min.Width, one.Natural.Width, one.Max.Width));

        Assert.Equal(12 + 44 + 56 + 12, MeterSizing.Reading(MeterLook.Rows, Percent, rows: 2).Natural.Height);
    }

    [Theory]
    [InlineData(MeterLook.Number, true)]
    [InlineData(MeterLook.Graph, true)]
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
        Assert.True(sizing.Min.Width >= MeterSizing.ReadingWidth(MeterSizing.Identified(look, Percent)) + 24, $"{look}: the glyph and the reading fit");
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
    public void S11002_a_history_is_160_to_640_by_48_to_200()
        => Assert.Equal((new SizeDip(160, 48), new SizeDip(160, 64), new SizeDip(640, 200)), (MeterSizing.History.Min, MeterSizing.History.Natural, MeterSizing.History.Max));

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

        Assert.Equal(12 + (4 * 28) + 12, MeterSizing.ItemRows(4).Natural.Height);
        Assert.True(MeterSizing.ItemRows(4).FixedHeight);
    }

    [Fact]
    public void S11007_facts_start_with_the_first_four_and_their_height_follows_the_pick()
    {
        var facts = new Facts();
        Assert.Equal(["a", "b", "c", "d"], facts.Facts);

        var sizing = FactsRules.Sizing(facts);
        Assert.Empty(sizing.Problems());
        Assert.Equal((12 + 28 + 12, 12 + (4 * 28) + 12, 12 + (6 * 28) + 12), (sizing.Min.Height, sizing.Natural.Height, sizing.Max.Height));
        Assert.Equal((200, 260, 420), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S11007_a_dragged_height_keeps_the_first_facts_or_adds_the_next_in_the_kinds_order()
    {
        var facts = new Facts { Facts = ["e", "a"] };

        var grown = Assert.IsType<Facts>(FactsRules.SizeSets(facts, new SizeDip(240, 12 + (4 * 28) + 12)));
        Assert.Equal(["e", "a", "b", "c"], grown.Facts);
        Assert.Equal(["e", "a"], facts.Facts);

        var cut = Assert.IsType<Facts>(FactsRules.SizeSets(grown, new SizeDip(240, 10)));
        Assert.Equal(["e"], cut.Facts);

        Assert.Same(facts, FactsRules.SizeSets(facts, new SizeDip(400, 12 + (2 * 28) + 12)));
        Assert.Equal(["a", "b", "c", "d", "e", "f"], FactsRules.Resize(["a"], Facts.All, 99));
    }

    [Fact]
    public void S11007_the_pick_reads_the_sheet_by_key_and_keeps_a_row_for_a_fact_not_read()
    {
        var sheet = new DetailSheet(null, null, [new DetailFact("Cores", "8", Key: "b"), new DetailFact("Speed", "3 GHz", Key: "a")], []);
        var rows = FactsRules.Pick(Locale.En, sheet, ["a", "c", "b", "zz"], Facts.All);

        Assert.Equal(["3 GHz", "--", "8"], rows.Select(r => r.Value));
        Assert.Equal([true, false, true], rows.Select(r => r.Available));
        Assert.Equal("Threads", rows[1].Label);
    }

    [Fact]
    public void S11007_unknown_and_repeated_keys_are_repaired_and_none_left_is_the_defaults()
    {
        var facts = new Facts { Facts = ["b", "zz", "b", "a"] };
        Assert.NotEmpty(facts.Repair());
        Assert.Equal(["b", "a"], facts.Facts);
        Assert.Empty(facts.Repair());

        var none = new Facts { Facts = ["zz"] };
        Assert.NotEmpty(none.Repair());
        Assert.Equal(["a", "b", "c", "d"], none.Facts);
    }
}
