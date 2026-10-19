namespace Themia.Core.Widgets;

/// <summary>
/// [S-6070] The options of a kind's properties menu that only some styles or states use, as the
/// kind's own enum: the lines that change nothing the widget visibly does or shows right now are not
/// built at all. Every such rule is one Core property of the config, never a check written in a
/// menu. An option every style and state uses is not listed: it is always offered.
/// </summary>
public interface IShownSettings<TSetting>
    where TSetting : struct, Enum
{
    /// <summary>The conditional options that change what the widget shows in its current style and state, in menu order.</summary>
    IReadOnlyList<TSetting> ShownSettings { get; }
}

/// <summary>[S-6070] Reading the rule from a menu.</summary>
public static class SettingRules
{
    /// <summary>Whether the menu offers <paramref name="setting"/> for the config as it is now.</summary>
    public static bool Offers<TSetting>(this IShownSettings<TSetting> config, TSetting setting)
        where TSetting : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.ShownSettings.Contains(setting);
    }
}
