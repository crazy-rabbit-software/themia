using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Disk;

namespace Themia.Core.Model;

/// <summary>[S-6271] The disk reading's drawings: the rate alone, or reads and writes as two bars.</summary>
[JsonConverter(typeof(StyleJsonConverter<DiskStyle>))]
public enum DiskStyle
{
    /// <summary>[S-1820] Reads and writes together, the rate alone at 28.</summary>
    Number = 0,

    /// <summary>[S-6271] The rate over reads and writes apart, each its rate and a bar against the last minute's top.</summary>
    Bars = 1,
}

/// <summary>[S-1820] Disk throughput (reads and writes) of one drive or every drive: the reading alone. Its history is the Disk history atom's.</summary>
public sealed class DiskConfig : ReadingConfig, IStyled, IReadsSystem, IDiskScope
{
    private static readonly StyleOption[] DiskStyles =
    [
        StyleOption.Of(DiskStyle.Number, DiskStr.Styles_Disk_Number.Local(), 0xE8EF),
        StyleOption.Of(DiskStyle.Bars, DiskStr.Styles_Io_Bars.Local(), 0xE8E4),
    ];

    /// <summary>[S-6271] The reading's drawing.</summary>
    public DiskStyle Style { get; set; } = DiskStyle.Number;

    IReadOnlyList<StyleOption> IStyled.Styles => DiskStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (DiskStyle)value;
    }

    /// <summary>[S-1822] One drive (<see cref="Drive"/>) or every drive together.</summary>
    public DiskScope Scope { get; set; } = DiskScope.OneDrive;

    /// <summary>[S-1160] The drive to report on, as a root (<c>C:\</c>). Empty means the system drive.</summary>
    public string Drive { get; set; } = string.Empty;

    /// <summary>[S-2513] The disk activity of its series (<see cref="DiskScopes.Instance"/>).</summary>
    public SystemInterests SystemWants(string systemRoot) => DiskScopes.Wants(this, systemRoot);

    public override WidgetConfig Clone() => new DiskConfig { Style = Style, Scope = Scope, Drive = Drive };

    /// <summary>[S-1160] [S-1822] Keep the drive as a root; one that is not a drive letter, or an unknown scope, is repaired loudly.</summary>
    public override IReadOnlyList<string> Repair() => DiskScopes.Repair(this, Kind.ToString());
}
