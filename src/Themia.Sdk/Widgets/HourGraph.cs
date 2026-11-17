using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-12703] How a weather graph draws its main series: the same frame, three marks.</summary>
public enum HourGraphLook
{
    /// <summary>A line at the Chart block's stroke, no wash.</summary>
    Line = 0,

    /// <summary>The line with the Chart block's 16 % wash under it.</summary>
    Area = 1,

    /// <summary>A round column per hour, filled from the foot.</summary>
    Bars = 2,
}

/// <summary>
/// [S-12703] One slot of a weather graph: its moment, its value as a share of the plot's height (null: a gap,
/// never a 0), the role it is drawn in (its category band's: the accent, Caution or Critical), an optional second
/// series' share (tertiary) and a direction in degrees the wind comes from (the wind graph's arrows).
/// </summary>
public readonly record struct HourSlot(DateTimeOffset At, double? Value, ColourRole Role = ColourRole.Accent, double? Second = null, double? Direction = null);

/// <summary>[S-12703] A figure written at a slot's point: over it (a peak, a maximum) or under it (a minimum).</summary>
public readonly record struct HourMark(int Slot, string Text, bool Under = false);

/// <summary>
/// [S-12703] What a weather graph draws, made by its kind's rules from the answer and the config: the slots, the
/// look, the top line (the kind's glyph leading the reading now in its role, and a measure the kind lets the person
/// choose), the figures at the marked slots, the shaded spans (daylight), the moment now and the time line's two ends.
/// </summary>
public sealed record HourGraphModel(IReadOnlyList<HourSlot> Slots, HourGraphLook Look)
{
    /// <summary>[S-12708] The kind's glyph, leading the reading: what the graph measures, never in words ([S-12801]).</summary>
    public string Glyph { get; init; } = string.Empty;

    /// <summary>
    /// [S-12708] The measure after the reading where the kind offers a choice of them (the air graph's "PM2.5 µg/m³"),
    /// else empty: never the kind's name or the place, which a group's Header widget says once ([S-12801]).
    /// </summary>
    public string Label { get; init; } = string.Empty;

    public string Reading { get; init; } = string.Empty;

    /// <summary>The reading's role: primary text, or Caution/Critical in a bad band (never the accent: [S-6706]).</summary>
    public ColourRole ReadingRole { get; init; } = ColourRole.TextPrimary;

    public IReadOnlyList<HourMark> Marks { get; init; } = [];

    /// <summary>Spans shaded behind the plot (the UV graph's daylight).</summary>
    public IReadOnlyList<DaylightSpan> Shade { get; init; } = [];

    public DateTimeOffset Now { get; init; }

    /// <summary>The time between two slots: an hour, a quarter for the rain's nowcast.</summary>
    public TimeSpan Step { get; init; } = TimeSpan.FromHours(1);

    public string StartLabel { get; init; } = string.Empty;

    public string EndLabel { get; init; } = string.Empty;

    /// <summary>Whether the slots carry directions (the wind graph's arrow line under the plot).</summary>
    public bool Arrows => Slots.Any(s => s.Direction is not null);
}

/// <summary>
/// [S-12703] [S-12704] The weather graphs' one frame and its geometry, pure and tested; the SDK's <c>HourGraph</c>
/// part draws it. Every graph: the top line (Body: the glyph and the reading), 8 down the plot (its figures inside it, a Caption line
/// kept free over the highest point and under the lowest), the arrow line where the wind has one, 4 down the
/// time line (Caption, tertiary: "Now" and the window's end), the family's inset all round. The slots are
/// equal cells across the plot, each value on its cell's middle, time left to right in every language ([S-1113]).
/// </summary>
public static class HourGraphRules
{
    /// <summary>The hour windows a graph offers (12, 24, 48 hours from the one under way).</summary>
    public static IReadOnlyList<int> HourChoices { get; } = [12, 24, 48];

    /// <summary>The air graph's windows: one to four days.</summary>
    public static IReadOnlyList<int> AirHourChoices { get; } = [24, 48, 96];

    /// <summary>The plot at its least and at a new card's.</summary>
    public const double PlotMin = 56;

    public const double PlotStart = 96;

    public const double Narrowest = 220;

    public const double WidthStart = 320;

    /// <summary>A column at most this share of its cell, never under the stroke nor over a large icon's half.</summary>
    public const double BarShare = 0.6;

