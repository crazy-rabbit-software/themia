using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-1115] Native digits in the clock's readout (a host rule, [S-1115], over the clock, a module
/// project). The rest of the clock's tests are its module's.
/// </summary>
public sealed class ClockDigitsHostTests
{
    private static DateTimeOffset At(int hour, int minute, int second = 0)
        => new(2026, 3, 9, hour, minute, second, TimeSpan.Zero);

    private static ClockConfig Utc(Action<ClockConfig>? tweak = null)
    {
        var config = new ClockConfig { TimeZoneId = "Etc/UTC", ShowDate = false };
        tweak?.Invoke(config);
        return config;
    }

    /// <summary>
    /// [S-1115] Arabic's own digits, one for one: the readout keeps the character count the hero
    /// type ladder sizes it by, with seconds too.
    /// </summary>
    [Fact]
    public void S1115_an_arabic_clock_reads_in_arabic_digits_at_the_same_length()
    {
        var latin = ClockFormatter.Read(At(9, 5, 7), Utc(c => c.ShowSeconds = true), Locale.En, use24Hour: true);
        var arabic = ClockFormatter.Read(At(9, 5, 7), Utc(c => c.ShowSeconds = true), Locale.Ar, use24Hour: true);

        Assert.Equal("٠٩:٠٥:٠٧", arabic.Time);
        Assert.Equal(latin.Time.Length, arabic.Time.Length);
        Assert.Equal("9:05", ClockFormatter.Read(At(9, 5), Utc(), Locale.Ur, use24Hour: false).Time);
    }
}
