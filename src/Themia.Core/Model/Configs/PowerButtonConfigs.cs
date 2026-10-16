using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-1910] The closed set of things a power widget can do. Never persisted: each action is its own
/// widget kind, and <see cref="PowerActionRules.ForKind"/> maps the kind to its action.
/// </summary>
public enum PowerAction
{
    Shutdown = 0,
    Restart = 1,
    Sleep = 2,
    Hibernate = 3,
    Lock = 4,
    SignOut = 5,
    ScreenOff = 6,
}

/// <summary>
/// [S-1919] The looks every power widget shares: one enum for all seven kinds, named as the toggles'
/// looks of the same shape are (the round button is one part for both). The first is the default;
/// an unknown stored name is repaired to it ([S-6004]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<PowerButtonStyle>))]
public enum PowerButtonStyle
{
    /// <summary>[S-1911] The whole tile is the button: icon with the name under it.</summary>
    Tile = 0,

    /// <summary>[S-1919] A circular accent button holding the icon, the name under it.</summary>
    Round = 1,
}

/// <summary>
/// [S-1910] A one-action power widget: the whole tile is one button. Nothing to configure beyond the
/// card's own settings and its look, so the config carries which action its kind stands for and the
/// style every power widget shares ([S-1919]).
/// </summary>
public abstract class PowerButtonConfig : WidgetConfig, IStyled
{
    private static readonly StyleOption[] PowerStyles =
    [
        StyleOption.Of(PowerButtonStyle.Tile, Str.Styles_Toggles_Tile, 0xECA5),
        StyleOption.Of(PowerButtonStyle.Round, Str.Styles_Toggles_Round, 0xEA3B),
    ];

    /// <summary>The action this widget runs, decided by its kind.</summary>
    [JsonIgnore]
    public PowerAction Action => PowerActionRules.ForKind(Kind)
        ?? throw new InvalidOperationException($"{Kind} is not a power widget");

    /// <summary>[S-1919] How the button is drawn; copied by every kind's <c>Clone</c>.</summary>
    public PowerButtonStyle Style { get; set; } = PowerButtonStyle.Tile;

    IReadOnlyList<StyleOption> IStyled.Styles => PowerStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (PowerButtonStyle)value;
    }
}

/// <summary>[S-1910] Shut down.</summary>
public sealed class ShutdownConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Shutdown;

    public override WidgetConfig Clone() => new ShutdownConfig { Style = Style };
}

/// <summary>[S-1910] Restart.</summary>
public sealed class RestartConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Restart;

    public override WidgetConfig Clone() => new RestartConfig { Style = Style };
}

/// <summary>[S-1910] Sleep.</summary>
public sealed class SleepConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Sleep;

    public override WidgetConfig Clone() => new SleepConfig { Style = Style };
}

/// <summary>[S-1910] Hibernate.</summary>
public sealed class HibernateConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Hibernate;

    public override WidgetConfig Clone() => new HibernateConfig { Style = Style };
}

/// <summary>[S-1910] Lock.</summary>
public sealed class LockConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Lock;

    public override WidgetConfig Clone() => new LockConfig { Style = Style };
}

/// <summary>[S-1910] Sign out.</summary>
public sealed class SignOutConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.SignOut;

    public override WidgetConfig Clone() => new SignOutConfig { Style = Style };
}

/// <summary>[S-1910] Turn off the display.</summary>
public sealed class ScreenOffConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ScreenOff;

    public override WidgetConfig Clone() => new ScreenOffConfig { Style = Style };
}
