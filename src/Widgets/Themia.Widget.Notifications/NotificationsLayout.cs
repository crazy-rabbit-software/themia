namespace Themia.Core.Widgets;

/// <summary>[S-4404] What a notifications widget shows at a size.</summary>
/// <param name="Face">The smallest tile: the bell and how many, nothing else.</param>
/// <param name="Grouped">Under app headings (icon, name, clear) rather than flat with an icon per row.</param>
/// <param name="BodyLines">Lines of each notification's body under its title (0: the title alone).</param>
/// <param name="Folded">Notifications an app shows before "See more" when grouped.</param>
public readonly record struct NotificationPlan(bool Face, bool Grouped, int BodyLines, int Folded);

/// <summary>[S-4404] The notification widget's ladder.</summary>
public static class NotificationsLayout
{
    /// <summary>[S-4404] Below two compact rows of height the list is the bell and the count.</summary>
    public const double FaceBelow = 64;

    /// <summary>
    /// [S-4404] The ladder: the bell and a count under 180 wide or two rows high; then the newest flat,
    /// titles alone while the height is compact, with each row's app icon; from a medium height grouped under app headings; the body gets a
    /// line from a small height, two from medium, three from large. Costs are line heights.
    /// </summary>
    public static NotificationPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new NotificationPlan(Face: false, Grouped: false, BodyLines: 0, Folded: NotificationRules.FoldAt);
        }

        if (area.WidthClass == SizeClass.Compact || area.Height < FaceBelow)
        {
            // Under 180 wide a title does not fit beside a time; under two rows high there is no list.
            return new NotificationPlan(Face: true, Grouped: false, BodyLines: 0, Folded: NotificationRules.FoldAt);
        }

        var bodyLines = area.HeightClass switch
        {
            SizeClass.Compact => 0,
            SizeClass.Regular => 1,
            SizeClass.Expanded => 2,
            _ => 3,
        };

        return new NotificationPlan(Face: false, Grouped: area.HeightClass >= SizeClass.Expanded, BodyLines: bodyLines, Folded: NotificationRules.FoldAt);
    }
}
