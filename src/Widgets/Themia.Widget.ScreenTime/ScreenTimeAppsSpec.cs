using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.ScreenTime;

/// <summary>
/// [S-3350] [S-11510] The screen-time app list on the kit ([S-7074]): rows of the rich look, the app's icon as the lead
/// (or the dot of its tone, [S-2272]; 32 on rows of two lines), its name, its time as the value (a reading, [S-7072]; in
/// the critical role over its own limit, [S-457]), the share and a window's average on the second line where the row
/// has one. The busiest apps first, the last row "Other" when apps are left over, so the rows add up to the total.
/// </summary>
public static class ScreenTimeAppsSpec
{
    /// <summary>Hide the app from every screen-time widget.</summary>
    public const string HideId = "hide";

    public static KitSpec Spec { get; } = new(
        KitRowLook.Rich,
        KitShow.Lead | KitShow.Subtitle | KitShow.When,
        static () => string.Empty,
        static () => Str.Screentime_NoActivity.Text(),
        KitTime.None)
    {
        Readings = true,
    };

    /// <summary>[S-3350] A segment as a kit item: its key the id, its time the value, over its limit Critical, its figures and the tip.</summary>
    public static KitItem Item(ScreenTimeSegment segment, string? figures, ScreenTimeGoal? goal, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(segment);

        var time = ScreenTimeRules.FormatDuration(segment.Seconds);
        var tip = $"{segment.Name} — {time}";
        if (figures is not null)
        {
            tip += $" · {figures}";
        }

        if (goal is { } g)
        {
            tip += $" · {(g.Over ? ScreenTimeStr.WidgetsC_ScreenTime_LimitOverTip : ScreenTimeStr.WidgetsC_ScreenTime_LimitTip).Format(locale, ("limit", ScreenTimeRules.FormatLimit(g.Limit)))}";
        }

        return new KitItem(
            segment.Key,
            segment.Name,
            Subtitle: figures,
            Lead: new KitLead(KitLeadKind.Icon, segment.Path ?? segment.Key),
            Tone: goal is { Over: true } ? KitTone.Critical : KitTone.Neutral,
            Tooltip: tip,
            Value: time);
    }

    /// <summary>[S-3350] An app's menu: Hide it (never Other: hiding has no undo).</summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Id == ScreenTimeRules.OtherKey ? [] : [new KitAction(KitActionKind.Change, Str.Screentime_HideApp.Text(), KitIcon.Hide, Id: HideId)];
    }
}
