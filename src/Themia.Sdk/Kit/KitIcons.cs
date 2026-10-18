using Themia.App.Shell;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7012] The one map from the kit's icon names to Segoe Fluent glyphs: an item's lead and an
/// action's line. Every <see cref="KitIcon"/> member has its glyph here (a test fails on one
/// without), and an action left at <see cref="KitIcon.Default"/> takes its kind's.
/// </summary>
public static class KitIcons
{
    public static string Glyph(KitIcon icon) => icon switch
    {
        KitIcon.Open => Glyphs.OpenExternal,
        KitIcon.Web => Glyphs.Globe,
        KitIcon.Link => Glyphs.Link,
        KitIcon.Copy => Glyphs.Copy,
        KitIcon.Refresh => Glyphs.Refresh,
        KitIcon.Rename => Glyphs.Rename,
        KitIcon.Delete => Glyphs.Delete,
        KitIcon.Check => Glyphs.Done,
        KitIcon.Feed => Glyphs.Rss,
        KitIcon.Mail => Glyphs.Mail,
        KitIcon.Task => Glyphs.Task,
        KitIcon.Event => Glyphs.Calendar,
        KitIcon.Warning => Glyphs.Warning,
        KitIcon.Default => Glyphs.Info,
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, null),
    };

    /// <summary>The glyph of an action's line: its own icon, else its kind's.</summary>
    public static string Glyph(KitAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return action.Icon != KitIcon.Default ? Glyph(action.Icon) : Glyph(action.Kind switch
        {
            KitActionKind.Open => KitIcon.Open,
            KitActionKind.OpenTo => KitIcon.Web,
            KitActionKind.CopyLink => KitIcon.Link,
            KitActionKind.CopyText => KitIcon.Copy,
            KitActionKind.Toggle => KitIcon.Check,
            KitActionKind.Rename => KitIcon.Rename,
            KitActionKind.Delete => KitIcon.Delete,
            KitActionKind.Refresh => KitIcon.Refresh,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, null),
        });
    }
}
