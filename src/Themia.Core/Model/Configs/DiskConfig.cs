using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-1822] Whose throughput the disk meter shows.</summary>
public enum DiskScope
{
    /// <summary>One drive: <see cref="DiskConfig.Drive"/> (empty = the system drive).</summary>
    OneDrive = 0,

    /// <summary>Every drive together.</summary>
    AllDrives = 1,
}

/// <summary>[S-6271] The disk meter's looks: the rate over its history, or reads and writes as two bars.</summary>
[JsonConverter(typeof(StyleJsonConverter<DiskStyle>))]
public enum DiskStyle
{
    /// <summary>[S-1820] Reads and writes together, over their history.</summary>
    Graph = 0,

    /// <summary>[S-6271] Reads and writes apart, each its rate and a bar against the history's top.</summary>
    Bars = 1,
}

/// <summary>[S-1820] Disk throughput (reads and writes) of one drive or every drive, with its history.</summary>
public sealed class DiskConfig : GaugeConfig, IStyled
{
    private static readonly StyleOption[] DiskStyles =
    [
        StyleOption.Of(DiskStyle.Graph, Str.Styles_Io_Graph, 0xE9D2),
        StyleOption.Of(DiskStyle.Bars, Str.Styles_Io_Bars, 0xE8E4),
    ];

    /// <summary>
    /// Five minutes of history. The history is on, as it was in the original.
    /// </summary>
    public DiskConfig()
        : base(graphSpanSeconds: 300)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Disk;

    /// <summary>[S-6271] The meter's look; what was its history switch ([S-6000]).</summary>
    public DiskStyle Style { get; set; } = DiskStyle.Graph;

    IReadOnlyList<StyleOption> IStyled.Styles => DiskStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (DiskStyle)value;
    }

    /// <summary>[S-6271] Only the graph style draws the history.</summary>
    [JsonIgnore]
    public override bool DrawsHistory => Style == DiskStyle.Graph;

    /// <summary>[S-1822] One drive (<see cref="Drive"/>) or every drive together.</summary>
    public DiskScope Scope { get; set; } = DiskScope.OneDrive;

    /// <summary>[S-1160] The drive to report on, as a root (<c>C:\</c>). Empty means the system drive.</summary>
    public string Drive { get; set; } = string.Empty;

    public override WidgetConfig Clone()
    {
        var copy = new DiskConfig
        {
            Style = Style,
            Scope = Scope,
            Drive = Drive,
        };
        CopyTo(copy);
        return copy;
    }

    /// <summary>[S-1160] [S-1822] Keep the drive as a root; one that is not a drive letter, or an unknown scope, is repaired loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        Drive ??= string.Empty;

        if (Drive.Length > 0)
        {
            if (DiskRules.Root(Drive) is { } root)
            {
                Drive = root;
            }
            else
            {
                notes.Add($"{Kind}: '{Drive}' is not a drive letter; reporting on the system drive");
                Drive = string.Empty;
            }
        }

        if (!Enum.IsDefined(Scope))
        {
            notes.Add($"{Kind}: scope {(int)Scope} is unknown; reporting on one drive");
            Scope = DiskScope.OneDrive;
        }

        return notes;
    }
}
