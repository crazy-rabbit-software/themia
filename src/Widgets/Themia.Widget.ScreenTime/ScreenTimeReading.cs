using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.ScreenTime;

namespace Themia.Core.Widgets;

/// <summary>[S-11501] What the reading says: its figure, the line under it, and whether the figure is over its limit.</summary>
public readonly record struct ScreenTimeReadingText(double Seconds, string Line, bool Over);

/// <summary>
/// [S-11500] [S-11501] The screen-time reading's size and words, pure: the figure on the family's 28
/// with one line under it, one height, 160..320 wide.
/// </summary>
public static class ScreenTimeReading
{
    public const double Narrowest = 160;

    public const double NaturalWidth = 200;

    public const double Widest = 320;

    /// <summary>The widest figure a reading holds, so its least width never cuts it: 888h 59m.</summary>
    public const double WorstSeconds = (888 * 3600) + (59 * 60);

    /// <summary>[S-11500] The reading's sizing, with its header: the 28's line and its line under, one height.</summary>
    public static WidgetSizing Sizing()
    {
        var height = FamilySpace.ContentTop + FamilyType.Big28.LineHeight + FamilyType.Under(FamilyType.Big28).LineHeight + FamilySpace.Inset;
        var figure = Math.Ceiling(ReadoutRules.Width(FamilyType.Big28, new ReadoutText(ScreenTimeRules.FormatDuration(WorstSeconds)))) + (2 * FamilySpace.Inset);
        var least = Math.Max(Narrowest, Math.Ceiling(figure / FamilySpace.S1) * FamilySpace.S1);
        return WidgetSizing.Range(new(least, height), new(Math.Max(least, NaturalWidth), height), new(Math.Max(least, Widest), height));
    }

    /// <summary>
    /// [S-11501] The reading of a period: a day's total with "of 4h" under it where a daily limit is set;
    /// a window's total with its average per day and "of 4h/day"; over the limit (a window: its average)
    /// the figure is critical ([S-457]).
    /// </summary>
    public static ScreenTimeReadingText Headline(bool window, long total, int days, long? dayLimit)
    {
        if (!window)
        {
            return ScreenTimeRules.Goal(total, dayLimit) is { } goal
                ? new(total, ScreenTimeStr.Screentime_OfGoal.Format(("value", ScreenTimeRules.FormatLimit(goal.Limit))), goal.Over)
                : new(total, string.Empty, false);
        }

        var average = ScreenTimeRules.DailyAverage(total, days);
        var parts = new List<string> { Str.Screentime_PerDay.Format(("value", ScreenTimeRules.FormatDuration(average))) };
        var over = false;
        if (ScreenTimeRules.Goal(average, dayLimit) is { } limit)
        {
            parts.Add(ScreenTimeStr.Screentime_GoalPerDay.Format(("value", ScreenTimeRules.FormatLimit(limit.Limit))));
            over = limit.Over;
        }

        return new(total, string.Join(" · ", parts), over);
    }
}
