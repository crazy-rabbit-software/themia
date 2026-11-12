using Themia.Core.Habits;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-6006] The habits' sample: five habits with a year of uneven history, held in memory
/// (<see cref="PreviewHabitLists"/>), so nothing touches the user's own habits.
/// </summary>
internal sealed partial class HabitTrackerView : IWidgetSample
{
    private const string PreviewListId = "preview-list";

    public Task ShowSampleAsync()
    {
        ShowHabits(
        [
            new("h1", "Drink water", History(0, 9, 10)),
            new("h2", "Read 20 pages", History(1, 6, 10)),
            new("h3", "Stretch", History(2, 4, 10)),
            new("h4", "Walk 8000 steps", History(3, 7, 10)),
            new("h5", "Journal", History(4, 3, 10)),
        ]);
        return Task.CompletedTask;
    }

    /// <summary>Show <paramref name="habits"/> as a list held in memory.</summary>
    internal void ShowHabits(Habit[] habits)
    {
        if (_pool is not null)
        {
            _pool.Changed -= OnPoolChanged;
        }

        _pool = new PreviewHabitLists([new HabitList(PreviewListId, "Daily", habits)]);
        _pool.Changed += OnPoolChanged;
        _config.ListId = PreviewListId;
        ResolveList();
        Render();
    }

    /// <summary>A year of periods back from the sample moment, done on <paramref name="of"/> of every <paramref name="every"/>, the pattern shifted by <paramref name="seed"/>.</summary>
    private string[] History(int seed, int of, int every)
    {
        var keys = new List<string>();
        var cursor = Bucket(SampleDay.Now);

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
