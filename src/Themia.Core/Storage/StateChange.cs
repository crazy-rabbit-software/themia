namespace Themia.Core.Storage;

/// <summary>
/// [S-15660] What changes of the live state changed, as the surfaces need to know it to draw them (<see cref="StateWriter.TakeChange"/>):
/// the widgets whose config or own look changed (or that are new), the widgets whose rect moved, whether the app-wide
/// design or the preferences moved, and whether the whole state is to be drawn again (an undo step, an import taken back,
/// another view shown on a display). A surface re-applies a card's config only where <see cref="Reapplies"/> says so, its
/// design where <see cref="Design"/> does; a moved card is laid where it now stands, as every card always is.
/// </summary>
/// <param name="Configs">The widgets (by id) whose config (its content, or another object), own look or presence changed.</param>
/// <param name="Moved">The widgets (by id) whose rect changed.</param>
/// <param name="Design">The app-wide design moved: every card is painted again.</param>
/// <param name="Preferences">The preferences moved: every card's config is applied again (formats, language, icon size).</param>
/// <param name="Whole">Everything is to be drawn again: the full pass.</param>
/// <param name="Arranged">
/// [S-15664] The arrangement may have changed (a widget moved, added, removed or given another look or config object, a
/// fence's items, a view's name, icon, colour, order or widgets' order): what the undo history has to look at. False for
/// a change of content, design or preferences alone, which records no step and takes no snapshot.
/// </param>
public sealed record StateChange(
    IReadOnlySet<string> Configs,
    IReadOnlySet<string> Moved,
    bool Design,
    bool Preferences,
    bool Whole,
    bool Arranged)
{
    private static readonly IReadOnlySet<string> NoIds = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Nothing changed.</summary>
    public static StateChange None { get; } = new(NoIds, NoIds, Design: false, Preferences: false, Whole: false, Arranged: false);

    /// <summary>Everything is to be drawn again (and looked at by the history).</summary>
    public static StateChange All { get; } = new(NoIds, NoIds, Design: true, Preferences: true, Whole: true, Arranged: true);

    /// <summary>Whether nothing changed.</summary>
    public bool IsNone => !Whole && !Design && !Preferences && !Arranged && Configs.Count == 0 && Moved.Count == 0;

    /// <summary>[S-15660] Whether the card of <paramref name="widgetId"/> applies its config again.</summary>
    public bool Reapplies(string widgetId) => Whole || Preferences || Configs.Contains(widgetId);

    /// <summary>The two changes as one: what changed in either.</summary>
    public StateChange With(StateChange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.IsNone)
        {
            return this;
        }

        if (IsNone)
        {
            return other;
        }

        return new(
            Union(Configs, other.Configs),
            Union(Moved, other.Moved),
            Design || other.Design,
            Preferences || other.Preferences,
            Whole || other.Whole,
            Arranged || other.Arranged);
    }

    private static IReadOnlySet<string> Union(IReadOnlySet<string> a, IReadOnlySet<string> b)
    {
        if (b.Count == 0)
        {
            return a;
        }

        if (a.Count == 0)
        {
            return b;
        }

        var all = new HashSet<string>(a, StringComparer.Ordinal);
        all.UnionWith(b);
        return all;
    }
}
