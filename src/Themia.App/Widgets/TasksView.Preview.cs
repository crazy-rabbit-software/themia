#if DEBUG
using Themia.App.Services.Tasks;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Tasks;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: a list on this PC held in memory, so the rows and the add footer
/// ([S-780], [S-3850]) can be looked at without an account or the user's own lists.
/// </summary>
internal sealed partial class TasksView : IWidgetPreviewState
{
    private const string PreviewListId = "preview-list";

    public IReadOnlyList<string> PreviewStates { get; } = ["tasks", "few", "empty"];

    public Task ShowPreviewStateAsync(string state)
    {
        LocalTask[]? tasks = state switch
        {
            "tasks" =>
            [
                new("t1", "Book the dentist appointment", false),
                new("t2", "Reply to the landlord about the heating", false),
                new("t3", "Pick up the parcel", false),
                new("t4", "Renew the passport before the trip in spring", false),
                new("t5", "Water the plants", false),
                new("t6", "Send the invoice", false),
                new("t7", "Call Grandma", false),
                new("t8", "Back up the laptop", false),
                new("t9", "Order printer ink", false),
                new("t10", "Plan the weekend", false),
            ],
            "few" => [new("t1", "Pick up the parcel", false), new("t2", "Water the plants", false)],
            "empty" => [],
            _ => null,
        };

        if (tasks is null)
        {
            return Task.CompletedTask;
        }

        _config.Source = TaskSource.Local;
        _config.LocalListId = PreviewListId;
        _local = new LocalTaskPool(null, [new LocalTaskList(PreviewListId, "Errands", tasks)]);
        _localListId = PreviewListId;
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
