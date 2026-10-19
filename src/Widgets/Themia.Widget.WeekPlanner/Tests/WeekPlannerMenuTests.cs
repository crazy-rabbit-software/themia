using Themia.App.Services.Tasks;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.WeekPlanner.Tests;

/// <summary>[S-5324] The week planner's options on its properties menu ([S-3719]).</summary>
public sealed class WeekPlannerMenuTests
{
    private int _saves;

    private static readonly IReadOnlyList<TaskAccount> Work = [new("a", HostProviders.Microsoft, "Work", null)];

    private WidgetMenuLines Build(WeekPlannerConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        WeekPlannerMenu.Lines(lines, config, office: null, Work);
        return lines;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    [Fact]
    public void S5324_the_menu_is_appearance_the_calendar_lines_then_todays_tasks_and_their_account()
    {
        var lines = Build(new WeekPlannerConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [Str.WidgetUi_AccountsLabel.Text(), Str.Calendar_ShowBirthdays_Label.Text(), WeekPlannerStr.Planner_ShowTasks.Text(), WeekPlannerStr.Planner_TaskAccount.Text()],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
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

        Assert.Equal([WeekPlannerStr.Planner_TaskAccountFirst.Text(), "Work", "gone"], options.Select(o => o.Text));
        Assert.True(options[2].Checked);

        options[1].Action!();
        Assert.Equal("a", config.TaskAccountId);
        Assert.Equal(1, _saves);
    }
}
