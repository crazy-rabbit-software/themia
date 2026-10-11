#if DEBUG
using Themia.App.Services.Habits;
using Themia.App.Shell.Gallery;
using Themia.Core.Habits;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: a habit list held in memory, so the rows and the add footer
/// ([S-1427], [S-3850]) can be looked at without touching the user's own habits.
/// </summary>
internal sealed partial class HabitTrackerView : IWidgetPreviewState
{
    private const string PreviewListId = "preview-list";

    public IReadOnlyList<string> PreviewStates { get; } = ["habits", "few", "empty"];

    public Task ShowPreviewStateAsync(string state)
    {
        var today = Bucket(DateTimeOffset.Now).Key;
        Habit[]? habits = state switch
        {
            "habits" =>
            [
                new("h1", "Drink water", [today]),
                new("h2", "Read 20 pages", []),
                new("h3", "Stretch", [today]),
                new("h4", "Walk 8000 steps", []),
                new("h5", "No phone after 22:00", []),
                new("h6", "Practise Spanish", [today]),
                new("h7", "Journal", []),
                new("h8", "Meditate", []),
                new("h9", "Floss", []),
                new("h10", "Tidy the desk", []),
            ],
            "few" => [new("h1", "Drink water", [today]), new("h2", "Read 20 pages", [])],
            "empty" => [],
            _ => null,
        };

        if (habits is null)
        {
            return Task.CompletedTask;
        }

        if (_pool is not null)
        {
            _pool.Changed -= OnPoolChanged;
        }

        _pool = new HabitPool(null, [new HabitList(PreviewListId, "Daily", habits)]);
        _pool.Changed += OnPoolChanged;
        _config.ListId = PreviewListId;
        ResolveList();
        Render();
        return Task.CompletedTask;
    }
}
#endif
