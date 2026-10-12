using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-5324] The week planner's settings: every calendar's rows (accounts, calendars and colours,
/// birthdays: [S-561]), then today's tasks: whether they show, and from which account. Each writes
/// straight to the live config and saves.
/// </summary>
internal static partial class WidgetSettings
{
    public static void WeekPlanner(List<FrameworkElement> rows, WeekPlannerConfig planner, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(save);

        Calendar(rows, planner, save);

        rows.Add(FluentLayout.Header(Str.Planner_TasksHeader.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Task, Str.Planner_ShowTasks.Text(), Str.Planner_ShowTasksDesc.Text(),
            FluentLayout.Toggle(planner.ShowTasks, value => { planner.ShowTasks = value; save(); })));

        // The first account with tasks, then each account by name; a chosen account that has since
        // been signed out stays listed by its id, so the choice is never silently swapped ([S-590]).
        var accounts = AppServices.Tasks?.Accounts ?? [];
        var ids = new List<string> { string.Empty };
        ids.AddRange(accounts.Select(account => account.Id));

        if (planner.TaskAccountId.Length > 0 && !ids.Contains(planner.TaskAccountId))
        {
            ids.Add(planner.TaskAccountId);
        }

        // Choice takes a value type: the options are indices into the ids.
        var options = ids.Select((id, index) => (index, index == 0
            ? Str.Planner_TaskAccountFirst.Text()
            : accounts.FirstOrDefault(account => account.Id == id)?.Label ?? id)).ToList();

        rows.Add(FluentLayout.Row(Glyphs.Contact, Str.Planner_TaskAccount.Text(), null,
            FluentLayout.Choice(options, Math.Max(0, ids.IndexOf(planner.TaskAccountId)), value => { planner.TaskAccountId = ids[value]; save(); })));
    }
}
