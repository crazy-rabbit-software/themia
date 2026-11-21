using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1910] Shut down.</summary>
public sealed class ShutdownConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Shutdown;

    public override WidgetConfig Clone() => CopyTo(new ShutdownConfig());
}

/// <summary>[S-1910] Restart.</summary>
public sealed class RestartConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Restart;

    public override WidgetConfig Clone() => CopyTo(new RestartConfig());
}

/// <summary>[S-1910] Sleep.</summary>
public sealed class SleepConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Sleep;

    public override WidgetConfig Clone() => CopyTo(new SleepConfig());
}

/// <summary>[S-1910] Hibernate.</summary>
public sealed class HibernateConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Hibernate;

    public override WidgetConfig Clone() => CopyTo(new HibernateConfig());
}

/// <summary>[S-1910] Lock.</summary>
public sealed class LockConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.Lock;

    public override WidgetConfig Clone() => CopyTo(new LockConfig());
}

/// <summary>[S-1910] Sign out.</summary>
public sealed class SignOutConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.SignOut;

    public override WidgetConfig Clone() => CopyTo(new SignOutConfig());
}

/// <summary>[S-1910] Turn off the display.</summary>
public sealed class ScreenOffConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.ScreenOff;

    public override WidgetConfig Clone() => CopyTo(new ScreenOffConfig());
}
