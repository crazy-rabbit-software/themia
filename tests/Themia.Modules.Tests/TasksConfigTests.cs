using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Widget.Tasks;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-611] The task widget's config: its filter and its load repair, moved from Core's TaskRulesTests and TaskSyncTests, unchanged.</summary>
public sealed class TasksConfigTests
{
    [Fact]
    public void S611_a_list_on_this_pc_keeps_only_its_text_filter()
    {
        var config = new TasksConfig { Source = TaskSource.Local, DueFilter = TaskDueFilter.Today, ImportantOnly = true, TextFilter = "milk", ShowCompleted = true };
        Assert.Equal(new TaskFilter(true, Text: "milk"), config.Filter(today: false, importance: true));

        config.Source = TaskSource.Account;
        Assert.Equal(new TaskFilter(true, TaskDueFilter.Today, true, "milk", FinishedTodayOnly: true), config.Filter(today: true, importance: true));
    }

    [Fact]
    public void A_valid_config_needs_no_repair()
    {
        var config = new TasksConfig { AccountId = "acc", ListId = TaskRules.TodayListId, TaskOrder = ["a", "b"] };
        Assert.Empty(config.Repair());
        Assert.Equal("acc", config.AccountId);
    }

    [Fact]
    public void S611_a_hand_edited_filter_is_repaired_and_the_clone_carries_every_setting()
    {
        var config = new TasksConfig { DueFilter = (TaskDueFilter)42, TextFilter = new string('x', 500), NewOnTop = true, ImportantOnly = true };
        Assert.Equal(2, config.Repair().Count);
        Assert.Equal(TaskDueFilter.Any, config.DueFilter);
        Assert.Equal(TasksConfig.TextFilterMax, config.TextFilter.Length);

        var clone = (TasksConfig)config.Clone();
        Assert.True(clone.NewOnTop);
        Assert.True(clone.ImportantOnly);
        Assert.Equal(config.TextFilter, clone.TextFilter);
    }
}
