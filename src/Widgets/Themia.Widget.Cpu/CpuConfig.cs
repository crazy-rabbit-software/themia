using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Cpu;

namespace Themia.Core.Model;

/// <summary>[S-6311] The processor reading's drawings: the number, or the number in a gauge ([S-6000]).</summary>
[JsonConverter(typeof(StyleJsonConverter<CpuStyle>))]
public enum CpuStyle
{
    /// <summary>[S-1140] The load alone, at 28.</summary>
    Number = 0,

    /// <summary>[S-6314] The load inside a half-circle gauge.</summary>
    Gauge = 1,
}

/// <summary>[S-1140] Processor load: the reading alone. Its history is the CPU history atom's, its cores the CPU cores atom's.</summary>
public sealed class CpuConfig : ReadingConfig, IStyled, IReadsSystem
{
    /// <summary>[S-6311] The reading's drawing.</summary>
    public CpuStyle Style { get; set; } = CpuStyle.Number;

    private static readonly StyleOption[] CpuStyles =
    [
        StyleOption.Of(CpuStyle.Number, CpuStr.Styles_Cpu_Number.Local(), 0xE8EF),
        StyleOption.Of(CpuStyle.Gauge, CpuStr.Styles_Cpu_Gauge.Local(), 0xEC4A),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => CpuStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (CpuStyle)value;
    }

    /// <summary>[S-2513] The load, the one series the CPU history and the CPU cores read too.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Cpu);

    public override WidgetConfig Clone() => new CpuConfig { Style = Style };
}
