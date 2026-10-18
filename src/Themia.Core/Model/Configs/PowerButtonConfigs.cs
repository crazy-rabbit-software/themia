using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-1910] Sleep.</summary>
public sealed class SleepConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Sleep;

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
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Hibernate;

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
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Lock;

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
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.SignOut;

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
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.ScreenOff;

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
