using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Clock.Tests;

/// <summary>[S-665] The clock's ladder: the only detail it adds is the date, short when narrow. Moved with the widget from Core's ResponsiveLayoutTests.</summary>
public sealed class ClockLadderTests
{
    [Fact]
    public void S665_a_narrow_clock_has_a_short_date_and_no_week_or_zone()
    {
        // The week, the day of the year and the time zone are retired (concise widgets): the
        // only detail a clock adds is the date, short when narrow.
        var config = new ClockConfig { TimeZoneId = "Etc/UTC" };

        Assert.Equal("Sun, Oct 4", ClockFormatter.ShortDate(new DateTimeOffset(2026, 10, 4, 13, 52, 0, TimeSpan.Zero), config, Locale.En));
    }
}
