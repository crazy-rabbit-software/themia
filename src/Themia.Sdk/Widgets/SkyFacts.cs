using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11170] A fact atom of the weather, sky and money family (weather facts, air facts, sun times,
/// moon times): an ordered pick of fact rows, which and in what order its config's own, one 28 row
/// each. Its height is the family's content top (36), its rows and the bottom inset (12), stepped by a
/// row and following the pick's count; dragging the bottom edge keeps the first rows or adds the
/// next ones not yet picked, in the kind's own order (<see cref="Resize{T}"/>). The width is a range
/// in which a row's name and value both fit. Pure, so every fact atom shares it.
/// </summary>
public static class SkyFacts
{
    /// <summary>A fact atom's narrowest: a short name and a value side by side.</summary>
    public const double MinWidth = 200;

    /// <summary>A new fact atom's width.</summary>
    public const double NaturalWidth = 240;

    /// <summary>Past it a row is a name at one end and a value far off at the other.</summary>
    public const double MaxWidth = 420;

    /// <summary>[S-11170] The height's steps: the content top and the bottom inset as the base, a row as the step, the pick's count as the count.</summary>
    public static SizeSteps Steps(int count, int most)
        => new(FamilySpace.ContentTop + FamilySpace.Inset, FamilyShape.Row1, 1, Math.Max(1, most), Math.Clamp(count, 1, Math.Max(1, most)));

    /// <summary>[S-11170] The sizing of a fact atom showing <paramref name="count"/> of its <paramref name="most"/> rows.</summary>
    public static WidgetSizing Sizing(int count, int most, double minWidth = MinWidth, double naturalWidth = NaturalWidth, double maxWidth = MaxWidth)
        => WidgetSizing.Range(new(minWidth, 1), new(naturalWidth, 1), new(maxWidth, 1)).WithHeightSteps(Steps(count, most));

    /// <summary>
    /// [S-11170] A stored pick made whole: each fact once, only facts the kind has, in the order stored;
    /// an empty or missing pick is the kind's default. Returns the repaired list and whether it changed.
    /// </summary>
    public static (List<T> Facts, bool Changed) Repair<T>(IReadOnlyList<T>? stored, IReadOnlyList<T> all, IReadOnlyList<T> defaults)
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentNullException.ThrowIfNull(defaults);
        var kept = (stored ?? []).Where(all.Contains).Distinct().ToList();
        if (kept.Count == 0)
        {
            return ([.. defaults], true);
        }

        return (kept, stored is null || kept.Count != stored.Count);
    }

    /// <summary>
    /// [S-11170] The pick <paramref name="count"/> rows tall: its first rows, or all of it and then the
    /// facts not picked yet in the kind's order (<paramref name="all"/>), never more than the kind has.
    /// </summary>
    public static List<T> Resize<T>(IReadOnlyList<T> picked, int count, IReadOnlyList<T> all)
    {
        ArgumentNullException.ThrowIfNull(picked);
        ArgumentNullException.ThrowIfNull(all);
        var wanted = Math.Clamp(count, 1, Math.Max(1, all.Count));
        var next = picked.Take(wanted).ToList();
        foreach (var fact in all)
        {
            if (next.Count >= wanted)
            {
                break;
            }

            if (!next.Contains(fact))
            {
                next.Add(fact);
            }
        }

        return next;
    }

    /// <summary>
    /// [S-2442] [S-11170] The config a fact atom of <paramref name="size"/> means: the pick resized to
    /// the rows that height holds (<paramref name="with"/> sets it on a copy). The same config when the
    /// count is the same; never the given one changed.
    /// </summary>
    public static WidgetConfig SizeSets<T>(WidgetConfig config, SizeDip size, IReadOnlyList<T> picked, IReadOnlyList<T> all, Action<WidgetConfig, List<T>> with)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(with);
        var wanted = Steps(picked.Count, all.Count).Nearest(size.Height);
        if (wanted == picked.Count)
        {
            return config;
        }

        var copy = config.Clone();
        with(copy, Resize(picked, wanted, all));
        return copy;
    }
}
