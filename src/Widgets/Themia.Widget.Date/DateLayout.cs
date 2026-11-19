using System.Collections.Concurrent;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Date;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-10801]..[S-10804] The date widget's words and sizes, pure: what each style says of a day (through
/// <see cref="LocaleFormat"/> only, never culture data), the family's step it is planned at, and the box that holds
/// the language's widest day in that style: the natural size. The view lays the face at that step and scales it up
/// and down to fill its box ([S-10803], as the clock, [S-12804]): how big the date is is the card's (no Size setting,
/// [S-2449] retired).
/// </summary>
public static class DateLayout
{
    /// <summary>The days measured for the widest text: two whole years, so every weekday meets every month and day.</summary>
    private static readonly DateOnly First = new(2024, 1, 1);

    private const int Days = 731;

    private static readonly ConcurrentDictionary<(Locale, DateWidgetStyle, int), double> Widest = new();

    /// <summary>[S-10802] A one-line style's step: 28, a line of words.</summary>
    public static TypeStep LineStep => FamilyType.Big28;

    /// <summary>[S-10802] The page's day number: the family's one-readout step, 68.</summary>
    public static TypeStep NumberStep => FamilyType.Hero68;

    /// <summary>[S-10801] What a one-line style says of <paramref name="day"/>; the page's parts are <see cref="Page"/>.</summary>
    public static string Line(DateWidgetStyle style, Locale locale, DateOnly day) => style switch
    {
        DateWidgetStyle.Short => LocaleFormat.Date(locale, day, DateStyle.WeekdayMonthDayShort),
        DateWidgetStyle.Numeric => LocaleFormat.Date(locale, day, DateStyle.Short),
        _ => DateStr.Date_Long.Format(locale,
            ("weekday", LocaleFormat.WeekdayName(locale, day.DayOfWeek, NameWidth.Wide)),
            ("date", LocaleFormat.Date(locale, day, DateStyle.MonthDay))),
    };

    /// <summary>[S-10801] The page's three parts: the weekday, the day of the month in the language's digits, the month.</summary>
    public static (string Weekday, string Number, string Month) Page(Locale locale, DateOnly day)
        => (LocaleFormat.WeekdayName(locale, day.DayOfWeek, NameWidth.Wide),
            LocaleFormat.Integer(locale, day.Day),
            LocaleFormat.MonthName(locale, day.Month, NameWidth.Wide));

    /// <summary>[S-10801] The whole date, the tooltip in every style: "Monday, November 17, 2026".</summary>
    public static string Full(Locale locale, DateOnly day) => LocaleFormat.Date(locale, day, DateStyle.Full);

    /// <summary>
    /// [S-12833] The text sizes of the face (<see cref="FaceFloor"/>): its main reading (the date's one line, the page's day
    /// number) and, on the page, the weekday's and the month's line, which give way first.
    /// </summary>
    public static IReadOnlyList<double> PartSizes(DateConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style != DateWidgetStyle.Page)
        {
            return [LineStep.FontSize];
        }

