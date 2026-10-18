using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7035] A paged kit list (<see cref="KitOverflow.Page"/>): pages of the rows that fit whole, a
/// page being the plan's columns filled one after the other, each group's heading over its first row
/// and again at the top of a column that starts mid-group, so a row is never shown without its group
/// (the agenda's days, [S-2709]). Rows are fitted at one title line each; what is left under a
/// column's rows gives long titles their second line, in order ([S-2717]). Pure: the edges are tested
/// here and not in a view. Groups are compared by their key.
/// </summary>
public static class KitPages
{
    /// <summary>[S-2717] A title wraps onto at most this many lines; past them it ends in an ellipsis.</summary>
    public const int MaxTitleLines = 2;

    /// <summary>[S-1267] Under this height the first page's first group is implied when it is today, and its heading's line goes to a row.</summary>
    public const double FirstHeadingNeedsHeight = 128;

    /// <summary>
    /// [S-2709] How many rows from <paramref name="start"/> one column of <paramref name="height"/>
    /// holds whole, with their headings; <paramref name="firstHeading"/> false drops the column's first
    /// heading ([S-1267]). At least one row while any is left, so paging always moves.
    /// </summary>
    public static int Fit(IReadOnlyList<string> groups, int start, double height, double headingHeight, double rowHeight, bool firstHeading)
    {
        ArgumentNullException.ThrowIfNull(groups);
        if (start >= groups.Count)
        {
            return 0;
        }

        var room = new Disclosure(1, height);
        var count = 0;
        for (var i = start; i < groups.Count; i++)
        {
            if (!room.Take(rowHeight + (HeadingBefore(groups, start, i, firstHeading) ? headingHeight : 0)))
            {
                break;
            }

            count++;
        }

        return Math.Max(1, count);
    }

    /// <summary>[S-2709] Whether row <paramref name="i"/> of a column starting at <paramref name="start"/> has its group's heading over it.</summary>
    public static bool HeadingBefore(IReadOnlyList<string> groups, int start, int i, bool firstHeading)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return i == start ? firstHeading : !string.Equals(groups[i], groups[i - 1], StringComparison.Ordinal);
    }

    /// <summary>[S-2717] The height a column's <paramref name="count"/> rows from <paramref name="start"/> and their headings use at one title line each.</summary>
    public static double ColumnHeight(IReadOnlyList<string> groups, int start, int count, double headingHeight, double rowHeight, bool firstHeading)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var used = 0.0;
        for (var i = start; i < start + count && i < groups.Count; i++)
        {
            used += rowHeight + (HeadingBefore(groups, start, i, firstHeading) ? headingHeight : 0);
        }

        return used;
    }

    /// <summary>
    /// [S-2709] Where each page starts: a page is <paramref name="columns"/> columns filled one after
    /// the other by <see cref="Fit"/>. The first page's first heading follows
    /// <paramref name="firstHeading"/>; every other column starts with its group's heading.
    /// </summary>
    public static IReadOnlyList<int> Pages(IReadOnlyList<string> groups, int columns, double height, double headingHeight, double rowHeight, bool firstHeading)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var pages = new List<int> { 0 };
        var at = 0;
        columns = Math.Max(1, columns);

        while (at < groups.Count)
        {
            for (var c = 0; c < columns && at < groups.Count; c++)
            {
                at += Fit(groups, at, height, headingHeight, rowHeight, firstHeading && at == 0);
            }

            if (at < groups.Count)
            {
                pages.Add(at);
            }
        }

        return pages;
    }

    /// <summary>The page that holds row <paramref name="row"/>, given each page's first row.</summary>
    public static int PageOf(IReadOnlyList<int> pages, int row)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var page = 0;
        for (var p = 0; p < pages.Count; p++)
        {
            if (pages[p] <= row)
            {
                page = p;
            }
        }

        return page;
    }

    /// <summary>
    /// [S-1267] Whether the first page's first group gets its heading: on a short tile a first group
    /// that is today is implied (it is what a small agenda is about) and its line goes to a row.
    /// </summary>
    public static bool ShowsFirstHeading(DateOnly? day, DateOnly today, double height)
        => !(day == today && height > 0 && height < FirstHeadingNeedsHeight);
}

/// <summary>[S-2716] A day group's heading, written by the kit.</summary>
public static class KitDays
{
    /// <summary>
    /// [S-2716] "Today" and "Tomorrow" by name, with the date after them ("Today · Thu, Oct 8") when
    /// <paramref name="dated"/>; any other day its date ("Sat, Oct 10").
    /// </summary>
    public static string Heading(Locale locale, DateOnly day, DateOnly today, bool dated)
    {
        var date = LocaleFormat.Date(locale, day, DateStyle.WeekdayMonthDayShort);
        Str? word = day == today ? Str.Common_Today : day == today.AddDays(1) ? Str.Common_Tomorrow : null;

        if (word is not { } name)
        {
            return date;
        }

        return dated
            ? Strings.Format(locale, Str.WidgetsA_Calendar_HeadingWithDate, ("day", Strings.Get(locale, name)), ("date", date))
            : Strings.Get(locale, name);
    }

    /// <summary>
    /// [S-2716] Whether a heading line of <paramref name="width"/> DIPs holds "Today" or "Tomorrow"
    /// with the date after it, for the longest such heading the language writes (every weekday,
    /// every month), so the date does not come and go with the day of the week. Concise first: the
    /// word is the heading; the date only joins where it fits whole.
    /// </summary>
    public static bool Dated(Locale locale, double width)
    {
        var longest = 0;
        foreach (var name in new[] { Str.Common_Today, Str.Common_Tomorrow })
        {
            var word = Strings.Get(locale, name);
            for (var month = 1; month <= 12; month++)
            {
                for (var day = 22; day <= 28; day++)
                {
                    var date = LocaleFormat.Date(locale, new DateOnly(2026, month, day), DateStyle.WeekdayMonthDayShort);
                    longest = Math.Max(longest, Strings.Format(locale, Str.WidgetsA_Calendar_HeadingWithDate, ("day", word), ("date", date)).Length);
                }
            }
        }

        return width >= FluentType.EstimateWidth(FluentType.BodyStrong, longest);
    }
}