    /// <summary>What a graph needs besides its plot, top to bottom, its insets included.</summary>
    public static double Frame(bool arrows)
        => FamilySpace.Inset + FamilyType.Body.LineHeight + FamilySpace.S2
            + (arrows ? FamilySpace.S1 + FamilyShape.Icon : 0)
            + FamilySpace.S1 + FamilyType.Caption.LineHeight + FamilySpace.Inset;

    /// <summary>
    /// [S-12704] Every weather graph's sizing: 220..640 wide, the plot 56..240 tall, a new card 320 by a 96 plot;
    /// relaxed as a chart ([S-12411]: the plot takes the box).
    /// </summary>
    public static WidgetSizing Sizing(bool arrows)
    {
        var frame = Frame(arrows);
        return WidgetSizing.Range(
            new(Narrowest, frame + PlotMin),
            new(WidthStart, frame + PlotStart),
            new(Narrowest * 3, frame + (PlotMin * 4))) with { Room = StretchRoom.Picture };
    }

    /// <summary>
    /// [S-12705] The window: the hour under way (the last stamp at or before <paramref name="now"/>) and the
    /// <paramref name="hours"/> after it, in time order. An answer older than its window starts where it has hours
    /// left; one with nothing at or after the hour under way gives nothing (too old to draw).
    /// </summary>
    public static IReadOnlyList<T> Window<T>(IReadOnlyList<T> items, Func<T, DateTimeOffset> at, DateTimeOffset now, int hours)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(at);
        var ordered = items.OrderBy(at).ToList();
        var first = ordered.FindLastIndex(i => at(i) <= now);
        if (first < 0)
        {
            first = 0;
        }

