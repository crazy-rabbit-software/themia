namespace Themia.Sdk.Items;

/// <summary>
/// [S-16651] [S-16652] [S-16950] Which items a list shows and in what order, whatever its kind (owner, 2026-11-23: "you should be
/// able to filter them out or rearrange to your liking"; "order and which items are shown should be combined"): every item
/// not hidden; the ones the user put in order first, as the order has them, every other after them in the order its kind
/// gives (a drive by letter, a Bluetooth device connected first). Ids compare ignoring case (a drive's root typed "e:\").
/// The same for an item's parts: the ones shown, in the user's order (a table's columns). Pure.
/// </summary>
public static class ItemOrder
{
    /// <summary>[S-16651] [S-16652] The items shown, in the user's order then their own.</summary>
    public static IReadOnlyList<T> Arrange<T>(IEnumerable<T> items, Func<T, string> id, IReadOnlyCollection<string> hidden, IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(hidden);
        return Ordered((items ?? []).Where(d => !hidden.Contains(id(d), StringComparer.OrdinalIgnoreCase)), id, order);
    }

    /// <summary>[S-16652] <paramref name="items"/> in the user's order: the ones it names as it names them, the rest as they came.</summary>
    public static IReadOnlyList<T> Ordered<T>(IEnumerable<T> items, Func<T, string> id, IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(order);
        int Place(T item)
        {
            var key = id(item);
            for (var i = 0; i < order.Count; i++)
            {
                if (string.Equals(order[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        // OrderBy is stable: the rest keep the order their kind gave them.
        return [.. (items ?? []).OrderBy(Place)];
    }

    /// <summary>
    /// [S-16652] The order after the items shown were put in <paramref name="shown"/>'s order: those, then every id the old
    /// <paramref name="order"/> held that is not shown now (a stick not plugged in keeps its place for when it is back).
    /// </summary>
    public static List<string> Reordered(IReadOnlyList<string> order, IReadOnlyList<string> shown)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(shown);
        var next = shown.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        next.AddRange(order.Where(id => !next.Contains(id, StringComparer.OrdinalIgnoreCase)));
        return next;
    }

    /// <summary>
    /// [S-16950] The parts of <paramref name="offered"/> (the kind's, in its own order) in the user's <paramref name="order"/>: the
    /// ones it names first as it names them, the rest as the kind has them.
    /// </summary>
    public static IReadOnlyList<ItemPart> Parts(IReadOnlyList<ItemPart> offered, IReadOnlyList<ItemPart> order)
    {
        ArgumentNullException.ThrowIfNull(offered);
        ArgumentNullException.ThrowIfNull(order);
        var places = order.ToList();
        return [.. offered.OrderBy(p => places.IndexOf(p) is var at and >= 0 ? at : int.MaxValue)];
    }

    /// <summary>
    /// [S-16950] [S-16945] The parts shown, in the user's order: a table's columns. Never none: with every part left out, the title.
    /// </summary>
    public static IReadOnlyList<ItemPart> Shown(IReadOnlyList<ItemPart> offered, IItemParts config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var shown = Parts(offered, config.PartOrder).Where(p => !config.HiddenParts.Contains(p)).ToList();
        return shown.Count > 0 ? shown : [ItemPart.Title];
    }

    /// <summary>[S-16673] A gap held to 0..<see cref="ItemLayout.MostGap"/> blocks; a note where it was not.</summary>
    public static int Repaired(int gap, string kind, List<string> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        var held = Math.Clamp(gap, 0, ItemLayout.MostGap);
        if (held != gap)
        {
            notes.Add($"{kind}: a gap of {gap} blocks is outside 0..{ItemLayout.MostGap}; using {held}");
        }

        return held;
    }

    /// <summary>[S-16672] [S-16950] Parts once each, every one a part: a hand-edited list repaired, a note per one dropped.</summary>
    public static List<ItemPart> Repaired(List<ItemPart> parts, string kind, List<string> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        var kept = new List<ItemPart>();
        foreach (var part in parts ?? [])
        {
            if (Enum.IsDefined(part) && !kept.Contains(part))
            {
                kept.Add(part);
            }
            else
            {
                notes.Add($"{kind}: part '{part}' is unknown or there twice; dropped");
            }
        }

        return kept;
    }

    /// <summary>[S-16655] Ids once each, none empty: a hand-edited list repaired, a note per id dropped.</summary>
    public static List<string> Repaired(List<string> ids, string kind, string what, List<string> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        var kept = new List<string>((ids ?? []).Count);
        foreach (var id in ids ?? [])
        {
            if (!string.IsNullOrWhiteSpace(id) && !kept.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                kept.Add(id);
            }
            else
            {
                notes.Add($"{kind}: {what} '{id}' is empty or there twice; dropped");
            }
        }

        return kept;
    }

    /// <summary>[S-16951] An item list's shared keys repaired: its gap, its parts and their order, its rows; one note each.</summary>
    public static List<string> Repair(IItemList config, string kind)
    {
        ArgumentNullException.ThrowIfNull(config);
        var notes = new List<string>();
        config.Gap = Repaired(config.Gap, kind, notes);
        config.HiddenParts = Repaired(config.HiddenParts, kind, notes);
        config.PartOrder = Repaired(config.PartOrder, kind, notes);
        var rows = Kit.KitSizing.ClampRows(config.Rows);
        if (rows != config.Rows)
        {
            notes.Add($"{kind}: {config.Rows} rows is outside {Kit.KitSizing.MinRows}..{Kit.KitSizing.MaxRows}; using {rows}");
            config.Rows = rows;
        }

        return notes;
    }
}
