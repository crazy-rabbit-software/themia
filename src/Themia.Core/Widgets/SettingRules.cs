using System.Globalization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6070] A config whose settings page offers some rows only in some styles or states: the rows
/// that change nothing the widget visibly does or shows right now are not built at all, so they are
/// neither on the page nor found by its search. Every such rule is one Core property of the config
/// (<see cref="IShownSettings{TSetting}.ShownSettings"/>), never a check written in a page.
/// </summary>
public interface IShownSettings
{
    /// <summary>The rows offered now as one comparable string; part of the page's shape ([S-6071]).</summary>
    string ShownSettingsKey { get; }
}

/// <summary>
/// [S-6070] The rows of a kind's page that only some styles or states use, as the kind's own enum.
/// A row every style and state uses is not listed: it is always offered.
/// </summary>
public interface IShownSettings<TSetting> : IShownSettings
    where TSetting : struct, Enum
{
    /// <summary>The conditional rows that change what the widget shows in its current style and state, in page order.</summary>
    IReadOnlyList<TSetting> ShownSettings { get; }

    string IShownSettings.ShownSettingsKey => string.Join(",", ShownSettings);
}

/// <summary>[S-6070] Reading the rule from a page, and the page shape built on it.</summary>
public static class SettingRules
{
    /// <summary>Whether the page offers <paramref name="setting"/> for the config as it is now.</summary>
    public static bool Offers<TSetting>(this IShownSettings<TSetting> config, TSetting setting)
        where TSetting : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.ShownSettings.Contains(setting);
    }

    /// <summary>
    /// [S-6071] A widget page's shape: its style (picked on the properties panel, [S-6536]) and
    /// which conditional rows are offered. A change to any of it (a style picked, a parent switch turned on)
    /// builds the page again; any other edit leaves the page, and the control under the hand, alone.
    /// </summary>
    public static string Shape(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var style = WidgetStyles.Current(config) is { } value ? value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        var rows = config is IShownSettings rule ? rule.ShownSettingsKey : string.Empty;
        return style + "|" + rows;
    }
}
