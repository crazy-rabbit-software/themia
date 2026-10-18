#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Habits;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: a habit list held in memory, so the rows and the add footer
/// ([S-1427], [S-3850]) can be looked at without touching the user's own habits.
/// </summary>
internal sealed partial class HabitTrackerView : IWidgetPreviewState
{
    private const string PreviewListId = "preview-list";

    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "habits", "few", "empty"];

    public Task ShowPreviewStateAsync(string state)
    {
        var today = Bucket(DateTimeOffset.Now).Key;
        Habit[]? habits = state switch
        {
            // [S-6006] Five habits with a year of uneven history, for every style's render.
            "sample" =>
            [
                new("h1", "Drink water", History(0, 9, 10)),
                new("h2", "Read 20 pages", History(1, 6, 10)),
                new("h3", "Stretch", History(2, 4, 10)),
                new("h4", "Walk 8000 steps", History(3, 7, 10)),
                new("h5", "Journal", History(4, 3, 10)),
            ],
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

        _pool = new PreviewHabitLists([new HabitList(PreviewListId, "Daily", habits)]);
        _pool.Changed += OnPoolChanged;
        _config.ListId = PreviewListId;
        ResolveList();
        Render();
        return Task.CompletedTask;
    }

    /// <summary>A year of periods back from now, done on <paramref name="of"/> of every <paramref name="every"/>, the pattern shifted by <paramref name="seed"/>.</summary>
    private string[] History(int seed, int of, int every)
    {
        var keys = new List<string>();
        var cursor = Bucket(DateTimeOffset.Now);

        for (var i = 0; i < 370; i++)
        {
            if ((((i * 7) + (seed * 3) + (i / 11)) % every) < of)
            {
                keys.Add(cursor.Key);
            }

            cursor = HabitPeriods.Previous(cursor, _config.Cadence, TimeZoneInfo.Local);
        }

        return [.. keys];
    }
}
#endif
