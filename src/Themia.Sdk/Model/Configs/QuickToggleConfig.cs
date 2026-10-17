using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-3900] The closed set of quick settings a toggle widget switches. Never persisted: each toggle is
/// its own widget kind, and <see cref="QuickToggleRules.ForKind"/> maps the kind to its toggle.
/// </summary>
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
/// [S-3916] The looks every quick-setting toggle shares: one enum for all thirteen kinds. The first
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
/// [S-3900] A quick-setting toggle widget: the whole tile is one switch. Nothing to configure beyond
/// the card's own settings and its look, so the config carries which toggle its kind stands for and
/// the style every toggle shares ([S-3916]).
/// </summary>
public abstract class QuickToggleConfig : WidgetConfig, IStyled
{
    private static readonly StyleOption[] ToggleStyles =
    [
        StyleOption.Of(QuickToggleStyle.Tile, Str.Styles_Toggles_Tile.Local(), 0xECA5),
        StyleOption.Of(QuickToggleStyle.Round, Str.Styles_Toggles_Round.Local(), 0xEA3B),
        StyleOption.Of(QuickToggleStyle.Switch, Str.Styles_Toggles_Switch.Local(), 0xEC11),
    ];

    /// <summary>The toggle this widget switches: each kind states its own.</summary>
    [JsonIgnore]
    public abstract QuickToggle Toggle { get; }

    /// <summary>[S-3916] How the toggle is drawn; copied by every kind's <c>Clone</c>.</summary>
    public QuickToggleStyle Style { get; set; } = QuickToggleStyle.Tile;

    IReadOnlyList<StyleOption> IStyled.Styles => ToggleStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (QuickToggleStyle)value;
    }
}
