using Themia.App.Services.Accounts;
using Themia.App.Services.Office;
using Themia.App.Services.Tasks;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.WeekPlanner.Tests;

/// <summary>[S-5324] The week planner's options on its properties menu ([S-3719]).</summary>
public sealed class WeekPlannerMenuTests
{
    private int _saves;

    private static readonly IReadOnlyList<TaskAccount> Work = [new("a", HostProviders.Microsoft, "Work", null), new("b", HostProviders.Google, "Home", null)];

    private WidgetMenuLines Build(WeekPlannerConfig config, IReadOnlyList<TaskAccount>? taskAccounts = null, IOfficeSource? office = null)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        WeekPlannerMenu.Lines(lines, config, office ?? new Office([new("work", HostProviders.Microsoft, "work@example.com", "Work", null, null, null)]), taskAccounts ?? Work);
        return lines;
    }

    /// <summary>The calendar accounts, with no calendar lists yet.</summary>
    private sealed class Office(IReadOnlyList<AccountInfo> accounts) : IOfficeSource
    {
        public event Action? Changed { add { } remove { } }

        public IReadOnlyList<AccountInfo> Accounts(FeatureId feature) => accounts;

        public Task<Synced<IReadOnlyList<CalendarEvent>>> EventsAsync(CalendarTarget target, DateTimeOffset from, DateTimeOffset to, bool force, CancellationToken cancellation)
            => throw new NotSupportedException();

        public Task<Synced<IReadOnlyList<CalendarInfo>>> CalendarsAsync(string accountId, bool force, CancellationToken cancellation)
            => new TaskCompletionSource<Synced<IReadOnlyList<CalendarInfo>>>().Task;

        public Task<Synced<IReadOnlyList<Birthday>>> BirthdaysAsync(string accountId, bool force, CancellationToken cancellation)
            => throw new NotSupportedException();

        public Task<Synced<IReadOnlyList<Contact>>> ContactsAsync(string accountId, int max, bool force, CancellationToken cancellation)
            => throw new NotSupportedException();
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    [Fact]
    public void S5324_the_menu_is_appearance_the_calendar_lines_then_todays_tasks_and_their_account()
    {
        var lines = Build(new WeekPlannerConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [
                Str.WidgetUi_AccountsLabel.Text(), Str.WidgetsA_Calendar_Settings_Calendars.Text(), Str.Calendar_ShowBirthdays_Label.Text(), string.Empty,
                WeekPlannerStr.Planner_ShowTasks.Text(), WeekPlannerStr.Planner_TaskAccount.Text(),
            ],
            lines.Lines.Select(l => l.IsSeparator ? string.Empty : l.Text));
    }

    [Fact]
    public void S5324_birthdays_are_offered_only_while_a_chosen_calendar_account_has_contacts()
    {
        var dav = new Office([new("dav", HostProviders.CalDav, "dav@example.com", "Dav", null, "https://dav.example.com/", null)]);

        Assert.DoesNotContain(Build(new WeekPlannerConfig(), office: dav).Lines, l => l.Text == Str.Calendar_ShowBirthdays_Label.Text());
    }

    [Fact]
    public void S5324_the_account_is_not_offered_with_one_account_with_tasks_unless_the_chosen_one_is_signed_out()
    {
        IReadOnlyList<TaskAccount> one = [Work[0]];

        Assert.DoesNotContain(Build(new WeekPlannerConfig(), one).Lines, l => l.Text == WeekPlannerStr.Planner_TaskAccount.Text());
        Assert.DoesNotContain(Build(new WeekPlannerConfig { TaskAccountId = "a" }, one).Lines, l => l.Text == WeekPlannerStr.Planner_TaskAccount.Text());
        Assert.DoesNotContain(Build(new WeekPlannerConfig(), []).Lines, l => l.Text == WeekPlannerStr.Planner_TaskAccount.Text());
        Assert.Contains(Build(new WeekPlannerConfig { TaskAccountId = "gone" }, one).Lines, l => l.Text == WeekPlannerStr.Planner_TaskAccount.Text());
    }

    [Fact]
    public void S5324_the_account_is_offered_while_the_tasks_show()
    {
        var config = new WeekPlannerConfig();
        Line(Build(config).Lines, WeekPlannerStr.Planner_ShowTasks.Text()).Action!();

        Assert.False(config.ShowTasks);
        Assert.DoesNotContain(Build(config).Lines, l => l.Text == WeekPlannerStr.Planner_TaskAccount.Text());
    }

    [Fact]
    public void S5324_the_first_account_with_tasks_then_each_and_a_signed_out_one_by_its_id()
    {
        var config = new WeekPlannerConfig { TaskAccountId = "gone" };
        var options = Line(Build(config).Lines, WeekPlannerStr.Planner_TaskAccount.Text()).Children!;

        Assert.Equal([WeekPlannerStr.Planner_TaskAccountFirst.Text(), "Work", "Home", "gone"], options.Select(o => o.Text));
        Assert.True(options[3].Checked);

        options[1].Action!();
        Assert.Equal("a", config.TaskAccountId);
        Assert.Equal(1, _saves);
    }
}
