using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3738] A time of day and a date typed on the properties menu: either clock, the year or the day first, nothing guessed.</summary>
public sealed class TypedTimeTests
{
    [Theory]
    [InlineData("7:30", 450)]
    [InlineData("07:30", 450)]
    [InlineData("19.05", 1145)]
    [InlineData("7", 420)]
    [InlineData("7:30 pm", 1170)]
    [InlineData("7PM", 1140)]
    [InlineData("12 am", 0)]
    [InlineData("12:15pm", 735)]
    [InlineData(" 0:00 ", 0)]
    [InlineData("23:59", 1439)]
    public void S3738_a_typed_time_is_read_in_either_clock(string typed, int minutes)
        => Assert.Equal(minutes, TypedTime.TimeOfDay(typed));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("24:00")]
    [InlineData("7:60")]
    [InlineData("7:5")]
    [InlineData("13 pm")]
    [InlineData("0 am")]
    [InlineData("seven")]
    [InlineData("7:30:00")]
    [InlineData("-7")]
    [InlineData("123")]
    [InlineData("+7:30")]
    public void S3738_any_other_time_is_refused(string? typed)
        => Assert.Null(TypedTime.TimeOfDay(typed));

    [Theory]
    [InlineData("2026-12-24", 2026, 12, 24)]
    [InlineData("2026/1/5", 2026, 1, 5)]
    [InlineData("24.12.2026", 2026, 12, 24)]
    [InlineData("5.1.26", 2026, 1, 5)]
    [InlineData(" 2028-02-29 ", 2028, 2, 29)]
    public void S3738_a_typed_date_is_read_with_the_year_or_the_day_first(string typed, int year, int month, int day)
        => Assert.Equal(new DateOnly(year, month, day), TypedTime.Date(typed));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12/11/2026")]
    [InlineData("2026-13-01")]
    [InlineData("2027-02-29")]
    [InlineData("31.4.2026")]
    [InlineData("0000-01-01")]
    [InlineData("24.12.202")]
    [InlineData("tomorrow")]
    [InlineData("2026-12")]
    [InlineData("2026-12-24 18:00")]
    [InlineData("-2026-12-24")]
    public void S3738_any_other_date_is_refused(string? typed)
        => Assert.Null(TypedTime.Date(typed));

    [Fact]
    public void S3738_a_date_written_back_reads_as_itself()
    {
        var day = new DateOnly(2031, 3, 7);
        Assert.Equal("2031-03-07", TypedTime.DateText(day));
        Assert.Equal(day, TypedTime.Date(TypedTime.DateText(day)));
    }
}
