using Themia.Core.Model;
using Themia.Core.Series;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

public class SeriesTests
{
    [Fact]
    public void S15510_a_place_token_round_trips_and_a_bad_one_reads_as_no_value()
    {
        var place = new SeriesPlace(47.3769, 8.5417, "Zürich | Altstadt");
        var token = SeriesTokens.Place(place);

        Assert.Equal(place, SeriesTokens.PlaceOf(token));
        Assert.Null(SeriesTokens.PlaceOf("91|8|North of north"));
        Assert.Null(SeriesTokens.PlaceOf("47.3|nope|Zürich"));
        Assert.Null(SeriesTokens.PlaceOf("47.3|8.5"));
        Assert.Null(SeriesTokens.PlaceOf(null));
    }

    [Fact]
    public void S15510_a_day_token_is_a_whole_number_from_zero()
    {
        Assert.Equal(3, SeriesTokens.DayOf(SeriesTokens.Day(3)));
        Assert.Null(SeriesTokens.DayOf("-1"));
        Assert.Null(SeriesTokens.DayOf("1.5"));
        Assert.Null(SeriesTokens.DayOf(""));
    }

    [Fact]
    public void S15510_a_kind_exposes_a_slot_by_holding_its_contract()
    {
        Assert.Equal([SeriesSlots.Drive], SeriesSlots.Of(new DriveFake()));
        Assert.Equal([SeriesSlots.Place, SeriesSlots.Day], SeriesSlots.Of(new PlaceDayFake()));
        Assert.Empty(SeriesSlots.Of(new DayFake { TakesDay = false }));
        Assert.Empty(SeriesSlots.Of(new PlainFake()));
        Assert.Equal([SeriesSlots.Day], SeriesSlots.Of(new PlaceDayFake(), SeriesType.Day));
    }

    [Fact]
    public void S15510_a_slot_writes_a_value_that_reads_and_refuses_one_that_does_not()
    {
        var atom = new PlaceDayFake { UseDeviceLocation = true };

        Assert.True(SeriesSlots.Place.Write(atom, SeriesTokens.Place(new SeriesPlace(46.948, 7.4474, "Bern"))));
        Assert.Equal(46.948, atom.Latitude);
        Assert.Equal("Bern", atom.PlaceName);
        Assert.False(atom.UseDeviceLocation);

        Assert.True(SeriesSlots.Day.Write(atom, "4"));
        Assert.Equal(4, atom.Day);
        Assert.False(SeriesSlots.Day.Write(atom, "tomorrow"));
        Assert.Equal(4, atom.Day);
        Assert.False(SeriesSlots.Drive.Write(atom, @"D:\"));
    }

    [Fact]
    public void S15513_a_day_range_runs_from_its_start_by_its_step_as_far_as_a_forecast_reaches()
    {
        var repeater = new RepeaterFake { RepeatBy = SeriesType.Day, Start = 1, Step = 3 };

        Assert.Equal(["1", "4", "7", "10", "13"], SeriesSources.ValuesOf(repeater));

        repeater.Step = 0;
        repeater.Start = 12;
        Assert.Equal(["12", "13"], SeriesSources.ValuesOf(repeater));
    }

    [Fact]
    public void S15511_a_repeater_reads_its_chosen_source_of_its_type_else_the_types_default()
    {
        var repeater = new RepeaterFake { RepeatBy = SeriesType.Drive, Values = "placesPicked" };

        Assert.Same(SeriesSources.DrivesConnected, SeriesSources.For(repeater));

        repeater.Values = "drivesPicked";
        repeater.Picked = [@"D:\", @"D:\", "", @"E:\"];
        Assert.Same(SeriesSources.DrivesPicked, SeriesSources.For(repeater));
        Assert.Equal([@"D:\", @"E:\"], SeriesSources.ValuesOf(repeater));

        repeater.RepeatBy = null;
        Assert.Null(SeriesSources.For(repeater));
        Assert.Empty(SeriesSources.ValuesOf(repeater));
    }

    [Fact]
    public void S15513_connected_drives_are_the_ready_and_answering_ones_in_letter_order()
    {
        DriveEntry Drive(string root, DriveState state) => new(root, VolumeKind.Fixed, state);

        var roots = ConnectedDrivesSource.Of(
        [
            Drive(@"A:\", DriveState.NoMedia), Drive(@"C:\", DriveState.Ready), Drive(@"D:\", DriveState.Missing),
            Drive(@"E:\", DriveState.Pending), Drive(@"F:\", DriveState.Disconnected),
        ]);

        Assert.Equal([@"C:\", @"E:\"], roots);
    }
}
