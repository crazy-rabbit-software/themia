using System.Text.Json.Serialization;
using Themia.Core.Astronomy;
using Themia.Core.Localization;

namespace Themia.Widget.MoonTimes;

/// <summary>[S-11125] One row the moon times can show; the order is the kind's own (what a taller card adds next).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MoonTimeKind>))]
public enum MoonTimeKind
{
    Rise,
    Set,
    NextFull,
    NextNew,
}

/// <summary>
/// One row as drawn: its kind, name, short name, value, the value without its weekday for a narrow row
/// (a date's), whether it is the next rise or set (emphasised), and the phase its lead moon is drawn at.
/// </summary>
public readonly record struct MoonTime(MoonTimeKind Kind, string Name, string? ShortName, string Value, string? ShortValue, bool Next, double LeadPhase);

/// <summary>
/// [S-11125] The moon times' rows at an instant and a place, from the SDK's astronomy (the moon reading's
/// own, so the two agree): the next moonrise and moonset ([S-3104], their day where it is not today, an em
/// dash where there is none, [S-3106]), the next full moon and the next new moon ([S-1491]) as dates. The
/// next of rise and set is emphasised.
/// </summary>
public static class MoonTimeRules
{
    public static IReadOnlyList<MoonTimeKind> All { get; } = Enum.GetValues<MoonTimeKind>();

    /// <summary>[S-11125] A new card's rows: moonrise, moonset and the next full moon.</summary>
    public static IReadOnlyList<MoonTimeKind> Defaults { get; } = [MoonTimeKind.Rise, MoonTimeKind.Set, MoonTimeKind.NextFull];

    public static string Name(MoonTimeKind kind) => kind switch
    {
        MoonTimeKind.Rise => MoonTimesStr.WidgetsC_Moon_Rise.Text(),
        MoonTimeKind.Set => MoonTimesStr.WidgetsC_Moon_Set.Text(),
        MoonTimeKind.NextFull => MoonPhase.Label(MoonPhaseName.FullMoon),
        _ => MoonPhase.Label(MoonPhaseName.NewMoon),
    };

    private static string? ShortName(MoonTimeKind kind) => kind switch
    {
        MoonTimeKind.Rise => MoonTimesStr.WidgetsC_Moon_Short_Rise.Text(),
        MoonTimeKind.Set => MoonTimesStr.WidgetsC_Moon_Short_Set.Text(),
        _ => null,
    };

    /// <summary>[S-11125] The <paramref name="picked"/> rows at <paramref name="now"/> for an observer at the given place.</summary>
    public static IReadOnlyList<MoonTime> For(IReadOnlyList<MoonTimeKind> picked, DateTimeOffset now, double latitude, double longitude)
    {
        ArgumentNullException.ThrowIfNull(picked);
        var locale = Strings.Current;
        var phase = MoonPhase.At(now).Phase;
        var times = MoonPhase.NextRiseSet(now, latitude, longitude);
        var riseNext = times.Next is { IsRise: true };
        var setNext = times.Next is { IsRise: false };

        string Date(MoonEvent major) => SkyText.ShortDate(locale, major.At.ToLocalTime());
        string Day(MoonEvent major) => LocaleFormat.Date(locale, DateOnly.FromDateTime(major.At.ToLocalTime().DateTime), DateStyle.MonthDayShort);
        var full = MoonPhase.NextFull(now);
        var fresh = MoonPhase.NextNew(now);

        MoonTime Row(MoonTimeKind kind) => kind switch
        {
            MoonTimeKind.Rise => new(kind, Name(kind), ShortName(kind), SkyText.EventTime(locale, times.Rise, now), null, riseNext, phase),
            MoonTimeKind.Set => new(kind, Name(kind), ShortName(kind), SkyText.EventTime(locale, times.Set, now), null, setNext, phase),
            MoonTimeKind.NextFull => new(kind, Name(kind), null, Date(full), Day(full), false, 0.5),
            _ => new(kind, Name(kind), null, Date(fresh), Day(fresh), false, 0),
        };

        return [.. picked.Select(Row)];
    }
}
