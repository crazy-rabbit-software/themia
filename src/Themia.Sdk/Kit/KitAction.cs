using Themia.Core.Shell;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7010] What an item action does: a closed set. There is no kind for a view, sort, column or
/// "show" switch, so a kit menu cannot change how a widget looks ([S-1746]); and none for a write
/// to a service: a change the user wants made is an <see cref="OpenTo"/> to the service's own page.
/// </summary>
public enum KitActionKind
{
    /// <summary>The item's link; the primary action (double-click, Enter).</summary>
    Open,

    /// <summary>"&lt;verb&gt; on &lt;Service&gt;": a web page where the user does what Themia will not.</summary>
    OpenTo,

    /// <summary>The link to the clipboard.</summary>
    CopyLink,

    /// <summary>A named text to the clipboard (a number, an address).</summary>
    CopyText,

    /// <summary>A local state the widget owns flipped (a task's tick); never a service write.</summary>
    Toggle,

    /// <summary>F2; only for a widget with items of its own.</summary>
    Rename,

    /// <summary>Delete; only for items of its own or a change it queued.</summary>
    Delete,

    /// <summary>The widget's sources now, past their freshness and back-off.</summary>
    Refresh,
}

/// <summary>
/// [S-7010] One action on one item, as the widget's spec gives it for that item.
/// </summary>
/// <param name="Label">What the line says, in the current language ("Open", "Merge on GitHub").</param>
/// <param name="Icon">The line's glyph; <see cref="KitIcon.Default"/> is the kind's own.</param>
/// <param name="Target">Open and OpenTo: the address; without one the line is shown disabled.</param>
/// <param name="Text">CopyLink and CopyText: what goes on the clipboard; without it the line is shown disabled.</param>
/// <param name="Checked">Toggle: the state the check shows.</param>
/// <param name="Id">Toggle, Rename, Delete: which of the view's handlers runs.</param>
public sealed record KitAction(
    KitActionKind Kind,
    string Label,
    KitIcon Icon = KitIcon.Default,
    Uri? Target = null,
    string? Text = null,
    bool Disabled = false,
    bool Checked = false,
    string? Id = null)
{
    /// <summary>Whether the line can run: it is not disabled and has what its kind needs.</summary>
    public bool Runnable => !Disabled && Kind switch
    {
        KitActionKind.Open or KitActionKind.OpenTo => Target is not null,
        KitActionKind.CopyLink or KitActionKind.CopyText => !string.IsNullOrEmpty(Text),
        _ => true,
    };
}

/// <summary>One line of a kit item's menu: an action, or a separator between groups.</summary>
/// <param name="Quick">[S-1740] The icon-row command the line is, when it is one.</param>
/// <param name="Shortcut">[S-1753] The key that does the same under the lease ([S-7020]), shown beside the label.</param>
public sealed record KitMenuLine(KitAction? Action, QuickCommand? Quick = null, KeyChord? Shortcut = null)
{
    public static KitMenuLine Separator { get; } = new(Action: null);

    public bool IsSeparator => Action is null;
}

/// <summary>
/// [S-7011] An item's actions as its menu: one order for every kit widget. The item's own group
/// (Open, a check, the "&lt;verb&gt; on &lt;Service&gt;" lines in the spec's order, the copies), then
/// Rename, then Delete, then Refresh, each group after a separator (an empty group, and with it its
/// separator, is left out). The first copy is the icon row's Copy, Rename and Delete are theirs
/// ([S-1740]; the row shows only with two or more, [S-1741]). A line whose key the list takes says
/// it ([S-1753]): Enter on Open, Ctrl+C on the first copy, F2, Delete. Nothing else builds a kit item menu.
/// </summary>
public static class KitMenu
{
    private static readonly KitActionKind[][] Groups =
    [
        [KitActionKind.Open, KitActionKind.Toggle, KitActionKind.OpenTo, KitActionKind.CopyLink, KitActionKind.CopyText],
        [KitActionKind.Rename],
        [KitActionKind.Delete],
        [KitActionKind.Refresh],
    ];

    public static IReadOnlyList<KitMenuLine> Lines(IReadOnlyList<KitAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var lines = new List<KitMenuLine>();
        var copied = false;

        foreach (var group in Groups)
        {
            var members = group
                .SelectMany(kind => actions.Where(a => a.Kind == kind && IsFirstOfItsKind(actions, a)))
                .ToList();

            if (members.Count == 0)
            {
                continue;
            }

            if (lines.Count > 0)
            {
                lines.Add(KitMenuLine.Separator);
            }

            foreach (var action in members)
            {
                QuickCommand? quick = action.Kind switch
                {
                    KitActionKind.CopyLink or KitActionKind.CopyText when !copied => QuickCommand.Copy,
                    KitActionKind.Rename => QuickCommand.Rename,
                    KitActionKind.Delete => QuickCommand.Delete,
                    _ => null,
                };

                KeyChord? key = action.Kind switch
                {
                    KitActionKind.Open => KeyChord.Plain(ChordKey.Enter),
                    KitActionKind.CopyLink or KitActionKind.CopyText when !copied => KeyChord.Ctrl(ChordKey.C),
                    KitActionKind.Rename => KeyChord.Plain(ChordKey.F2),
                    KitActionKind.Delete => KeyChord.Plain(ChordKey.Delete),
                    _ => null,
                };

                copied |= action.Kind is KitActionKind.CopyLink or KitActionKind.CopyText;
                lines.Add(new KitMenuLine(action with { Disabled = !action.Runnable }, quick, key));
            }
        }

        return lines;
    }

    /// <summary>
    /// [S-7011] An item has one Open, one check, one Rename, one Delete and one Refresh; only the web
    /// pages and the copies may be several. A repeat of a single kind is left out.
    /// </summary>
    private static bool IsFirstOfItsKind(IReadOnlyList<KitAction> actions, KitAction action)
        => action.Kind is KitActionKind.OpenTo or KitActionKind.CopyLink or KitActionKind.CopyText
            || ReferenceEquals(actions.First(a => a.Kind == action.Kind), action);

    /// <summary>[S-7011] The action Enter and a double-click run: the item's Open, when it can run.</summary>
    public static KitAction? Primary(IReadOnlyList<KitAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return actions.FirstOrDefault(a => a.Kind == KitActionKind.Open) is { Runnable: true } open ? open : null;
    }
}
