using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2055] The tasks' sample: Robin's errands, the week planner's list, as a list on this PC
/// held in memory, so the rows and the add footer ([S-780], [S-3850]) draw without an account or the
/// user's own lists. Nothing is saved.
/// </summary>
internal sealed partial class TasksView : IWidgetSample
{
    private const string PreviewListId = "preview-list";

    public Task ShowSampleAsync()
    {
        ShowLocalSample([.. SampleTasks.Errands.Select((task, i) => new LocalTask($"t{i + 1}", task.Title, false))], showCompleted: false);
        return Task.CompletedTask;
    }

    /// <summary><paramref name="tasks"/> as a list on this PC held in memory (<see cref="MemoryTaskLists"/>), never the host's.</summary>
    private void ShowLocalSample(LocalTask[] tasks, bool showCompleted)
    {
        _config.Source = TaskSource.Local;
        _config.ShowCompleted = showCompleted;
        _config.LocalListId = PreviewListId;
        _local = new MemoryTaskLists([new LocalTaskList(PreviewListId, SampleTasks.ListName, tasks)]);
        _localListId = PreviewListId;
        PublishTitle();
        Render();
    }
}
