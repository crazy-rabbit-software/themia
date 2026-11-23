namespace Themia.Sdk.Devices;

/// <summary>
/// [S-16636] The one config shape of a device list (owner, 2026-11-23: "for all of those there should also be the list
/// version of it that basically shows the list of them ... they should still try to share the same structure in the
/// backen"): which devices of its kind are hidden, the user's order, the block gap, and how many items a new card stands.
/// A list of drives, USB sticks, network drives or Bluetooth devices holds it; its devices are found by their ids (a
/// drive's root, a device's endpoint id).
/// </summary>
public interface IDeviceListConfig
{
    /// <summary>[S-16651] The devices hidden by their ids: the checklist's off ones.</summary>
    List<string> Hidden { get; set; }

    /// <summary>[S-16652] The user's order by ids: these first, as listed, every other device after them in its kind's order.</summary>
    List<string> Order { get; set; }

    /// <summary>[S-16653] One grid cell between two devices, so each stands as a card of its own.</summary>
    bool Gap { get; set; }

    /// <summary>[S-16654] How many devices a new card stands (its natural height), 1..20.</summary>
    int Rows { get; set; }
}

/// <summary>
/// [S-16651] [S-16652] Which devices a list shows and in what order, whatever its kind (owner, 2026-11-23: "you should be
/// able to filter them out or rearrange to your liking"): every device not hidden; the ones the user put in order first,
/// as the order has them, every other after them in the order its kind gives (a drive by letter, a Bluetooth device
/// connected first). Ids compare ignoring case (a drive's root typed "e:\"). Pure.
/// </summary>
public static class DeviceOrder
{
    /// <summary>[S-16651] [S-16652] The devices shown, in the user's order then their own.</summary>
    public static IReadOnlyList<T> Arrange<T>(IEnumerable<T> devices, Func<T, string> id, IReadOnlyCollection<string> hidden, IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(hidden);
        return Ordered((devices ?? []).Where(d => !hidden.Contains(id(d), StringComparer.OrdinalIgnoreCase)), id, order);
    }

    /// <summary>[S-16652] <paramref name="devices"/> in the user's order: the ones it names as it names them, the rest as they came.</summary>
    public static IReadOnlyList<T> Ordered<T>(IEnumerable<T> devices, Func<T, string> id, IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(order);
        int Place(T device)
        {
            var key = id(device);
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
        return [.. (devices ?? []).OrderBy(Place)];
    }

    /// <summary>
    /// [S-16652] The order after the devices shown were put in <paramref name="shown"/>'s order: those, then every id the old
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
}
