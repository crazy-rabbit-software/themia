using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-4602] A stopwatch widget. It has no options; what it holds is the stopwatch itself, kept here
/// so a running stopwatch is still running after a restart (<see cref="StopwatchRecord"/>).
/// </summary>
public sealed class StopwatchConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Stopwatch;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("stopwatch"),
        Kind = WidgetKind.Stopwatch,
        ConfigType = typeof(StopwatchConfig),
        CreateDefault = () => new StopwatchConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["timer", "lap", "laps", "split", "chronometer", "elapsed", "count up"],
        LabelId = Str.Stopwatch_Label,
        DescriptionId = Str.Stopwatch_Desc,
        Glyph = Glyphs.Stopwatch,
        DefaultWidth = 260,
        DefaultHeight = 200,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Time, 0, 3),
    };

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
    };
}
