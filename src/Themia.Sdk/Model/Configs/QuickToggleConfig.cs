using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-3900] [S-16700] The closed set of quick settings the quick setting widget switches: its config's
/// <see cref="QuickToggleConfig.Target"/>, written by its camelCase name (<c>wifi</c>, <c>airplaneMode</c>); a name this
/// build does not know reads as none and load validation repairs it to the first.
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<QuickToggle>))]
public enum QuickToggle
{
    Wifi = 0,
    Bluetooth = 1,
    AirplaneMode = 2,
    EnergySaver = 3,
    NightLight = 4,
    RotationLock = 5,
    DoNotDisturb = 6,
    Sound = 7,
    Microphone = 8,
    SpatialSound = 9,
    MonoAudio = 10,
    ColorFilters = 11,
    GameMode = 12,
}

/// <summary>
/// [S-3916] The looks every quick setting shares: one enum for all thirteen targets. The first
/// is the default; an unknown stored name is repaired to it ([S-6004]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<QuickToggleStyle>))]
public enum QuickToggleStyle
{
    /// <summary>[S-3904] The Windows 11 quick-settings tile: the whole tile accent-filled while on.</summary>
    Tile = 0,

    /// <summary>[S-3917] A circular button holding the icon, accent-filled while on, the name under it.</summary>
    Round = 1,

    /// <summary>[S-3918] The name with a Windows toggle switch and its state line, as a Settings row.</summary>
    Switch = 2,
}

/// <summary>
/// [S-3900] [S-16700] The quick setting widget (owner, 2026-11-23: "you should be able to select what they toggle, this
/// means that they should actually be merged into one widget and not be multiple ones"): the whole tile is one switch, of
/// the setting it targets (<see cref="Target"/>, its "Toggles" line), drawn in the style every target shares ([S-3916]).
/// </summary>
public sealed class QuickToggleConfig : WidgetConfig, IStyled
{
    /// <summary>[S-11307] The look as the Tile block draws it.</summary>
    [JsonIgnore]
    public TileLook Look => Style switch
    {
        QuickToggleStyle.Round => TileLook.Round,
        QuickToggleStyle.Switch => TileLook.Switch,
        _ => TileLook.Tile,
    };

    private static readonly StyleOption[] ToggleStyles =
    [
        StyleOption.Of(QuickToggleStyle.Tile, Str.Styles_Toggles_Tile.Local(), 0xECA5),
        StyleOption.Of(QuickToggleStyle.Round, Str.Styles_Toggles_Round.Local(), 0xEA3B),
        StyleOption.Of(QuickToggleStyle.Switch, Str.Styles_Toggles_Switch.Local(), 0xEC11),
    ];

    /// <summary>[S-16700] The setting this widget switches: its Toggles line; Wi-Fi at first.</summary>
    public QuickToggle Target { get; set; } = QuickToggle.Wifi;

    /// <summary>[S-3916] How the toggle is drawn; copied by <see cref="Clone"/>.</summary>
    public QuickToggleStyle Style { get; set; } = QuickToggleStyle.Tile;

    IReadOnlyList<StyleOption> IStyled.Styles => ToggleStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (QuickToggleStyle)value;
    }

    public override WidgetConfig Clone() => new QuickToggleConfig { Target = Target, Style = Style };

    /// <summary>[S-16700] A target this build does not know (a hand edit) is the first, said.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(Target))
        {
            return [];
        }

        Target = QuickToggle.Wifi;
        return ["its target was not a quick setting of this build; it is Wi-Fi"];
    }
}
