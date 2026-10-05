#if DEBUG
namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1884] Debug only: which widget cards and views were ever built, weakly, and how many views are
/// attached right now. The switch check ([S-1883]) reads it after a forced collection: a card or
/// view of a view no display shows that is still alive is a leak, and an attached count above the
/// shown widgets is a view that was never detached. Nothing in the app reads it.
/// </summary>
internal static class LiveCensus
{
    private static readonly List<WeakReference<object>> Cards = [];
    private static readonly List<WeakReference<object>> Views = [];

    /// <summary>Views attached and not yet detached.</summary>
    public static int Attached { get; private set; }

    public static void CardBuilt(object card) => Cards.Add(new WeakReference<object>(card));

    public static void ViewAttached(object view)
    {
        Views.Add(new WeakReference<object>(view));
        Attached++;
    }

    public static void ViewDetached() => Attached--;

    /// <summary>Cards still reachable. Collect first, or this counts garbage.</summary>
    public static int LiveCards() => Alive(Cards).Count;

    /// <summary>Cards still reachable, by widget kind, most first. Collect first.</summary>
    public static IReadOnlyList<(string Type, int Count)> LiveCardKinds()
        => [.. Alive(Cards)
            .GroupBy(c => (c as WidgetCard)?.Model.Kind.ToString() ?? c.GetType().Name, StringComparer.Ordinal)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(p => p.Item2)
            .ThenBy(p => p.Key, StringComparer.Ordinal)];

    /// <summary>Views still reachable, by type name, most first. Collect first.</summary>
    public static IReadOnlyList<(string Type, int Count)> LiveViews()
        => [.. Alive(Views)
            .GroupBy(v => v.GetType().Name, StringComparer.Ordinal)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(p => p.Item2)
            .ThenBy(p => p.Key, StringComparer.Ordinal)];

    private static List<object> Alive(List<WeakReference<object>> list)
    {
        list.RemoveAll(w => !w.TryGetTarget(out _));
        var alive = new List<object>(list.Count);
        foreach (var weak in list)
        {
            if (weak.TryGetTarget(out var target))
            {
                alive.Add(target);
            }
        }

        return alive;
    }
}
#endif
