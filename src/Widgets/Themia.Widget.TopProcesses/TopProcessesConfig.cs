using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2410] The busiest processes, by processor or by memory.</summary>
public sealed class TopProcessesConfig : DetailConfig, IReadsSystem
{
    /// <summary>The most rows the list ever holds; a ceiling, not a target ([S-2412]).</summary>
    public const int MaxRowsLimit = 50;

    /// <summary>[S-2411] The ranking.</summary>
    public ProcessSort SortBy { get; set; } = ProcessSort.Cpu;

    /// <summary>[S-2412] At most this many rows; 0 lists as many as the tile has whole rows for.</summary>
    public int MaxRows { get; set; }

    /// <summary>[S-2413] Every process of one executable is one row (Chrome's twenty are one Chrome).</summary>
    public bool GroupByName { get; set; } = true;

    /// <summary>[S-2513] The process list.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Processes);

    public override WidgetConfig Clone()
    {
        var copy = new TopProcessesConfig
        {
            SortBy = SortBy,
            MaxRows = MaxRows,
            GroupByName = GroupByName,
        };

        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        if (!Enum.IsDefined(SortBy))
        {
            notes.Add($"{Kind}: sort {(int)SortBy} is unknown; ranking by processor");
            SortBy = ProcessSort.Cpu;
        }

        if (MaxRows is < 0 or > MaxRowsLimit)
        {
            var repaired = Math.Clamp(MaxRows, 0, MaxRowsLimit);
            notes.Add($"{Kind}: {MaxRows} rows is outside 0..{MaxRowsLimit}; using {repaired}");
            MaxRows = repaired;
        }

        return notes;
    }
}
