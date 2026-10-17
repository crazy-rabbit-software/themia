using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

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

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("shutdown"),
        ConfigType = typeof(ShutdownConfig),
        CreateDefault = () => new ShutdownConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["power", "shutdown", "power off", "turn off", "switch off"],
        Label = Str.Power_Action_Shutdown.Local(),
        Description = Str.WidgetsC_Power_Desc_Shutdown.Local(),
        Glyph = Glyphs.Power,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 0),
    };

    public override WidgetConfig Clone() => new ShutdownConfig { Style = Style };
}

/// <summary>[S-1910] Restart.</summary>
public sealed class RestartConfig : PowerButtonConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("restart"),
        ConfigType = typeof(RestartConfig),
        CreateDefault = () => new RestartConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["power", "reboot", "restart", "reset"],
        Label = Str.Power_Action_Restart.Local(),
        Description = Str.WidgetsC_Power_Desc_Restart.Local(),
        Glyph = Glyphs.Reset,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 1),
    };

    public override WidgetConfig Clone() => new RestartConfig { Style = Style };
}

/// <summary>[S-1910] Sleep.</summary>
public sealed class SleepConfig : PowerButtonConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("sleep"),
        ConfigType = typeof(SleepConfig),
        CreateDefault = () => new SleepConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["power", "standby", "suspend"],
        Label = Str.Power_Action_Sleep.Local(),
        Description = Str.WidgetsC_Power_Desc_Sleep.Local(),
        Glyph = Glyphs.Moon,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 2),
    };

    public override WidgetConfig Clone() => new SleepConfig { Style = Style };
}

/// <summary>[S-1910] Hibernate.</summary>
public sealed class HibernateConfig : PowerButtonConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("hibernate"),
        ConfigType = typeof(HibernateConfig),
        CreateDefault = () => new HibernateConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["power", "suspend to disk", "deep sleep"],
        Label = Str.Power_Action_Hibernate.Local(),
        Description = Str.WidgetsC_Power_Desc_Hibernate.Local(),
        Glyph = Glyphs.Hibernate,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 3),
    };

    public override WidgetConfig Clone() => new HibernateConfig { Style = Style };
}

/// <summary>[S-1910] Lock.</summary>
public sealed class LockConfig : PowerButtonConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("lock"),
        ConfigType = typeof(LockConfig),
        CreateDefault = () => new LockConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["power", "lock screen", "away", "security"],
        Label = Str.Power_Action_Lock.Local(),
        Description = Str.WidgetsC_Power_Desc_Lock.Local(),
        Glyph = Glyphs.Lock,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 4),
    };

    public override WidgetConfig Clone() => new LockConfig { Style = Style };
}

/// <summary>[S-1910] Sign out.</summary>
public sealed class SignOutConfig : PowerButtonConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("signOut"),
        ConfigType = typeof(SignOutConfig),
        CreateDefault = () => new SignOutConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["power", "log off", "log out", "logout", "sign off"],
        Label = Str.Power_Action_SignOut.Local(),
        Description = Str.WidgetsC_Power_Desc_SignOut.Local(),
        Glyph = Glyphs.SignOut,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 5),
    };

    public override WidgetConfig Clone() => new SignOutConfig { Style = Style };
}

/// <summary>[S-1910] Turn off the display.</summary>
public sealed class ScreenOffConfig : PowerButtonConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenOff"),
        ConfigType = typeof(ScreenOffConfig),
        CreateDefault = () => new ScreenOffConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["power", "monitor off", "screen off", "blank screen", "display"],
        Label = Str.Power_Action_ScreenOff.Local(),
        Description = Str.WidgetsC_Power_Desc_ScreenOff.Local(),
        Glyph = Glyphs.Display,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 6),
    };

    public override WidgetConfig Clone() => new ScreenOffConfig { Style = Style };
}
