using Themia.App.Shell.Gallery;
using Themia.Core.Habits;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>sample</c> is the habits' sample (<see cref="ShowSampleAsync"/>); the rest a
/// habit list held in memory, so the rows and the add footer ([S-1427], [S-3850]) can be looked at
/// without touching the user's own habits.
/// </summary>
internal sealed partial class HabitTrackerView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "habits", "few", "empty", "adding", "typing", "renaming"];

    /// <summary>Words wider than any row, typed into a box: they scroll in it, never wrap it taller.</summary>
    private const string LongWords = "Practise the piano for half an hour before dinner every day";

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        // [S-13950] The inline boxes over the sample: the add row open (empty, then with words longer than
        // the row), a habit's name renamed in place.
        if (state is "adding" or "typing" or "renaming")
        {
            _ = ShowSampleAsync();
            if (state == "renaming" && _rows.Count > 0)
            {
                BeginRename(_rows[0].Id);
            }
            else
            {
                OpenAddEditor();
                if (state == "typing" && _editor is { } editor)
                {
                    editor.Box.Text = LongWords;
                    editor.Box.SelectionStart = LongWords.Length;
                }
            }

            return Task.CompletedTask;
        }

        var today = Bucket(SampleDay.Now).Key;
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

        if (habits is not null)
        {
            ShowHabits(habits);
        }

        return Task.CompletedTask;
    }
}
