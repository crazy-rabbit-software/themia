using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-4602] A stopwatch widget: what it holds is the stopwatch itself, kept here so a running
/// stopwatch is still running after a restart (<see cref="StopwatchRecord"/>), and so each widget is a
/// stopwatch of its own; [S-11302] how many laps it lists, which is the card's height ([S-14410]).
/// </summary>
public sealed class StopwatchConfig : WidgetConfig
{
    /// <summary>[S-11302] The most lap rows it lists.</summary>
    public const int MostLapRows = 10;

    /// <summary>[S-11302] [S-14410] How many of the newest laps it lists under its controls, 0 to <see cref="MostLapRows"/>; a new one none; a resize sets it.</summary>
    public int LapRows { get; set; }

    public StopwatchPhase Phase { get; set; }

    /// <summary>What it counted before its current run, in milliseconds.</summary>
    public long BankedMs { get; set; }

    /// <summary>While running, when its current run started (UTC). Null otherwise.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Where each lap ended, from the start, in milliseconds, oldest first.</summary>
    public List<long> LapsMs { get; set; } = [];

    /// <summary>What it holds, as the rules read it.</summary>
    [JsonIgnore]
    public StopwatchRecord Record => new(Phase, BankedMs, StartedAt, LapsMs ?? []);

    /// <summary>Write <paramref name="record"/> back.</summary>
    public void Store(StopwatchRecord record)
    {
        Phase = record.Phase;
        BankedMs = record.BankedMs;
        StartedAt = record.StartedAt;
        LapsMs = [.. record.LapsMs];
    }

    /// <summary>
    /// A phase this build does not know, negative figures, too many laps or laps out of order (a hand
    /// edit) are put right, loudly; a running stopwatch with no start is paused where it was.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (LapRows is < 0 or > MostLapRows)
        {
            repairs.Add($"Stopwatch lap rows {LapRows} out of range");
            LapRows = Math.Clamp(LapRows, 0, MostLapRows);
        }

        if (!Enum.IsDefined(Phase))
        {
            repairs.Add("Stopwatch had an unknown phase; reset");
            Store(StopwatchRecord.Of(StopwatchState.None, DateTimeOffset.UtcNow, TimeSpan.Zero));
            return repairs;
        }

        // The rules already read a damaged record safely; writing back what they read keeps the file honest.
        var now = DateTimeOffset.UtcNow;
        var read = StopwatchRecord.Of(Record.ToState(now, TimeSpan.Zero), now, TimeSpan.Zero);
        var lapsChanged = !read.LapsMs.SequenceEqual(LapsMs ?? []);

        if (read.Phase != Phase || lapsChanged || (Phase != StopwatchPhase.Running && read.BankedMs != BankedMs) || (Phase == StopwatchPhase.Running && StartedAt is null))
        {
            repairs.Add("Stopwatch state was damaged and has been repaired");
            Phase = read.Phase;
            LapsMs = [.. read.LapsMs];
            if (read.Phase != StopwatchPhase.Running)
            {
                BankedMs = read.BankedMs;
                StartedAt = null;
            }
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new StopwatchConfig
    {
        Phase = Phase,
        BankedMs = BankedMs,
        StartedAt = StartedAt,
        LapsMs = [.. LapsMs ?? []],
        LapRows = LapRows,
    };
}
