using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-6320] The graphics meter's looks: the adapter's load, three ways ([S-6000]).</summary>
[JsonConverter(typeof(StyleJsonConverter<GpuStyle>))]
public enum GpuStyle
{
    /// <summary>[S-430] The load over its history.</summary>
    Graph = 0,

    /// <summary>[S-6322] The load over a bar per engine type (3D, copy, video encode, video decode).</summary>
    Engines = 1,

    /// <summary>[S-6314] The load inside a half-circle gauge.</summary>
    Gauge = 2,
}

/// <summary>
/// [S-430] The graphics meter: one adapter's load of the busiest engine and its history ([S-1762]).
/// A gauge, so it carries the history setting the other meters do, and it draws the same sparkline.
/// Two adapters are two widgets ([S-433]).
/// </summary>
public sealed class GpuConfig : GaugeConfig, IStyled
{
    /// <summary>[S-436] A minute of history.</summary>
    public GpuConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Gpu;

    /// <summary>
    /// [S-433] The adapter, as its position in DXGI's list (software adapters left out). DXGI lists
    /// the adapter driving the primary display first, so the default 0 is that one, else the first.
    /// </summary>
    public int Adapter { get; set; }

    /// <summary>[S-6320] The meter's look.</summary>
    public GpuStyle Style { get; set; } = GpuStyle.Graph;

    private static readonly StyleOption[] GpuStyles =
    [
        StyleOption.Of(GpuStyle.Graph, Str.Styles_Gpu_Graph, 0xE9D2),
        StyleOption.Of(GpuStyle.Engines, Str.Styles_Gpu_Engines, 0xE8BC),
        StyleOption.Of(GpuStyle.Gauge, Str.Styles_Gpu_Gauge, 0xEC4A),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => GpuStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (GpuStyle)value;
    }

    public override WidgetConfig Clone()
    {
        var copy = new GpuConfig { Adapter = Adapter, Style = Style };
        CopyTo(copy);
        return copy;
    }

    /// <summary>[S-433] A position that is none (negative, hand-edited) is the first adapter, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        if (Adapter < 0)
        {
            notes.Add($"{Kind}: adapter {Adapter} is not a position; using the first");
            Adapter = 0;
        }

        return notes;
    }
}
