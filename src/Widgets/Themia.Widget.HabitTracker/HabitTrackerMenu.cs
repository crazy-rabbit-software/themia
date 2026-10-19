using System.Globalization;
using Themia.App.Widgets;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.HabitTracker;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1429] The Habits widget's options, all on its properties menu ([S-3719]): under Style what only
/// the dots draw (the streak and the history, [S-6408]); then Appearance ([S-3723]), which list it
/// shows (and the lists themselves), how often its boxes clear, and the data folder. It has no
/// settings page.
/// <para>
/// The habits themselves are added, renamed, moved and deleted <i>in the widget</i>, where the user
/// is looking at them ([S-1425]); a second editor here would be a parallel path to the same data.
/// The menu owns the lists (the device-wide pool every Habits widget picks from, the host's, read
/// through <see cref="SettingsHost.Habits"/>) and the widget's own options.
/// </para>
/// </summary>
internal static class HabitTrackerMenu
{
    private static (HabitPeriod, string)[] Periods() =>
    [
        (HabitPeriod.Hourly, HabitTrackerStr.WidgetsB_Settings_Habits_EveryHour.Text()),
        (HabitPeriod.Daily, HabitTrackerStr.WidgetsB_Settings_Habits_EveryDay.Text()),
        (HabitPeriod.Weekly, HabitTrackerStr.WidgetsB_Settings_Habits_EveryWeek.Text()),
        (HabitPeriod.Monthly, HabitTrackerStr.WidgetsB_Settings_Habits_EveryMonth.Text()),
        (HabitPeriod.Custom, HabitTrackerStr.WidgetsB_Settings_Habits_CustomInterval.Text()),
    ];

    /// <param name="pool">The habit lists on this PC, or null where the host has none to offer.</param>
    /// <param name="openFolder">Opens the folder a file is in, in Explorer ([S-1430]: the host's launcher).</param>
    public static void Lines(WidgetMenuLines menu, HabitTrackerConfig config, IHabitLists? pool, Action<string?> openFolder)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(openFolder);

        // [S-6408] The run and the dots are the Dots style's own; another style builds the menu without them ([S-6070]).
        if (config.Offers(HabitsSetting.Streak))
        {
            menu.StyleLines.Add(menu.Toggle(null, HabitTrackerStr.WidgetsB_Settings_Habits_Streaks_Label.Text(), config.ShowStreak, on => config.ShowStreak = on));
        }

        if (config.Offers(HabitsSetting.History))
        {
            // [S-1426] Zero removes the strip.
            menu.StyleLines.Add(menu.Number(Glyphs.Count, HabitTrackerStr.WidgetsB_Settings_Habits_History_Label.Text(),
                config.EffectiveHistoryDots, 0, HabitRules.MaxHistory, 1,
                value => value.ToString(CultureInfo.InvariantCulture), value => config.HistoryDots = value));
        }

        menu.PlaceCard();
        menu.Lines.Add(Lists(menu, config, pool));

        menu.Lines.Add(menu.Choice(Glyphs.Calendar, HabitTrackerStr.WidgetsB_Settings_Habits_Resets_Label.Text(), Periods(), config.Cadence.Period, value => config.Period = value));

        // [S-6074] The minutes mean something under the custom period only; picking it builds the
        // menu again with them. [S-1422] Read by the cadence's own rule, its reason under the box.
        if (config.Offers(HabitsSetting.Interval))
        {
            menu.Lines.Add(new ItemMenuEntry(Glyphs.Countdown, HabitTrackerStr.HabitTracker_IntervalLabel.Text(), () => menu.Ask(new WidgetTextAsk(
                HabitTrackerStr.HabitTracker_IntervalLabel.Text(),
                HabitTrackerStr.WidgetsB_Settings_Habits_Interval_Desc.Format(("min", HabitCadence.MinCustomMinutes), ("max", HabitCadence.MaxCustomMinutes)),
                Str.Common_Save.Text(),
                typed => HabitCadence.TryParseMinutes(typed, out _, out var refusal) ? null : refusal ?? string.Empty,
                typed =>
                {
                    if (HabitCadence.TryParseMinutes(typed, out var minutes, out _) && minutes != config.CustomMinutes)
                    {
                        config.CustomMinutes = minutes;
                        menu.Save();
                    }
                },
                Initial: config.Cadence.Minutes.ToString(CultureInfo.InvariantCulture))),
                Value: $"{config.Cadence.Minutes}{Str.WidgetsC_Pomodoro_MinutesUnit.Text()}"));
        }

