using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-4404] What a notifications widget shows at a size.</summary>
/// <param name="Lines">The rows' lines ([S-6712]): the title, and the body under it from a regular tile.</param>
/// <param name="Lead">Each row leads (its app's icon, or under a heading its slot), from a column of 180.</param>
/// <param name="Time">When it came, as the row's value, from 180.</param>
/// <param name="Grouped">Under app headings rather than flat with an icon per row: from an expanded height.</param>
/// <param name="Folded">Notifications an app shows before "See more" when grouped.</param>
public readonly record struct NotificationPlan(int Lines, bool Lead, bool Time, bool Grouped, int Folded)
{
    /// <summary>A notification's one height (28 or 40).</summary>
    public double RowHeight => FamilyShape.RowHeight(Lines);
}

/// <summary>
/// [S-4404] The notification widget's ladder on the family's rows ([S-6711]): every tile is the list
/// (the header's count says how many, so the small tile has no face of its own); the title primary,
/// when it came as the value, the body on the second line from a regular tile; the icon and the time
/// from a column of 180, as the kit's lists ([S-7031]); grouped under app headings (one row of 28)
/// from an expanded height.
/// </summary>
public static class NotificationsLayout
{
    public static NotificationPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new NotificationPlan(1, Lead: true, Time: true, Grouped: false, NotificationRules.FoldAt);
        }

        var wide = area.Width >= KitLadder.LeadNeedsWidth;
        return new NotificationPlan(RowRules.Lines(area, 2), wide, wide, Grouped: wide && area.HeightClass >= SizeClass.Expanded, NotificationRules.FoldAt);
    }
}