        var number = NumberStep;
        return [number.FontSize, FamilyType.Under(number).FontSize];
    }

    /// <summary>
    /// [S-10802] The face's box at its natural size: the widest day of the language in the style on its step (the
    /// page: the number's step and the weekday's and month's line under it, the widest of the three), no inset.
    /// </summary>
    public static SizeDip Face(DateConfig config, Locale locale) => Face(config, locale, lines: true).Box;

    /// <summary>
    /// [S-12833] The face as it is drawn where the room keeps the parts of at least <paramref name="floor"/> DIP: the page
    /// its number alone once its weekday and month gave way.
    /// </summary>
    public static SizeDip Face(DateConfig config, Locale locale, double floor)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Face(config, locale, Lines(config, floor)).Box;
    }

    /// <summary>[S-12833] Whether the page draws its weekday and month where the room keeps the parts of at least <paramref name="floor"/>.</summary>
    public static bool Lines(DateConfig config, double floor)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Style == DateWidgetStyle.Page && FamilyType.Under(NumberStep).FontSize >= floor;
    }

    /// <summary>[S-12833] The face's plan drawing its parts of at least <paramref name="floor"/> DIP: its width and its ink's height, no inset.</summary>
    public static FacePlan Plan(DateConfig config, Locale locale, double floor)
    {
        ArgumentNullException.ThrowIfNull(config);
        var face = Face(config, locale, Lines(config, floor));
        return new FacePlan(face.Box.Width, face.Ink);
    }

    /// <summary>[S-12833] How the face stands in a card's content box of <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static FaceFit Fit(double width, double height, DateConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);
        return FaceFloor.Fit(width, height, PartSizes(config), floor => Plan(config, locale, floor));
    }

    /// <summary>The face's box and its ink's height (the box less the leading over its first capitals and under its last baseline).</summary>
    private static (SizeDip Box, double Ink) Face(DateConfig config, Locale locale, bool lines)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == DateWidgetStyle.Page)
        {
            var number = NumberStep;
            var digits = HeroFit.Ems("88") * number.FontSize;
            if (!lines)
            {
                return (new SizeDip(Math.Ceiling(digits), number.LineHeight), number.LineHeight - LineInk.Of(number).Over - LineInk.Of(number).Under);
            }

            var line = FamilyType.Under(number);
            var words = Math.Max(WidestOf(locale, DateWidgetStyle.Page, 0, line), WidestOf(locale, DateWidgetStyle.Page, 1, line));
            var tall = number.LineHeight + (2 * line.LineHeight);
            return (new SizeDip(Math.Ceiling(Math.Max(words, digits)), tall), tall - LineInk.Of(line).Over - LineInk.Of(line).Under);
        }

        var step = LineStep;
        return (new SizeDip(Math.Ceiling(WidestOf(locale, config.Style, 0, step)), step.LineHeight), step.LineHeight - LineInk.Of(step).Over - LineInk.Of(step).Under);
    }

    /// <summary>
    /// [S-10802] The sizing: the face in the card's inset of 12 on every side, one height by the style, widened
    /// up to twice (beside a wider mate, under a clock); the host relaxes it ([S-12411]) and a larger box scales the face
    /// up to fill it ([S-10803]). [S-12833] A smaller box scales it down as one: its least box is its main reading alone (the
    /// line, the page's number) at the legibility floor in the inset, the page's weekday and month having given way.
    /// </summary>
    public static WidgetSizing Sizing(DateConfig config, Locale locale)
    {
        var face = Face(config, locale);
        var box = new SizeDip(face.Width + (2 * FamilySpace.Inset), face.Height + (2 * FamilySpace.Inset));
        var least = FaceFloor.LeastBox(PartSizes(config), floor => Plan(config, locale, floor));
        return WidgetSizing.Range(box, box, box with { Width = 2 * box.Width })
            .ScalesDownTo(new SizeDip(least.Width + (2 * FamilySpace.Inset), least.Height + (2 * FamilySpace.Inset)));
    }

    /// <summary>The widest text of <paramref name="style"/> (the page: its weekday, part 0, or its month, part 1) on <paramref name="step"/> over two years.</summary>
    private static double WidestOf(Locale locale, DateWidgetStyle style, int part, TypeStep step)
    {
        var ems = Widest.GetOrAdd((locale, style, part), key =>
        {
            var most = 0.0;
            for (var i = 0; i < Days; i++)
            {
                var day = First.AddDays(i);
                var text = key.Item2 == DateWidgetStyle.Page
                    ? (key.Item3 == 0 ? Page(key.Item1, day).Weekday : Page(key.Item1, day).Month)
                    : Line(key.Item2, key.Item1, day);
                most = Math.Max(most, HeroFit.Ems(text));
            }

            return most;
        });
        return ems * step.FontSize;
    }
}
