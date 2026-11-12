using System.Text.Json.Serialization;
using Themia.Core.Astronomy;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Widget.SunTimes;

/// <summary>[S-11115] One row the sun times can show; the order is the kind's own (what a taller card adds next).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SunTimeKind>))]
public enum SunTimeKind
{
    Sunrise,
    Sunset,

    /// <summary>The daylight's length.</summary>
    Daylight,

    /// <summary>Solar noon.</summary>
    Noon,

    /// <summary>Civil dawn.</summary>
    Dawn,

    /// <summary>Civil dusk.</summary>
    Dusk,
}

/// <summary>One row as drawn: its kind, its name, a short name for a narrow row, its value, and whether it is the next event (emphasised).</summary>
public readonly record struct SunTime(SunTimeKind Kind, string Name, string? ShortName, string Value, bool Next);

/// <summary>
/// [S-11115] The sun times' rows at an instant and a place, from the SDK's astronomy (the sun graphic's
/// own, so the two agree to the minute): a rise or set as a wall-clock time (an em dash inside the polar
/// circles), the daylight's length, solar noon, civil dawn and dusk. The next of sunrise and sunset is
/// emphasised.
/// </summary>
public static class SunTimeRules
{
    /// <summary>Every row, in the kind's order.</summary>
    public static IReadOnlyList<SunTimeKind> All { get; } = Enum.GetValues<SunTimeKind>();

    /// <summary>[S-11115] A new card's rows: sunrise, sunset and the daylight.</summary>
    public static IReadOnlyList<SunTimeKind> Defaults { get; } = [SunTimeKind.Sunrise, SunTimeKind.Sunset, SunTimeKind.Daylight];

    /// <summary>A row's name in the app's language.</summary>
    public static string Name(SunTimeKind kind) => kind switch
    {
        SunTimeKind.Sunrise => Str.WidgetUi_Sunrise.Text(),
        SunTimeKind.Sunset => Str.WidgetUi_Sunset.Text(),
        SunTimeKind.Daylight => SunTimesStr.WidgetUi_Daylight.Text(),
        SunTimeKind.Noon => SunTimesStr.WidgetsC_SunMoon_SolarNoon.Text(),
        SunTimeKind.Dawn => SunTimesStr.WidgetsC_SunMoon_Dawn.Text(),
        _ => SunTimesStr.WidgetsC_SunMoon_Dusk.Text(),
    };

    private static string? ShortName(SunTimeKind kind) => kind switch
    {
        SunTimeKind.Sunrise => SunTimesStr.WidgetsC_SunMoon_Short_Sunrise.Text(),
        SunTimeKind.Sunset => SunTimesStr.WidgetsC_SunMoon_Short_Sunset.Text(),
        SunTimeKind.Daylight => SunTimesStr.WidgetsC_SunMoon_Short_Daylight.Text(),
        _ => null,
    };

    /// <summary>[S-11115] The <paramref name="picked"/> rows at <paramref name="now"/> for an observer at the given place.</summary>
    public static IReadOnlyList<SunTime> For(IReadOnlyList<SunTimeKind> picked, DateTimeOffset now, double latitude, double longitude)
    {
        ArgumentNullException.ThrowIfNull(picked);
        var times = SolarPosition.TimesFor(now, latitude, longitude);
        var next = SolarPosition.NextEvent(now, latitude, longitude)?.Kind;
        var twilight = SolarPosition.CivilTwilightFor(now, latitude, longitude);
        var noon = SolarPosition.NoonFor(now, latitude, longitude);

        SunTime Row(SunTimeKind kind) => kind switch
        {
            SunTimeKind.Sunrise => new(kind, Name(kind), ShortName(kind), Clock(times.Sunrise), next == SunEventKind.Sunrise),
            SunTimeKind.Sunset => new(kind, Name(kind), ShortName(kind), Clock(times.Sunset), next == SunEventKind.Sunset),
            SunTimeKind.Daylight => new(kind, Name(kind), ShortName(kind), SkyText.Daylight(times), false),
            SunTimeKind.Noon => new(kind, Name(kind), null, Clock(noon.At), false),
            SunTimeKind.Dawn => new(kind, Name(kind), null, Clock(twilight.Sunrise), false),
            _ => new(kind, Name(kind), null, Clock(twilight.Sunset), false),
        };

        return [.. picked.Select(Row)];
    }

    /// <summary>
    /// A wall-clock time for an instant on the language's clock ("07:28", "7:28 AM"); an absent time is an
    /// em dash, because inside the polar circles there is no event to show ([S-161]).
    /// </summary>
    public static string Clock(DateTimeOffset? moment)
        => moment is { } instant ? LocaleFormat.Time(TimeOnly.FromDateTime(instant.ToLocalTime().DateTime)) : "—";
}
