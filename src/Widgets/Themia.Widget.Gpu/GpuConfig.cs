using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.App.Widgets.Meters;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Gpu;

namespace Themia.Core.Model;

/// <summary>[S-6320] The graphics reading's drawings: the adapter's load, two ways ([S-6000]).</summary>
[JsonConverter(typeof(StyleJsonConverter<GpuStyle>))]
public enum GpuStyle
{
    /// <summary>[S-430] The load alone, at 28.</summary>
    Number = 0,

    /// <summary>[S-6314] The load inside a half-circle gauge.</summary>
    Gauge = 1,
}

/// <summary>
/// [S-430] The graphics reading: one adapter's load of the busiest engine, alone. Its history is the
/// GPU history atom's, its engines the GPU engines atom's. Two adapters are two widgets ([S-433]).
/// </summary>
public sealed class GpuConfig : ReadingConfig, IStyled, IReadsSystem, IGpuAdapter
{
    /// <summary>
    /// [S-433] The adapter, as its position in DXGI's list (software adapters left out). DXGI lists
    /// the adapter driving the primary display first, so the default 0 is that one, else the first.
    /// </summary>
    public int Adapter { get; set; }

    /// <summary>[S-6320] The reading's drawing.</summary>
    public GpuStyle Style { get; set; } = GpuStyle.Number;

    private static readonly StyleOption[] GpuStyles =
    [
        StyleOption.Of(GpuStyle.Number, GpuStr.Styles_Gpu_Number.Local(), 0xE8EF),
        StyleOption.Of(GpuStyle.Gauge, GpuStr.Styles_Gpu_Gauge.Local(), 0xEC4A),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => GpuStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (GpuStyle)value;
    }

    /// <summary>[S-2513] Every adapter's sample, and its adapter's series (the history's and the engines' too).</summary>
    public SystemInterests SystemWants(string systemRoot) => GpuInterests.Of(Adapter);

    public override WidgetConfig Clone() => new GpuConfig { Adapter = Adapter, Style = Style };

    /// <summary>[S-433] A position that is none (negative, hand-edited) is the first adapter, loudly.</summary>
    public override IReadOnlyList<string> Repair() => GpuInterests.Repair(Kind, Adapter, a => Adapter = a);
}
