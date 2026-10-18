using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.System;
using Themia.Widget.HabitTracker;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1429] The Habits widget's settings: which list it shows, and how often and how it reads.
/// <para>
/// The habits themselves are added, renamed, moved and deleted <i>in the widget</i>, where the user
/// is looking at them ([S-1425]); a second editor here would be a parallel path to the same data.
/// This page owns the lists — the device-wide pool every Habits widget picks from — and the widget's
/// own settings.
/// </para>
/// </summary>
internal static class HabitTrackerSettings
{
    private const string HabitsScope = "habits-settings";

    public static void Rows(List<FrameworkElement> rows, HabitTrackerConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(HabitTrackerStr.WidgetsB_Settings_Habits_List.Text()));

        if (SettingsHost.Habits is not { } pool)
        {
            rows.Add(FluentLayout.Note(HabitTrackerStr.WidgetsB_Settings_Habits_ListsUnavailable.Text()));
        }
        else
        {
            // The rows change as lists are added and deleted, so they live in one host rebuilt in place.
            var lists = new StackPanel { Spacing = Fluent.Space1 };
            rows.Add(lists);

            void Rebuild()
            {
                lists.Children.Clear();

                foreach (var row in HabitListRows(pool, config, save, Rebuild))
                {
                    lists.Children.Add(row);
                }
            }

            Rebuild();
        }

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Email_Display.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Calendar, HabitTrackerStr.WidgetsB_Settings_Habits_Resets_Label.Text(), HabitTrackerStr.HabitTracker_ResetDesc.Text(),
            FluentLayout.Choice<HabitPeriod>(
                [
                    (HabitPeriod.Hourly, HabitTrackerStr.WidgetsB_Settings_Habits_EveryHour.Text()),
                    (HabitPeriod.Daily, HabitTrackerStr.WidgetsB_Settings_Habits_EveryDay.Text()),
                    (HabitPeriod.Weekly, HabitTrackerStr.WidgetsB_Settings_Habits_EveryWeek.Text()),
                    (HabitPeriod.Monthly, HabitTrackerStr.WidgetsB_Settings_Habits_EveryMonth.Text()),
                    (HabitPeriod.Custom, HabitTrackerStr.WidgetsB_Settings_Habits_CustomInterval.Text()),
                ],
                config.Cadence.Period,
                value =>
                {
                    config.Period = value;
                    save();
                })));

        // [S-6074] The minutes mean something under the custom period only; picking it builds the
        // page again with them ([S-6071]).
        if (config.Offers(HabitsSetting.Interval))
        {
            var verdict = FluentLayout.Verdict();
            var minutes = new TextBox
            {
                Text = config.Cadence.Minutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MinWidth = 120,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            void CommitMinutes()
            {
                if (!HabitCadence.TryParseMinutes(minutes.Text, out var value, out var refusal))
                {
                    FluentLayout.Refuse(verdict, refusal ?? string.Empty);
                    return;
                }

                FluentLayout.Confirm(verdict, HabitTrackerStr.WidgetsB_Settings_Habits_ClearsEvery.Plural(value));

                if (value != config.CustomMinutes)
                {
                    config.CustomMinutes = value;
                    save();
                }
            }

            minutes.LostFocus += (_, _) => CommitMinutes();
            minutes.KeyDown += (_, args) =>
            {
                if (args.Key == VirtualKey.Enter)
                {
                    args.Handled = true;
                    CommitMinutes();
                }
            };

            rows.Add(FluentLayout.FieldRow(Glyphs.Countdown, HabitTrackerStr.HabitTracker_IntervalLabel.Text(), HabitTrackerStr.WidgetsB_Settings_Habits_Interval_Desc.Format(("min", HabitCadence.MinCustomMinutes), ("max", HabitCadence.MaxCustomMinutes)), minutes, verdict));
        }

        // [S-6408] The run and the dots are the Dots style's own; another style rebuilds the page without them ([S-6071]).
        if (config.Offers(HabitsSetting.Streak))
        {
            rows.Add(FluentLayout.Row(Glyphs.Done, HabitTrackerStr.WidgetsB_Settings_Habits_Streaks_Label.Text(), HabitTrackerStr.WidgetsB_Settings_Habits_Streaks_Desc.Text(),
                FluentLayout.Toggle(config.ShowStreak, value => { config.ShowStreak = value; save(); })));
        }

        if (config.Offers(HabitsSetting.History))
        {
            rows.Add(FluentLayout.Row(Glyphs.Count, HabitTrackerStr.WidgetsB_Settings_Habits_History_Label.Text(),
                HabitTrackerStr.WidgetsB_Settings_Habits_History_Desc.Format(("zero", 0)),
                FluentLayout.Slider(
                    config.EffectiveHistoryDots,
                    0,
                    HabitRules.MaxHistory,
                    1,
                    string.Empty,
                    value => { config.HistoryDots = (int)Math.Round(value); save(); })));
        }

        // [S-1430] The way to the file itself, for a backup or another program: the habits are kept
        // in plain JSON beside the settings file, a folder nobody finds on their own.
        var file = SettingsHost.Habits?.FilePath;
        var open = new Button { Content = Str.Folder_OpenInExplorer.Text(), IsEnabled = file is not null };
        open.Click += (_, _) => SettingsHost.Launcher.OpenContainingFolder(file);
        rows.Add(FluentLayout.Row(Glyphs.Folder, HabitTrackerStr.WidgetsB_Settings_Habits_DataFolder_Label.Text(),
            HabitTrackerStr.WidgetsB_Settings_Habits_DataFolder_Desc.Text(), open));
    }

    /// <summary>
    /// [S-1429] The habit lists on this PC: pick one, rename it as you type, add one, delete one after
    /// asking. A pool every Habits widget shares, so renaming here retitles each widget showing it.
    /// </summary>
    private static IEnumerable<FrameworkElement> HabitListRows(IHabitLists pool, HabitTrackerConfig config, Action save, Action rebuild)
    {
        if (pool.Problem is { } problem)
        {
            yield return FluentLayout.Note(problem);
        }

        foreach (var list in pool.Lists)
        {
            var listId = list.Id;

            var pick = new RadioButton
            {
                GroupName = "habit-lists",
                IsChecked = listId == config.ListId,
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };

            pick.Checked += (_, _) =>
            {
                if (config.ListId != listId)
                {
                    config.ListId = listId;
                    save();
                }
            };

            var name = new TextBox
            {
                Text = list.Name,
                PlaceholderText = HabitTrackerStr.HabitTracker_Lists_NamePlaceholder.Text(),
                MinWidth = 160,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = pool.Writable,
            };

            // Written through as it is typed; a blank name is kept so the box can be cleared.
            name.TextChanged += (_, _) => pool.Rename(listId, name.Text);

            var count = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            count.Text = HabitTrackerStr.WidgetsB_Settings_Habits_HabitCount.Plural(list.Habits.Count);
            count.VerticalAlignment = VerticalAlignment.Center;

            var remove = FluentLayout.IconCommand(Glyphs.Delete, HabitTrackerStr.WidgetsB_Settings_Habits_DeleteList.Text());
            remove.IsEnabled = pool.Writable;
            remove.Click += async (_, _) =>
            {
                try
                {
                    // [S-1429] Always asked, empty or not: it takes every habit and streak in it, and
                    // the button sits one row from the name being typed in.
                    var label = string.IsNullOrWhiteSpace(list.Name) ? HabitTrackerStr.WidgetsB_Settings_Habits_UntitledList.Text() : list.Name;
                    var detail = list.Habits.Count == 0
                        ? HabitTrackerStr.WidgetsB_Settings_Habits_DeleteEmpty.Format(("name", label))
                        : HabitTrackerStr.WidgetsB_Settings_Habits_DeleteFull.Plural(list.Habits.Count, ("name", label));

                    if (!await FluentLayout.ConfirmAsync(remove, HabitTrackerStr.WidgetsB_Settings_Habits_DeleteQuestion.Text(), detail, Str.Common_Delete.Text()).ConfigureAwait(true))
                    {
                        return;
                    }

                    pool.Delete(listId);

                    // Every widget showing it says so rather than being handed another list's habits.
                    rebuild();
                }
                catch (Exception ex)
                {
                    Log.Error(HabitsScope, "could not delete the habit list", ex);
                }
            };

            var line = new Grid
            {
                ColumnSpacing = Fluent.Space2,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
            };

            Grid.SetColumn(name, 1);
            Grid.SetColumn(count, 2);
            Grid.SetColumn(remove, 3);
            line.Children.Add(pick);
            line.Children.Add(name);
            line.Children.Add(count);
            line.Children.Add(remove);

            yield return FluentLayout.Card(line);
        }

        if (pool.Lists.Count == 0)
        {
            yield return FluentLayout.Note(HabitTrackerStr.WidgetsB_Settings_Habits_NoLists.Text());
        }

        var add = new Button { Content = HabitTrackerStr.WidgetsB_Settings_Habits_AddList.Text(), IsEnabled = pool.Writable };
        add.Click += (_, _) =>
        {
            if (pool.Create(HabitLists.NewListName) is { } id)
            {
                config.ListId = id;
                save();
            }

            rebuild();
        };

        yield return add;
    }
}
