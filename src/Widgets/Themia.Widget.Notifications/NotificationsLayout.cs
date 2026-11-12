using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-11404] What a notifications widget shows: its config's, at every size.</summary>
/// <param name="Lines">The rows' lines ([S-6712]): the title, and the body under it on two.</param>
/// <param name="Lead">Each row leads (its app's icon, or under a heading its slot).</param>
/// <param name="Time">When it came, as the row's value.</param>
/// <param name="Grouped">Under app headings rather than flat with an icon per row.</param>
/// <param name="Folded">Notifications an app shows before "See more" when grouped.</param>
public readonly record struct NotificationPlan(int Lines, bool Lead, bool Time, bool Grouped, int Folded)
{
    /// <summary>A notification's one height (28 or 40).</summary>
    public double RowHeight => FamilyShape.RowHeight(Lines);
}

/// <summary>
/// [S-11404] The notification widget's plan on the family's rows ([S-6711]): every row its app's icon,
/// the title primary, when it came the value, the body on the second line where the config gives two;
/// under app headings (one row of 28) where the config says so. No size decides any of it.
/// </summary>
public static class NotificationsLayout
{
    public static NotificationPlan Plan(NotificationsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new NotificationPlan(config.RowsShown.Lines, Lead: true, Time: true, Grouped: config.AppHeadings, NotificationRules.FoldAt);
    }
}
