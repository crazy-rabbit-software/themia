using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.HabitTracker;

/// <summary>
/// [S-1420] [S-7074] The habits on the kit's check look ([S-7037]), as Tasks: a habit's check as the lead (the one
/// pointer target that ticks it, [S-1421]), its name, its current run as the value, its rate and best run on the second
/// line where the row has two ([S-11531]), the history dots at the row's end ([S-1426]); renamed in place, dragged to a
/// new place, the add row under the list ([S-1425], [S-3850]).
/// </summary>
public static class HabitSpec
{
    /// <summary>The action ids the view carries out.</summary>
    public const string ToggleId = "toggle";

    public const string RenameId = "rename";

    public const string DeleteId = "delete";

    public static KitSpec Spec { get; } = new(KitRowLook.Check, KitShow.Subtitle | KitShow.When, static () => HabitTrackerStr.WidgetsB_Habits_Untitled.Text(), static () => HabitTrackerStr.WidgetsB_Habits_Empty.Text(), KitTime.None)
    {
        AddLabel = static () => HabitTrackerStr.WidgetsB_Habits_AddHabit.Text(),
        AddPlaceholder = static () => HabitTrackerStr.WidgetsB_Habits_NamePlaceholder.Text(),
    };

    /// <summary>
    /// [S-1421] [S-1426] [S-11531] A habit as a row in <paramref name="current"/>: done this period or not, its run as the
    /// value as the plan writes it (a number, or in words), its rate (with the best run where streaks show) the subtitle,
    /// its state in words as the tooltip.
    /// </summary>
    public static KitItem Item(Habit habit, IReadOnlySet<string> log, HabitBucket current, HabitCadence cadence, HabitLayoutPlan plan, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(habit);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(plan);

        var zone = TimeZoneInfo.Local;
        var name = string.IsNullOrWhiteSpace(habit.Name) ? HabitTrackerStr.WidgetsB_Habits_Untitled.Text(locale) : habit.Name;
        var done = HabitRules.IsDone(log, current);
        var noun = HabitPeriods.CurrentNoun(cadence.Period);
        var streak = HabitRules.Streak(log, current, cadence, zone);
        var value = plan.Streak && streak > 0
            ? plan.StreakWords ? HabitPeriods.Units(cadence.Period, streak) : LocaleFormat.Integer(locale, streak)
            : null;

        var rate = HabitTrackerStr.WidgetsB_Habits_DoneOfLast.Format(locale, ("done", HabitRules.DoneIn(log, current, cadence, zone, HabitRules.RateWindow)), ("units", HabitPeriods.Units(cadence.Period, HabitRules.RateWindow)));
        if (plan.Streak)
        {
            var best = HabitRules.BestStreak(log, current, cadence, zone);
            rate = $"{rate} · {(best == 0 ? HabitTrackerStr.WidgetsB_Habits_NoBest.Text(locale) : HabitTrackerStr.WidgetsB_Habits_BestRun.Format(locale, ("units", HabitPeriods.Units(cadence.Period, best))))}";
        }

        return new KitItem(
            habit.Id,
            habit.Name,
            Subtitle: rate,
            Lead: KitLead.Check(done),
            Tooltip: (done ? HabitTrackerStr.WidgetsB_Habits_StateDone : HabitTrackerStr.WidgetsB_Habits_StateNotDone).Format(locale, ("name", name), ("when", noun)),
            Value: value);
    }

    /// <summary>[S-1425] A habit's menu: tick or untick it this period, Rename (F2), Delete (Delete); none where the list cannot be written.</summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item, HabitPeriod period, bool writable)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!writable)
        {
            return [];
        }

        var noun = HabitPeriods.CurrentNoun(period);
        var done = item.Lead.IsDone;
        return
        [
            new KitAction(KitActionKind.Toggle, (done ? HabitTrackerStr.WidgetsB_Habits_MarkNotDone : HabitTrackerStr.WidgetsB_Habits_MarkDone).Format(("when", noun)), done ? KitIcon.Undo : KitIcon.Check, Id: ToggleId),
            new KitAction(KitActionKind.Rename, Str.WidgetsB_Folder_Rename.Text(), Id: RenameId),
            new KitAction(KitActionKind.Delete, Str.Common_Delete.Text(), Id: DeleteId),
        ];
    }
}
