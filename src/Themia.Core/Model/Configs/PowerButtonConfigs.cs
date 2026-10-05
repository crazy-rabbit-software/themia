using System.Text.Json.Serialization;
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
/// [S-1910] A one-action power widget: the whole tile is one button. Nothing to configure beyond the
/// card's own settings, so the config only carries which action its kind stands for.
/// </summary>
public abstract class PowerButtonConfig : WidgetConfig
{
    /// <summary>The action this widget runs, decided by its kind.</summary>
    [JsonIgnore]
    public PowerAction Action => PowerActionRules.ForKind(Kind)
        ?? throw new InvalidOperationException($"{Kind} is not a power widget");
}

/// <summary>[S-1910] Shut down.</summary>
public sealed class ShutdownConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Shutdown;

    public override WidgetConfig Clone() => new ShutdownConfig();
}

/// <summary>[S-1910] Restart.</summary>
public sealed class RestartConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Restart;

    public override WidgetConfig Clone() => new RestartConfig();
}

/// <summary>[S-1910] Sleep.</summary>
public sealed class SleepConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Sleep;

    public override WidgetConfig Clone() => new SleepConfig();
}

/// <summary>[S-1910] Hibernate.</summary>
public sealed class HibernateConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Hibernate;

    public override WidgetConfig Clone() => new HibernateConfig();
}

/// <summary>[S-1910] Lock.</summary>
public sealed class LockConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Lock;

    public override WidgetConfig Clone() => new LockConfig();
}

/// <summary>[S-1910] Sign out.</summary>
public sealed class SignOutConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.SignOut;

    public override WidgetConfig Clone() => new SignOutConfig();
}

/// <summary>[S-1910] Turn off the display.</summary>
public sealed class ScreenOffConfig : PowerButtonConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ScreenOff;

    public override WidgetConfig Clone() => new ScreenOffConfig();
}
