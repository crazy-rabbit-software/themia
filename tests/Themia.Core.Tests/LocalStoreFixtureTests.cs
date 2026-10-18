using System.Runtime.CompilerServices;
using Themia.Core.Habits;
using Themia.Core.Notes;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The files the local-store widgets keep beside the state, as users have them: a notes folder
/// (<c>index.json</c> and one <c>.md</c> a note: markdown, non-ASCII text, CRLF and LF mixed, an empty
/// body, a renamed note), <c>habits.json</c> (lists with and without habits and ticks, day and week
/// keys) and <c>usage_stats.json</c> (days, names, paths, switch counts, hidden apps, a goal and an
/// app limit). Generated once with the build from before those widgets became projects (`d02d36bf`;
/// Fixtures/local, made-up content, not anyone's real files). The stores stay host code; this pins
/// that such files still load with no repair and write back byte for byte, so a real profile keeps
/// working without a migration.
/// </summary>
public sealed class LocalStoreFixtureTests
{
    private static string Fixture(string name, [CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "local", name);

    private static string CopyOf(string name)
    {
        var target = Path.Combine(Path.GetTempPath(), $"themia-local-{Guid.NewGuid():N}", name);
        var source = Fixture(name);
        if (Directory.Exists(source))
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }

        return target;
    }

    private static Dictionary<string, byte[]> Contents(string directory)
        => Directory.EnumerateFiles(directory).ToDictionary(f => Path.GetFileName(f), File.ReadAllBytes);

    [Fact]
    public void S1343_a_notes_folder_loads_whole_without_a_repair()
    {
        var folder = CopyOf("notes");
        var store = new NoteStore(folder);

        var notes = store.List();

        Assert.Equal(
            [("0f1e2d3c4b5a69788796a5b4c3d2e1f0", "Shopping", 1790841844L), ("1a2b3c4d5e6f708192a3b4c5d6e7f809", "Ideen für den Herbst", 1790841722L), ("2b3c4d5e6f708192a3b4c5d6e7f8091a", "Empty (renamed)", 1790841783L)],
            notes.Select(n => (n.Id, n.Title, n.UpdatedAt)).OrderBy(n => n.Id, StringComparer.Ordinal));
        Assert.All(notes, n => Assert.Equal(NoteReadStatus.Found, store.Read(n.Id).Status));
        Assert.Equal(File.ReadAllText(Fixture("notes/1a2b3c4d5e6f708192a3b4c5d6e7f809.md")), store.Read("1a2b3c4d5e6f708192a3b4c5d6e7f809").Text);
        Assert.Equal(string.Empty, store.Read("2b3c4d5e6f708192a3b4c5d6e7f8091a").Text);

        // Nothing was set aside, pruned or rewritten.
        Assert.Equal(Contents(Fixture("notes")), Contents(folder));
    }

    [Fact]
    public void S1343_a_notes_index_writes_back_byte_for_byte()
    {
        var json = File.ReadAllText(Fixture("notes/index.json"));

        var (notes, dropped, refusal) = NoteStore.ParseIndex(json);

        Assert.Null(refusal);
        Assert.Equal(0, dropped);
        Assert.Equal(json, NoteStore.SerializeIndex(notes));
    }

    [Fact]
    public void S1420_a_habits_file_loads_whole_without_a_repair_and_writes_back_byte_for_byte()
    {
        var load = new HabitListStore(CopyOf("habits.json")).Load();

        Assert.Equal(LoadOutcome.Loaded, load.Outcome);
        Assert.Null(load.Detail);
        Assert.True(load.Writable);
        Assert.Equal(["list-morning", "list-weekly", "list-empty"], load.Lists.Select(l => l.Id));
        Assert.Equal(["2026-09-28", "2026-09-29", "2026-09-30", "2026-10-01"], load.Lists[0].Habits[0].Log);
        Assert.Equal("Wöchentlich ✓", load.Lists[1].Name);
        Assert.Empty(load.Lists[2].Habits);
        Assert.Equal(File.ReadAllText(Fixture("habits.json")), HabitListStore.Serialize(load.Lists));
    }

    [Fact]
    public void S459_a_screen_time_history_loads_whole_without_a_repair_and_writes_back_byte_for_byte()
    {
        var (tracker, outcome) = ScreenTimeStore.Load(CopyOf("usage_stats.json"));

        Assert.Equal(ScreenTimeLoadOutcome.Loaded, outcome);
        Assert.Equal(["2026-09-29", "2026-09-30", "2026-10-01"], tracker.Days.Keys);
        Assert.Equal("Visual Studio Code", tracker.Names["code.exe"]);
        Assert.Equal(140, tracker.Switches["2026-09-29"]);
        Assert.Equal(["spotify.exe"], tracker.Preferences.Hidden);
        Assert.Equal(6 * 3600, tracker.Preferences.GoalSeconds);
        Assert.Equal(3600, tracker.Preferences.AppLimits["msedge.exe"]);
        Assert.Equal(File.ReadAllText(Fixture("usage_stats.json")), ScreenTimeStore.Serialize(tracker));
    }
}
