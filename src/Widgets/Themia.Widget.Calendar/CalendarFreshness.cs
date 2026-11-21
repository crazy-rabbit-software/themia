using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-3651] The calendar's one stale rule ([S-7042]): each account whose last sync could not
/// improve on the events held is a kit outcome — named by its label, in the kit's failure
/// vocabulary, stamped with when its events are from — so the problem glyph says it as
/// every kit widget does and the Refresh line carries the age.
/// </summary>
public static class CalendarFreshness
{
    public static IReadOnlyList<KitOutcome> Outcomes(
        IReadOnlyList<(string Account, FetchStatus Status, DateTimeOffset? At)> problems,
        IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(problems);
        ArgumentNullException.ThrowIfNull(labels);

        return [.. problems.Select(p => new KitOutcome(labels.TryGetValue(p.Account, out var label) ? label : p.Account, p.Status, HeldAt: p.At))];
    }
}
