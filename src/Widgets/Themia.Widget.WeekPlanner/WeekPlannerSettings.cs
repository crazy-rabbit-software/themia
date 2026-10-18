using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Core.Widgets;
using Themia.App.Shell;
using Themia.App.Shell.Config;

namespace Themia.Widget.WeekPlanner;

/// <summary>
/// [S-5324] The week planner's settings: every calendar's rows (accounts, calendars and colours,
/// birthdays: [S-561]), then today's tasks: whether they show, and from which account. Each writes
/// straight to the live config and saves.
/// </summary>
internal static class WeekPlannerSettings
{
    public static void Rows(WidgetSettingsRows settings, WeekPlannerConfig planner)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(planner);
        var rows = settings.Rows;
        var save = settings.Save;

        // [S-561] Every calendar's rows: the accounts and their calendars, then birthdays.
        OfficeSettingsRows.CalendarAccounts(settings, planner);
        rows.Add(FluentLayout.Header(Str.ConfigTabs_Display.Text()));
        OfficeSettingsRows.Birthdays(settings, planner);

        rows.Add(FluentLayout.Header(WeekPlannerStr.Planner_TasksHeader.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Task, WeekPlannerStr.Planner_ShowTasks.Text(), WeekPlannerStr.Planner_ShowTasksDesc.Text(),
            FluentLayout.Toggle(planner.ShowTasks, value => { planner.ShowTasks = value; save(); })));

        // [S-6074] The account is offered while the tasks show.
        if (!planner.Offers(WeekPlannerSetting.TaskAccount))
        {
            return;
        }

        // The first account with tasks, then each account by name; a chosen account that has since
        // been signed out stays listed by its id, so the choice is never silently swapped ([S-590]).
        var accounts = SettingsHost.Tasks?.Accounts ?? [];
        var ids = new List<string> { string.Empty };
        ids.AddRange(accounts.Select(account => account.Id));

        if (planner.TaskAccountId.Length > 0 && !ids.Contains(planner.TaskAccountId))
        {
            ids.Add(planner.TaskAccountId);
        }

        // Choice takes a value type: the options are indices into the ids.
        var options = ids.Select((id, index) => (index, index == 0
            ? WeekPlannerStr.Planner_TaskAccountFirst.Text()
            : accounts.FirstOrDefault(account => account.Id == id)?.Label ?? id)).ToList();

        rows.Add(FluentLayout.Row(Glyphs.Contact, WeekPlannerStr.Planner_TaskAccount.Text(), null,
            FluentLayout.Choice(options, Math.Max(0, ids.IndexOf(planner.TaskAccountId)), value => { planner.TaskAccountId = ids[value]; save(); })));
    }
}
