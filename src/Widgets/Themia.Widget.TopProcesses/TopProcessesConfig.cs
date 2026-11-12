using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>[S-2410] The busiest processes, by processor or by memory: a list of a set number of rows.</summary>
public sealed class TopProcessesConfig : DetailConfig, IReadsSystem, IKitRows
{
    /// <summary>[S-2412] A new list's rows.</summary>
    public const int DefaultRows = 5;

    /// <summary>[S-2411] The ranking.</summary>
    public ProcessSort SortBy { get; set; } = ProcessSort.Cpu;

    /// <summary>[S-2412] How many rows the list shows (1..20); more processes scroll inside them.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-2412] Every row has two lines: the program, and the other figure under it.</summary>
    [JsonIgnore]
    public int Lines
    {
        get => 2;
        set
        {
            // ignore: a process row always has its two lines; the menu offers no choice.
        }
    }

    /// <summary>[S-2413] Every process of one executable is one row (Chrome's twenty are one Chrome).</summary>
    public bool GroupByName { get; set; } = true;

    /// <summary>[S-2513] The process list.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Processes);

    public override WidgetConfig Clone() => new TopProcessesConfig
    {
        SortBy = SortBy,
        Rows = Rows,
        GroupByName = GroupByName,
    };

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        if (!Enum.IsDefined(SortBy))
        {
            notes.Add($"{Kind}: sort {(int)SortBy} is unknown; ranking by processor");
            SortBy = ProcessSort.Cpu;
        }

        var rows = KitSizing.ClampRows(Rows);
        if (rows != Rows)
        {
            notes.Add($"{Kind}: {Rows} rows is outside {KitSizing.MinRows}..{KitSizing.MaxRows}; using {rows}");
            Rows = rows;
        }

        return notes;
    }
}
