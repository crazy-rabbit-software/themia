using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-1389] What <c>SHQueryRecycleBin</c> said: how many items, and how many bytes.</summary>
public readonly record struct RecycleBinState(long Items, long Bytes)
{
    public bool IsEmpty => Items <= 0;

    /// <summary>
    /// [S-4863] Whether Empty is offered: only with something in the bin (an unknown answer is not
    /// "something"), and not while an empty is already out. The widget's button and the Shortcuts
    /// item menu both ask this.
    /// </summary>
    public static bool CanEmpty(RecycleBinState? state, bool emptying) => state is { IsEmpty: false } && !emptying;

    /// <summary>The Recycle Bin's shell target: what the widget opens, and the one Shortcuts item that carries a live badge.</summary>
    public static readonly string Target = LaunchTargets.ShellTarget("RecycleBinFolder");

    /// <summary>[S-1389] The Recycle Bin's tooltip: "name — N items (size)", "name — empty", or the name when unknown.</summary>
    public static string Tooltip(string name, RecycleBinState? state) => Tooltip(Strings.Current, name, state);

    /// <inheritdoc cref="Tooltip(string, RecycleBinState?)"/>
    public static string Tooltip(Locale locale, string name, RecycleBinState? state)
    {
        if (state is not { } bin)
        {
            return name;
        }

        if (bin.IsEmpty)
        {
            return Strings.Format(locale, Str.WidgetsC_Shortcuts_Bin_Empty, ("name", name));
        }

        return Strings.Plural(locale, Str.WidgetsC_Shortcuts_Bin_Items, bin.Items,
            ("name", name), ("size", Formatting.Bytes(locale, bin.Bytes)));
    }
}
