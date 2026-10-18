using Xunit;
using Themia.Core.Astronomy;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.Tests;

/// <summary>Package PJ: Sun &amp; moon parity ([S-1490], [S-1491], the SDK's astronomy); the clock's, the quote's and the scratchpad's are their modules'.</summary>
public sealed class DeskParityTests
{
    // ---------------------------------------------------------------------------------------------
    // Sun & moon
    // ---------------------------------------------------------------------------------------------

    private static readonly DateTimeOffset ZurichNoonOct = new(2026, 10, 4, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void S1490_civil_twilight_brackets_sunrise_and_sunset()
    {
        var sun = SolarPosition.TimesFor(ZurichNoonOct, 47.37, 8.54);
        var twilight = SolarPosition.CivilTwilightFor(ZurichNoonOct, 47.37, 8.54);

        Assert.Equal(PolarState.None, twilight.Polar);
        Assert.True(twilight.Sunrise < sun.Sunrise);
        Assert.True(twilight.Sunset > sun.Sunset);

        // About half an hour at this latitude in October.
        var dawnGap = (sun.Sunrise!.Value - twilight.Sunrise!.Value).TotalMinutes;
        Assert.InRange(dawnGap, 25, 40);
    }

    [Fact]
    public void S1490_white_nights_have_no_civil_dusk()
    {
        // Saint Petersburg-ish, but further north: 65°N at midsummer never goes 6° under.
        var twilight = SolarPosition.CivilTwilightFor(new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero), 65, 25);

        Assert.Equal(PolarState.Day, twilight.Polar);
        Assert.Null(twilight.Sunrise);
    }

    [Fact]
    public void S1490_solar_noon_sits_between_sunrise_and_sunset_at_the_noon_altitude()
    {
        var sun = SolarPosition.TimesFor(ZurichNoonOct, 47.37, 8.54);
        var noon = SolarPosition.NoonFor(ZurichNoonOct, 47.37, 8.54);

        Assert.True(noon.At > sun.Sunrise && noon.At < sun.Sunset);
        Assert.InRange((noon.At - sun.Sunrise!.Value) - (sun.Sunset!.Value - noon.At), TimeSpan.FromMinutes(-2), TimeSpan.FromMinutes(2));

        // Declination about -4.5° in early October: 90 - |47.37 + 4.5| ≈ 38°.
        Assert.InRange(noon.AltitudeDegrees, 36, 40);
    }

    [Fact]
    public void S1490_days_shorten_in_autumn_and_lengthen_in_spring_in_the_north()
    {
        var autumn = SolarPosition.DaylightChange(ZurichNoonOct, 47.37, 8.54);
        var spring = SolarPosition.DaylightChange(new DateTimeOffset(2026, 3, 20, 11, 0, 0, TimeSpan.Zero), 47.37, 8.54);

        Assert.NotNull(autumn);
        Assert.InRange(autumn!.Value.TotalMinutes, -4, -2);
        Assert.InRange(spring!.Value.TotalMinutes, 2, 4);
    }

    [Fact]
    public void S1490_no_daylight_change_inside_the_polar_circle()
        => Assert.Null(SolarPosition.DaylightChange(new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero), 80, 15));

    [Theory]
    [InlineData(134, "+2m 14s")]
    [InlineData(-58, "−58s")]
    [InlineData(-61, "−1m 1s")]
    [InlineData(0.4, "same as yesterday")]
    public void S1490_the_daylight_change_reads_as_minutes_and_seconds(double seconds, string expected)
        => Assert.Equal(expected, SkyText.Change(Locale.En, TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void S1490_the_sky_text_is_in_the_language_asked_for()
    {
        Assert.Equal("+2 min 14 s", SkyText.Change(Locale.De, TimeSpan.FromSeconds(134)));
        Assert.Equal("wie gestern", SkyText.Change(Locale.De, TimeSpan.Zero));
        Assert.Equal("in 6 h 45 min", SkyText.Countdown(Locale.De, TimeSpan.FromMinutes(405)));
        Assert.Equal("6時間45分後", SkyText.Countdown(Locale.Ja, TimeSpan.FromMinutes(405)));
        Assert.Equal("Mi., 7. Okt.", SkyText.ShortDate(Locale.De, new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)));
        Assert.Equal("Vollmond", MoonPhase.Label(Locale.De, MoonPhaseName.FullMoon));
    }

    [Theory]
    [InlineData(38.4, "38°")]
    [InlineData(-0.4, "0°")]
    [InlineData(-12.6, "-13°")]
    public void S1490_degrees_are_whole_and_never_minus_zero(double degrees, string expected)
        => Assert.Equal(expected, SkyText.Degrees(degrees));

    [Fact]
    public void S1490_daylight_and_countdown_text()
    {
        Assert.Equal("24h", SkyText.Daylight(Locale.En, new SunTimes(null, null, PolarState.Day)));
        Assert.Equal("0h", SkyText.Daylight(Locale.En, new SunTimes(null, null, PolarState.Night)));

        var rise = new DateTimeOffset(2026, 10, 4, 5, 28, 0, TimeSpan.Zero);
        Assert.Equal("11h 33m", SkyText.Daylight(Locale.En, new SunTimes(rise, rise.AddMinutes(693), PolarState.None)));

        Assert.Equal("in 6h 45m", SkyText.Countdown(Locale.En, TimeSpan.FromMinutes(405)));
        Assert.Equal("in 0m", SkyText.Countdown(Locale.En, TimeSpan.FromMinutes(-3)));
        Assert.Equal("Wed, Oct 7", SkyText.ShortDate(Locale.En, new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void S1491_the_next_major_phase_is_ahead_and_lands_on_full_or_new()
    {
        var now = ZurichNoonOct;
        var next = MoonPhase.NextMajor(now);

        Assert.True(next.At > now);
        Assert.True(next.At - now <= TimeSpan.FromDays(15));

        var phase = MoonPhase.At(next.At).Phase;
        var target = next.Name == MoonPhaseName.FullMoon ? 0.5 : 0.0;
        var distance = Math.Min(Math.Abs(phase - target), 1 - Math.Abs(phase - target));
        Assert.True(distance < 0.001, $"phase {phase} at {next.At}");
    }

    [Fact]
    public void S1491_at_the_full_moon_itself_the_next_major_phase_is_the_new_moon()
    {
        var full = MoonPhase.NextMajor(ZurichNoonOct);
        var after = full.Name == MoonPhaseName.FullMoon ? MoonPhase.NextMajor(full.At) : MoonPhase.NextMajor(MoonPhase.NextMajor(full.At).At);

        Assert.True(after.At > full.At);
    }
}
