using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Widget.DesktopItem;

/// <summary>[S-9504] What a target turned out to be when it was looked at (never when it was opened).</summary>
public enum TargetHealth
{
    /// <summary>Not looked at: a web address, a shell location, a Settings page, a network path, or the answer was unclear.</summary>
    Unknown = 0,

    File = 1,

    Folder = 2,

    /// <summary>A local path that is not there. Drawn faded and struck through.</summary>
    Missing = 3,
}

/// <summary>
/// An own icon's rules that are not drawing: its name, what it is on its view's links, the Recycle Bin
/// and the broken-target check. Pure, so each is tested. Its commands (open, the menu, Delete, the
/// clipboard, a drag) are the desktop's own, for both sources (<c>DesktopCommands</c>, [S-5580]).
/// </summary>
public static class OwnIconRules
{
    /// <summary>
    /// [S-9502] The name under the icon: the user's label, else the target's own (a site's host without
    /// <c>www.</c>, a Windows location's or a Settings page's name, a file's name without its extension),
    /// else "App layout" for a layout nobody named; the kind's own name while it opens nothing yet.
    /// </summary>
    public static string Name(DesktopItemConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Label.Trim().Length > 0 ? config.Label.Trim() : DerivedName(config);
    }

    /// <summary>[S-9502] The name a shortcut has without a label of the user's.</summary>
    public static string DerivedName(DesktopItemConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.IsLayout ? LayoutKindLabel()
            : string.IsNullOrWhiteSpace(config.Target) ? DesktopItemStr.Shortcut_Label.Text()
            : LaunchTargets.DisplayName(null, config.Target);
    }

    /// <summary>[S-9507] A typed name; the one the target gives (or nothing) clears the label, so the shortcut follows its target again. True when it changed.</summary>
    public static bool Rename(DesktopItemConfig config, string? typed)
    {
        ArgumentNullException.ThrowIfNull(config);
        var label = (typed ?? string.Empty).Trim();
        if (label == DerivedName(config))
        {
            label = string.Empty;
        }

        if (label == config.Label)
        {
            return false;
        }

        config.Label = label;
        return true;
    }

    /// <summary>
    /// [S-11934] The app a shortcut is on its view's links: a local executable (<c>….exe</c>) is that app,
    /// keyed as screen time keys it (<c>LinkKeys.App</c>); a folder, a document, a web address, a shell
    /// location, a Settings page, an app layout or a refused target is none.
    /// </summary>
    public static string? AppKey(LaunchTarget target)
        => target.Kind == LaunchKind.LocalPath && target.Value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? LinkKeys.App(target.Value)
            : null;

    /// <summary>
    /// [S-11949] The file a shortcut is on its view's links: a local path that is no executable (a folder, a
    /// document, a <c>.lnk</c> or <c>.url</c> by its own path: the widget never reads a link's target), keyed
    /// as every file widget keys it (<c>LinkKeys.LocalFileLink</c>); an executable is its app
    /// (<see cref="AppKey"/>), anything else none.
    /// </summary>
    public static FileLink? FileKey(LaunchTarget target)
        => target.Kind == LaunchKind.LocalPath && AppKey(target) is null ? LinkKeys.LocalFileLink(target.Value) : null;

    /// <summary>[S-9510] Whether a shortcut is the Recycle Bin.</summary>
    public static bool IsRecycleBin(string? target) => LaunchTargets.SameTarget(target, RecycleBinState.Target);

    /// <summary>
    /// [S-9504] Look at a target without opening it: only a probeable local path is asked about;
    /// everything else is <see cref="TargetHealth.Unknown"/> and is never drawn broken.
    /// </summary>
    public static TargetHealth Check(LaunchTarget target, Func<string, TargetHealth> probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        if (target.Kind != LaunchKind.LocalPath || !LaunchTargets.MayProbe(target))
        {
            return TargetHealth.Unknown;
        }

        return probe(target.Value);
    }

    /// <summary>[S-1405] What an app layout is called, which is also the name of one nobody named.</summary>
    public static string LayoutKindLabel() => LayoutKindLabel(Strings.Current);

    /// <inheritdoc cref="LayoutKindLabel()"/>
    public static string LayoutKindLabel(Locale locale) => Strings.Get(locale, Str.Shortcut_ChipLayout);

    /// <summary>[S-9509] What kind of thing the shortcut points at, the value on its Target line.</summary>
    public static string KindLabel(LaunchTarget target, TargetHealth health) => KindLabel(Strings.Current, target, health);

    /// <inheritdoc cref="KindLabel(LaunchTarget, TargetHealth)"/>
    public static string KindLabel(Locale locale, LaunchTarget target, TargetHealth health) => target.Kind switch
    {
        LaunchKind.Web => Strings.Get(locale, Str.WidgetsC_Shortcuts_Kind_Web),
        LaunchKind.ShellNamespace => DesktopItemStr.WidgetsC_Shortcuts_Kind_Location.Text(locale),
        LaunchKind.Settings => DesktopItemStr.Shortcut_Kind_Settings.Text(locale),
        LaunchKind.LocalPath => health switch
        {
            TargetHealth.Folder => Strings.Get(locale, Str.Shortcut_KindFolder),
            TargetHealth.File => Strings.Get(locale, Str.Shortcut_ChipFile),
            TargetHealth.Missing => DesktopItemStr.WidgetsC_Shortcuts_Kind_Missing.Text(locale),
            _ => DesktopItemStr.WidgetsC_Shortcuts_Kind_Path.Text(locale),
        },
        _ => DesktopItemStr.WidgetsC_Shortcuts_Kind_Refused.Text(locale),
    };
}
