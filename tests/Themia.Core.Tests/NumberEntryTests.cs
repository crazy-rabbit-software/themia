using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2309] A number typed into a page's number box, read in the units the box shows.</summary>
public sealed class NumberEntryTests
{
    [Theory]
    [InlineData("42", 42d)]
    [InlineData("42 %", 42d)]
    [InlineData("1.5 s", 1.5)]
    [InlineData("1,5 s", 1.5)]
    [InlineData("12px", 12d)]
    [InlineData("-3", -3d)]
    [InlineData("  7 ", 7d)]
    [InlineData("1 200", 1200d)]
    public void S2309_reads_the_number_in_what_was_typed(string typed, double expected)
        => Assert.Equal(expected, NumberEntry.Read(typed));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData(null)]
    public void S2309_no_digit_is_no_number(string? typed)
        => Assert.Null(NumberEntry.Read(typed));

    [Fact]
    public void S2309_a_typed_value_is_in_the_units_the_box_shows()
    {
        static string Percent(double fraction) => $"{Math.Round(fraction * 100)} %";
        static string Seconds(double ms) => $"{ms / 1000:0.0} s";
        static string Px(double v) => $"{v}px";

        Assert.Equal(0.42, NumberEntry.Value("42", 1, Percent)!.Value, 6);
        Assert.Equal(1500, NumberEntry.Value("1.5", 5000, Seconds)!.Value, 6);
        Assert.Equal(12, NumberEntry.Value("12 px", 32, Px));
        Assert.Null(NumberEntry.Value("x", 32, Px));
    }
}
