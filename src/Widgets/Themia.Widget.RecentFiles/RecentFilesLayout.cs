using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-4842] What the Recent files list shows at one size.</summary>
/// <param name="Lines">The rows' lines ([S-6712]): the name, and its folder under it from a regular tile.</param>
/// <param name="Lead">The file's own icon before the name, from a column of 180.</param>
/// <param name="Time">When it was opened, as the row's value, from 180.</param>
public readonly record struct RecentFilesPlan(int Lines, bool Lead, bool Time)
{
    /// <summary>The rows' one height (32 or 48).</summary>
    public double RowHeight => FamilyShape.RowHeight(Lines);
}

/// <summary>
/// [S-4842] The Recent files ladder: the list is the widget, on the family's rows ([S-6711]). The
/// name primary, when it was opened as the value, the folder on the second line where the tile is
/// regular both ways (`RowRules.Lines`); the icon and the time from a column of 180, as the kit's
/// lists ([S-7031]). As many whole rows as fit show; the rest scrolls.
/// </summary>
public static class RecentFilesLayout
{
    public static RecentFilesPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new RecentFilesPlan(1, Lead: true, Time: true);
        }

        var wide = area.Width >= KitLadder.LeadNeedsWidth;
        return new RecentFilesPlan(RowRules.Lines(area, 2), wide, wide);
    }

    /// <summary>[S-6712] The whole rows of <paramref name="plan"/> in the view's area less the card's bottom inset (at least one).</summary>
    public static int Rows(WidgetArea area, RecentFilesPlan plan)
        => Math.Max(1, RowRules.Fit(KitLadder.ListHeight(area), plan.Lines));
}
