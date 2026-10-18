using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;

namespace Themia.Core.Sync;

/// <summary>[S-2612] How a widget says the data on screen is a stored copy.</summary>
public static class SyncFreshness
{
    /// <summary>"Updated 10:42" today, "Updated Mon 10:42" this week, "Updated Oct 4" before.</summary>
    public static string Updated(Locale locale, DateTimeOffset at, DateTimeOffset now)
        => Strings.Format(locale, Str.Sync_Updated, ("time", When(locale, at, now)));

    /// <summary>
    /// The header's line for a stored copy the last sync could not improve on: "No network · updated
    /// 10:42". Null while the last sync worked (nothing to flag) or when nothing was ever stored.
    /// </summary>
    public static string? Line(Locale locale, FetchStatus status, DateTimeOffset? at, DateTimeOffset now)
    {
        if (at is not { } stamp || !FetchState.IsFailure(status) || status == FetchStatus.Empty)
        {
            return null;
        }

        return Strings.Format(locale, Str.Sync_StateUpdated, ("state", FetchState.Message(locale, status)), ("time", When(locale, stamp, now)));
    }

    /// <summary>
    /// A problem sentence over a stored copy, with when that copy is from: "No network · updated
    /// 10:42". The sentence alone when nothing is stored.
    /// </summary>
    public static string Stamp(Locale locale, string problem, DateTimeOffset? at, DateTimeOffset now)
        => at is { } stamp ? Strings.Format(locale, Str.Sync_StateUpdated, ("state", problem), ("time", When(locale, stamp, now))) : problem;

    /// <summary>
    /// [S-2664] One line per account and problem out of a problem per synced item (a calendar each),
    /// first seen first, stamped with the oldest stored copy among them: the header says how old the
    /// worst of what is on screen is. Items with nothing stored add no stamp; when none of them has
    /// one the line has none.
    /// </summary>
    public static IReadOnlyList<(string Owner, SyncProblem Problem, DateTimeOffset? At)> Fold(
        IEnumerable<(string Owner, SyncProblem Problem, DateTimeOffset? At)> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        var folded = new List<(string Owner, SyncProblem Problem, DateTimeOffset? At)>();
        foreach (var (owner, problem, at) in problems)
        {
            var index = folded.FindIndex(p => p.Owner == owner && p.Problem == problem);
            if (index < 0)
            {
                folded.Add((owner, problem, at));
            }
            else if (at is { } stamp && (folded[index].At is not { } held || stamp < held))
            {
                folded[index] = (owner, problem, stamp);
            }
        }

        return folded;
    }

    /// <summary>The time part alone: a clock time today, weekday and time within six days, else a short date.</summary>
    public static string When(Locale locale, DateTimeOffset at, DateTimeOffset now) => Themia.Sdk.Kit.KitFreshness.When(locale, at, now);
}
