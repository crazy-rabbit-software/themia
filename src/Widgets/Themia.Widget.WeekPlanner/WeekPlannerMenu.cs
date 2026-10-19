using Themia.App.Services.Office;
using Themia.App.Services.Tasks;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.Widget.WeekPlanner;

/// <summary>
/// [S-5324] The week planner's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), every calendar's lines (accounts, calendars and colours, birthdays: [S-561], the SDK's
/// <see cref="OfficeMenuLines"/>; birthdays while a chosen account has contacts), then today's
/// tasks: whether they show, and from which account while they do and there is more than one to
/// choose ([S-6074]). It has no settings page.
/// </summary>
internal static class WeekPlannerMenu
{
    /// <param name="office">The host's office source (<see cref="SettingsHost.Office"/>), null before it has started.</param>
    /// <param name="taskAccounts">The accounts with tasks (<see cref="SettingsHost.Tasks"/>).</param>
    public static void Lines(WidgetMenuLines menu, WeekPlannerConfig planner, IOfficeSource? office, IReadOnlyList<TaskAccount> taskAccounts)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(taskAccounts);

        menu.PlaceCard();
        menu.Lines.AddRange(OfficeMenuLines.CalendarAccounts(menu, planner, office));

        // [S-575] Only while a chosen calendar account has the contacts the birthdays come from.
        if (OfficeMenuLines.Birthdays(menu, planner, office) is { } birthdays)
        {
            menu.Lines.Add(birthdays);
        }

        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Toggle(Glyphs.Task, WeekPlannerStr.Planner_ShowTasks.Text(), planner.ShowTasks, on => planner.ShowTasks = on));

        // [S-6074] The account is offered while the tasks show, and only where there is a choice.
        if (planner.Offers(WeekPlannerSetting.TaskAccount) && OffersTaskAccount(planner, taskAccounts))
        {
            menu.Lines.Add(menu.Choice(Glyphs.Contact, WeekPlannerStr.Planner_TaskAccount.Text(), TaskAccountOptions(planner, taskAccounts), planner.TaskAccountId,
                id => planner.TaskAccountId = id));
        }
    }

    /// <summary>
    /// [S-5324] [S-6070] Whether choosing the task account can change what shows: only with two
    /// accounts with tasks or more, or while the chosen one has been signed out (it stays listed by
    /// its id until something else is picked). With one, "the first account" is that account.
    /// </summary>
    internal static bool OffersTaskAccount(WeekPlannerConfig planner, IReadOnlyList<TaskAccount> accounts)
        => accounts.Count > 1 || (planner.TaskAccountId.Length > 0 && accounts.All(a => a.Id != planner.TaskAccountId));

    /// <summary>
    /// [S-5324] The first account with tasks, then each account by name; a chosen account that has
    /// since been signed out stays listed by its id, so the choice is never silently swapped ([S-590]).
    /// </summary>
    internal static List<(string Id, string Text)> TaskAccountOptions(WeekPlannerConfig planner, IReadOnlyList<TaskAccount> accounts)
    {
        var options = new List<(string, string)> { (string.Empty, WeekPlannerStr.Planner_TaskAccountFirst.Text()) };
        options.AddRange(accounts.Select(a => (a.Id, a.Label)));
        if (planner.TaskAccountId.Length > 0 && accounts.All(a => a.Id != planner.TaskAccountId))
        {
            options.Add((planner.TaskAccountId, planner.TaskAccountId));
        }

        return options;
    }
}
