using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Battery;

namespace Themia.Core.Model;

/// <summary>[S-6272] The battery reading's drawings: the charge alone, a battery, or a ring.</summary>
[JsonConverter(typeof(StyleJsonConverter<BatteryStyle>))]
public enum BatteryStyle
{
    /// <summary>[S-1187] The charge led by the battery glyph, at 28.</summary>
    Number = 0,

    /// <summary>[S-6272] A large battery filled to the charge, the bolt while charging.</summary>
    Battery = 1,

    /// <summary>[S-6272] A ring filled to the charge, the figure inside.</summary>
    Ring = 2,
}

/// <summary>[S-1180] Charge level: the reading alone. Its history is the Battery history atom's.</summary>
public sealed class BatteryConfig : ReadingConfig, IStyled, IReadsSystem
{
    private static readonly StyleOption[] BatteryStyles =
    [
        StyleOption.Of(BatteryStyle.Number, BatteryStr.Styles_Battery_Number.Local(), 0xE8EF),
        StyleOption.Of(BatteryStyle.Battery, BatteryStr.Styles_Io_Battery.Local(), 0xE83F),
        StyleOption.Of(BatteryStyle.Ring, Str.Styles_Io_Ring.Local(), 0xEA3A),
    ];

    /// <summary>[S-6272] The meter's look; what was its history switch ([S-6000]).</summary>
    public BatteryStyle Style { get; set; } = BatteryStyle.Number;

    IReadOnlyList<StyleOption> IStyled.Styles => BatteryStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (BatteryStyle)value;
    }

    /// <summary>[S-2513] The charge and its history.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Battery);

    /// <summary>[S-6573] Only the ring's track is an item; the graph and the battery picture are the card's own drawing.</summary>
    protected override bool StyleDrawsItems => Style == BatteryStyle.Ring;

    public override WidgetConfig Clone() => new BatteryConfig { Style = Style };
}