        // [S-1430] The way to the file itself, for a backup or another program: the habits are kept
        // in plain JSON beside the settings file, a folder nobody finds on their own.
        var file = pool?.FilePath;
        menu.Lines.Add(WidgetMenuLines.Submenu(Glyphs.Folder, HabitTrackerStr.WidgetsB_Settings_Habits_DataFolder_Label.Text(),
        [
            WidgetMenuLines.Note(HabitTrackerStr.WidgetsB_Settings_Habits_DataFolder_Desc.Text()),
            new(Glyphs.FolderOpen, Str.Folder_OpenInExplorer.Text(), () => openFolder(file), Disabled: file is null),
        ]));
    }

    /// <summary>
    /// [S-1429] The habit lists on this PC: the one this widget shows with the dot (each list's habit
    /// count at its end), then Rename and Delete for that list and Add a list. A pool every Habits
    /// widget shares, so renaming retitles each widget showing it, and deleting is always asked first.
    /// </summary>
    private static ItemMenuEntry Lists(WidgetMenuLines menu, HabitTrackerConfig config, IHabitLists? pool)
    {
        var title = HabitTrackerStr.WidgetsB_Settings_Habits_List.Text();
        if (pool is null)
        {
            return WidgetMenuLines.Submenu(Glyphs.Checklist, title, [WidgetMenuLines.Note(HabitTrackerStr.WidgetsB_Settings_Habits_ListsUnavailable.Text())]);
        }

        var lines = new List<ItemMenuEntry>();
        if (pool.Problem is { } problem)
        {
            lines.Add(WidgetMenuLines.Note(problem));
        }

        foreach (var list in pool.Lists)
        {
            var id = list.Id;
            lines.Add(new ItemMenuEntry(null, Shown(list), () =>
            {
                if (config.ListId != id)
                {
                    config.ListId = id;
                    menu.Save();
                }
            }, Checked: id == config.ListId, Group: title, Value: HabitTrackerStr.WidgetsB_Settings_Habits_HabitCount.Plural(list.Habits.Count)));
        }

        if (pool.Lists.Count == 0)
        {
            lines.Add(WidgetMenuLines.Note(HabitTrackerStr.WidgetsB_Settings_Habits_NoLists.Text()));
        }

        lines.Add(ItemMenuEntry.Separator);
        var current = pool.Find(config.ListId);
        if (current is not null)
        {
            var currentId = current.Id;
            lines.Add(new ItemMenuEntry(Glyphs.Rename, Str.Menu_Rename.Text(), () => menu.Ask(new WidgetTextAsk(
                Str.Common_Rename.Text(),
                HabitTrackerStr.HabitTracker_Lists_NamePlaceholder.Text(),
                Str.Common_Rename.Text(),
                _ => null,
                typed =>
                {
                    var name = typed.Trim();
                    if (name != current.Name)
                    {
                        pool.Rename(currentId, name);
                        menu.Save();
                    }
                },
                Initial: current.Name)), Disabled: !pool.Writable));
        }

        lines.Add(menu.Command(Glyphs.Add, HabitTrackerStr.WidgetsB_Settings_Habits_AddList.Text(), () =>
        {
            if (pool.Create(HabitLists.NewListName) is { } id)
            {
                config.ListId = id;
            }
        }, disabled: !pool.Writable));

        if (current is not null)
        {
            // [S-1429] Always asked, empty or not: it takes every habit and streak in it.
            var label = Shown(current);
            var question = current.Habits.Count == 0
                ? HabitTrackerStr.WidgetsB_Settings_Habits_DeleteEmpty.Format(("name", label))
                : HabitTrackerStr.WidgetsB_Settings_Habits_DeleteFull.Plural(current.Habits.Count, ("name", label));
            var currentId = current.Id;

            lines.Add(ItemMenuEntry.Separator);

            // Every widget showing it says so rather than being handed another list's habits.
            lines.Add(menu.Confirm(Glyphs.Delete, HabitTrackerStr.WidgetsB_Settings_Habits_DeleteList.Text(), question, Str.Common_Delete.Text(),
                () => pool.Delete(currentId), danger: true, disabled: !pool.Writable));
        }

        return WidgetMenuLines.Submenu(Glyphs.Checklist, title, lines) with { Value = current is null ? null : Shown(current) };
    }

    /// <summary>A list's name, or "Untitled list".</summary>
    private static string Shown(HabitList list)
        => string.IsNullOrWhiteSpace(list.Name) ? HabitTrackerStr.WidgetsB_Settings_Habits_UntitledList.Text() : list.Name;
}