        var shown = ordered.Skip(first).Take(Math.Max(1, hours) + 1).ToList();
        return shown.Count > 0 && at(shown[^1]) >= now.AddHours(-1) ? shown : [];
    }

    /// <summary>
    /// [S-12705] The range a series is drawn against: its lowest and highest values, widened to at least
    /// <paramref name="minSpan"/> about their middle (a flat day is not drawn as a cliff); from 0 when
    /// <paramref name="fromZero"/> (amounts, indices), up to at least <paramref name="floorTop"/> there (a dry day
    /// stays low). Null when there is no value.
    /// </summary>
    public static (double Low, double High)? Scale(IEnumerable<double?> values, double minSpan, bool fromZero = false, double floorTop = 0)
    {
        ArgumentNullException.ThrowIfNull(values);
        var known = values.Where(v => v is { } x && double.IsFinite(x)).Select(v => v!.Value).ToList();
        if (known.Count == 0)
        {
            return null;
        }

        var (low, high) = (known.Min(), known.Max());
        if (fromZero)
        {
            return (0, Math.Max(Math.Max(high, floorTop), minSpan > 0 ? minSpan : double.Epsilon));
        }

        if (high - low < minSpan)
        {
            var middle = (low + high) / 2;
            (low, high) = (middle - (minSpan / 2), middle + (minSpan / 2));
        }

        return (low, high);
    }

    /// <summary>A value as its share of a range (clamped 0..1); null stays null.</summary>
    public static double? Share(double? value, (double Low, double High) range)
        => value is { } v && double.IsFinite(v)
            ? range.High > range.Low ? Math.Clamp((v - range.Low) / (range.High - range.Low), 0, 1) : 0
            : null;

    /// <summary>[S-12705] The first lowest and first highest of the values, as indices; null without any.</summary>
    public static (int Low, int High)? Extremes(IReadOnlyList<double?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        int? low = null, high = null;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v || !double.IsFinite(v))
            {
                continue;
            }

            if (low is null || v < values[low.Value]!.Value)
            {
                low = i;
            }

            if (high is null || v > values[high.Value]!.Value)
            {
                high = i;
            }
        }

        return low is { } l && high is { } h ? (l, h) : null;
    }

    /// <summary>[S-12703] A slot's middle across a plot <paramref name="width"/> wide of <paramref name="count"/> equal cells.</summary>
    public static double X(int slot, int count, double width) => count <= 0 ? 0 : (slot + 0.5) * width / count;

    /// <summary>[S-12703] A moment's place across the plot: the cells' middles are their slots' stamps, <paramref name="step"/> apart.</summary>
    public static double X(DateTimeOffset at, DateTimeOffset first, TimeSpan step, int count, double width)
        => count <= 0 || step <= TimeSpan.Zero ? 0 : (((at - first) / step) + 0.5) * width / count;

    /// <summary>[S-12703] A share's height in the plot, a Caption line kept free over it and under it for the figures.</summary>
    public static double Y(double share, double height)
    {
        var room = FamilyType.Caption.LineHeight;
        return room + ChartRules.Y(share, Math.Max(0, height - (2 * room)));
    }

    /// <summary>[S-12703] A column's width in a cell: its share of the cell, at least the stroke, at most a large icon's half.</summary>
    public static double BarWidth(double cell) => Math.Clamp(cell * BarShare, FamilyShape.Stroke, FamilyShape.IconLarge / 2);

    /// <summary>
    /// [S-12703] The unbroken runs of a series: index ranges with a value in every slot. A gap breaks the line
    /// rather than being drawn through; a run of one slot is a dot.
    /// </summary>
    public static IReadOnlyList<(int First, int Last)> Runs(IReadOnlyList<double?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var runs = new List<(int, int)>();
        int? start = null;
        for (var i = 0; i <= values.Count; i++)
        {
            var has = i < values.Count && values[i] is { } v && double.IsFinite(v);
            if (has && start is null)
            {
                start = i;
            }
            else if (!has && start is { } s)
            {
                runs.Add((s, i - 1));
                start = null;
            }
        }

        return runs;
    }

    /// <summary>
    /// [S-12703] The role a line's piece from one slot to the next is drawn in: the worse of its two ends' (the
    /// accent, then Caution, then Critical), so a band's colour starts at the hour that reaches it.
    /// </summary>
    public static ColourRole Worse(ColourRole a, ColourRole b) => Rank(a) >= Rank(b) ? a : b;

    private static int Rank(ColourRole role) => role switch
    {
        ColourRole.Critical => 2,
        ColourRole.Caution => 1,
        _ => 0,
    };

    /// <summary>[S-12703] The local midnights inside the window: the days' borders, drawn as hairlines.</summary>
    public static IReadOnlyList<DateTimeOffset> Midnights(DateTimeOffset first, DateTimeOffset last, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var list = new List<DateTimeOffset>();
        var local = TimeZoneInfo.ConvertTime(first, zone);
        var day = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(1);
        for (var guard = 0; guard < 8; guard++, day = day.AddDays(1))
        {
            var instant = new DateTimeOffset(day, zone.GetUtcOffset(day));
            if (instant >= last)
            {
                break;
            }

            list.Add(instant);
        }

        return list;
    }

    /// <summary>
    /// [S-12703] The time line's end: the window's last hour as a time ("17:00"), with its short weekday when it
    /// is not today ("Tue 17:00").
    /// </summary>
    public static string EndLabel(Locale locale, DateTimeOffset last, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var end = TimeZoneInfo.ConvertTime(last, zone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var time = LocaleFormat.Time(locale, TimeOnly.FromDateTime(end.DateTime));
        return DateOnly.FromDateTime(end.DateTime) == today ? time : $"{LocaleFormat.WeekdayName(locale, end.DayOfWeek, NameWidth.Short)} {time}";
    }

    /// <summary>
    /// [S-12708] [S-12801] The frame's words for a graph: the kind's <paramref name="glyph"/> leading the reading on
    /// the top line, the chosen <paramref name="measure"/> after it (empty for a kind with one measure), "Now" and the
    /// window's end on the time line. No name and no place: a group's Header widget says them once.
    /// </summary>
    public static HourGraphModel Framed(HourGraphModel model, string glyph, string measure, DateTimeOffset now, Locale locale, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model with
        {
            Glyph = glyph ?? string.Empty,
            Label = measure ?? string.Empty,
            Now = now,
            StartLabel = Strings.Get(locale, Str.WeatherGraph_Now),
            EndLabel = model.Slots.Count == 0 ? string.Empty : EndLabel(locale, model.Slots[^1].At, now, zone),
        };
    }

    /// <summary>
    /// [S-12705] A temperature from the answer's °C in the shared unit ([S-3770]): °F is ×9/5+32; null stays null.
    /// </summary>
    public static double? Temperature(double? celsius, TemperatureUnit unit)
        => celsius is { } c && double.IsFinite(c) ? unit == TemperatureUnit.Fahrenheit ? (c * 9 / 5) + 32 : c : null;
}
