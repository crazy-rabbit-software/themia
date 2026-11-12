#if DEBUG
using Themia.Core.Astronomy;

namespace Themia.Sdk.Samples;

/// <summary>
/// Debug renders only ([S-2051]): the one place every sample is about, Zürich, spelled the same in
/// every widget. Its sunrise and sunset are computed by the same astronomy the Sun and Moon widgets
/// draw with, so the weather's sunrise and the Sun widget's cannot disagree.
/// </summary>
public static class SamplePlace
{
    /// <summary>The place's name, as a header shows it.</summary>
    public const string Name = "Zürich";

    /// <summary>The place as a search would have named it (the weather widgets' location; a header shows its first part).</summary>
    public const string LocationName = "Zürich, Switzerland";

    public const double Latitude = 47.37;

    public const double Longitude = 8.54;

    /// <summary>Sunrise and sunset on <paramref name="day"/> as local wall-clock times (null in a polar day or night).</summary>
    public static (TimeOnly? Sunrise, TimeOnly? Sunset) Sun(DateOnly day)
    {
        var noon = SampleDay.At(day, new TimeOnly(12, 0), TimeZoneInfo.Local);
        var times = SolarPosition.TimesFor(noon, Latitude, Longitude);
        return (Local(times.Sunrise), Local(times.Sunset));

        static TimeOnly? Local(DateTimeOffset? at)
            => at is { } moment ? TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, TimeZoneInfo.Local).DateTime) : null;
    }
}
#endif
