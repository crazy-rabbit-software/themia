using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>What a kit widget's body shows.</summary>
public enum KitScreenKind
{
    /// <summary>The list (a problem may still be in the header).</summary>
    Items,

    /// <summary>The widget reads accounts and none offering what it needs is signed in: the card's Set up ([S-1862]); no sentence.</summary>
    NoAccounts,

    /// <summary>Nothing held yet and a first answer is out.</summary>
    Loading,

    /// <summary>The source answered with nothing: the spec's own sentence.</summary>
    Empty,

    /// <summary>A filter left nothing of what the source holds.</summary>
    NoMatch,

    /// <summary>The widget names accounts and none of them is signed in any more.</summary>
    AccountsGone,

    /// <summary>Every account it reads needs signing in again.</summary>
    SignInRequired,

    /// <summary>Signed in, and the service refused this data to every account it reads.</summary>
    Forbidden,

    /// <summary>Nothing held and the last attempts failed: one line per source and reason.</summary>
    Problem,
}

/// <summary>How an account a widget reads stands, as the account list (not a reply) says.</summary>
public enum KitAccountState
{
    Ok,
    SignInRequired,
    Forbidden,
}

/// <summary>A signed-in account offering what the widget needs.</summary>
public sealed record KitAccount(string Id, string Label, KitAccountState State = KitAccountState.Ok);

/// <summary>
/// [S-7040] How one source of a widget last went: a feed, an account's part of a list.
/// </summary>
/// <param name="Owner">Its name in a sentence ("World News", "Work"), or null for a widget of one source.</param>
/// <param name="Status">The last attempt; <see cref="FetchStatus.Loading"/> while the first is out.</param>
/// <param name="Detail">The source's own words for a failure ("HTTP 404 Not Found"), or null for the shared sentence.</param>
/// <param name="HeldAt">When the copy on screen was fetched, or null when nothing is held.</param>
/// <param name="RetryAt">When it will be asked again, when a wait is known.</param>
public sealed record KitOutcome(
    string? Owner,
    FetchStatus Status,
    string? Detail = null,
    DateTimeOffset? HeldAt = null,
    DateTimeOffset? RetryAt = null);

/// <summary>[S-7040] The decision: which screen, its sentence, and the problems a list carries in its header.</summary>
/// <param name="OffersAccounts">The state carries a way to Settings › Accounts.</param>
/// <param name="HeaderProblems">For a list: one line per source that failed ("World News: HTTP 404 Not Found").</param>
public sealed record KitScreen(KitScreenKind Kind, string Sentence, bool OffersAccounts, IReadOnlyList<string> HeaderProblems)
{
    /// <summary>
    /// [S-9919] Where the user widens what the account may read, offered beside the Accounts link on a
    /// refusal or a missing item of a service that grants per resource; null otherwise.
    /// </summary>
    public Uri? Grant { get; init; }

    /// <summary>The screen offers the grant page.</summary>
    public bool OffersGrant => Grant is not null;

    /// <summary>A problem the widget states itself before any source is asked (an address refused by its rules).</summary>
    public static KitScreen Refused(string sentence) => new(KitScreenKind.Problem, sentence, false, []);
}

/// <summary>
/// [S-7040] The one state machine of a kit widget: every kit widget's "no account", "account gone",
/// "sign in again", loading, empty, no match and failure has one look and one sentence, decided here.
/// Views never see an HTTP status or an account error.
/// <list type="number">
/// <item>A widget that reads accounts (<paramref name="accounts"/> not null): none at all is the card's
/// Set up; the ones it names all gone is "account gone"; every one it reads needing a sign-in, or
/// refused, says so; all with the Accounts link.</item>
/// <item>Items held: the list, with a line per failed source for the header.</item>
/// <item>A source not answered yet: loading.</item>
/// <item>Every source answered with nothing: the spec's empty sentence; with a filter on, its no-match one.</item>
/// <item>Otherwise every failed source's line, in the sources' order (an empty one among them too,
/// "Field notes: the feed has no items").</item>
/// </list>
/// </summary>
public static class KitScreens
{
    public static KitScreen Decide(
        Locale locale,
        IReadOnlyList<KitAccount>? accounts,
        IReadOnlyList<string> chosen,
        IReadOnlyList<KitOutcome> outcomes,
        int itemCount,
        string emptySentence,
        string? noMatchSentence = null,
        Uri? grantPage = null,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(emptySentence);

        if (accounts is not null && AccountScreen(locale, accounts, chosen) is { } accountScreen)
        {
            return accountScreen.Kind == KitScreenKind.Forbidden ? accountScreen with { Grant = grantPage } : accountScreen;
        }

        var problems = outcomes.Where(o => FetchState.IsFailure(o.Status)).Select(o => Line(locale, o, now)).ToList();

        if (itemCount > 0)
        {
            return new KitScreen(KitScreenKind.Items, string.Empty, false, problems);
        }

        if (outcomes.Count == 0 || outcomes.Any(o => o.Status == FetchStatus.Loading))
        {
            return new KitScreen(KitScreenKind.Loading, FetchState.Message(locale, FetchStatus.Loading), false, []);
        }

        if (outcomes.All(o => o.Status is FetchStatus.Empty or FetchStatus.Ok))
        {
            return noMatchSentence is not null && outcomes.Any(o => o.Status == FetchStatus.Ok)
                ? new KitScreen(KitScreenKind.NoMatch, noMatchSentence, false, [])
                : new KitScreen(KitScreenKind.Empty, emptySentence, false, []);
        }

        // [S-9919] Every source refused or missing: the user widens what the account may read there.
        var refused = outcomes.Count > 0 && outcomes.All(o => o.Status is FetchStatus.Forbidden or FetchStatus.NotFound);
        return new KitScreen(KitScreenKind.Problem, string.Join("\n", problems), refused && accounts is not null, []) { Grant = refused ? grantPage : null };
    }

    /// <summary>[S-7041] The accounts a widget reads: every one offering its need when it names none, else the ones it names that are still signed in.</summary>
    public static IReadOnlyList<KitAccount> Targets(IReadOnlyList<KitAccount> accounts, IReadOnlyList<string> chosen)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(chosen);

        if (chosen.Count == 0)
        {
            return accounts;
        }

        var wanted = new HashSet<string>(chosen, StringComparer.Ordinal);
        return [.. accounts.Where(a => wanted.Contains(a.Id))];
    }

    private static KitScreen? AccountScreen(Locale locale, IReadOnlyList<KitAccount> accounts, IReadOnlyList<string> chosen)
    {
        if (accounts.Count == 0)
        {
            return new KitScreen(KitScreenKind.NoAccounts, string.Empty, false, []);
        }

        var targets = Targets(accounts, chosen);
        if (targets.Count == 0)
        {
            return new KitScreen(KitScreenKind.AccountsGone, Strings.Get(locale, Str.Kit_AccountsGone), true, []);
        }

        if (targets.All(a => a.State == KitAccountState.SignInRequired))
        {
            var text = targets.Count == 1
                ? Strings.Format(locale, Str.Kit_SignInAgain, ("account", targets[0].Label))
                : Strings.Get(locale, Str.Kit_AllSignInAgain);
            return new KitScreen(KitScreenKind.SignInRequired, text, true, []);
        }

        if (targets.All(a => a.State != KitAccountState.Ok))
        {
            return new KitScreen(KitScreenKind.Forbidden, Strings.Get(locale, Str.Kit_Forbidden), true, []);
        }

        return null;
    }

    /// <summary>
    /// "World News: HTTP 404 Not Found": the source's own words when it has them, else the shared
    /// sentence; a throttle with a known end says when it asks again ([S-9901]).
    /// </summary>
    internal static string Line(Locale locale, KitOutcome outcome, DateTimeOffset? now = null)
    {
        var reason = string.IsNullOrWhiteSpace(outcome.Detail) ? FetchState.Message(locale, outcome.Status) : outcome.Detail!;
        if (outcome.Status == FetchStatus.RateLimited && outcome.RetryAt is { } retry && now is { } at && retry > at)
        {
            reason = Strings.Format(locale, Str.Kit_RetryAt, ("state", reason), ("time", KitFreshness.When(locale, retry, at)));
        }

        return outcome.Owner is { Length: > 0 } owner ? Strings.Format(locale, Str.Kit_OwnerProblem, ("name", owner), ("reason", reason)) : reason;
    }
}

/// <summary>
/// [S-7042] The one stale rule's words. While a held copy is on screen and the last attempt of some
/// source failed, the header's problem glyph says, one line per source and problem (repeats folded,
/// stamped with the oldest copy among them, as [S-2664]): "Work: No network · updated Mon 10:42". The
/// Refresh line says how old the list is ("Refresh · updated 4 min ago").
/// </summary>
public static class KitFreshness
{
    /// <summary>The problem glyph's tooltip lines; empty while every source's last attempt worked.</summary>
    public static IReadOnlyList<string> Lines(Locale locale, IReadOnlyList<KitOutcome> outcomes, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        var folded = new List<(string? Owner, FetchStatus Status, DateTimeOffset? At)>();
        foreach (var outcome in outcomes.Where(o => FetchState.IsFailure(o.Status) && o.Status != FetchStatus.Empty))
        {
            var index = folded.FindIndex(f => f.Owner == outcome.Owner && f.Status == outcome.Status);
            if (index < 0)
            {
                folded.Add((outcome.Owner, outcome.Status, outcome.HeldAt));
            }
            else if (outcome.HeldAt is { } at && (folded[index].At is not { } held || at < held))
            {
                folded[index] = folded[index] with { At = at };
            }
        }

        return [.. folded.Select(f =>
        {
            var state = FetchState.Message(locale, f.Status);
            var stamped = f.At is { } at ? Strings.Format(locale, Str.Sync_StateUpdated, ("state", state), ("time", When(locale, at, now))) : state;
            return f.Owner is { Length: > 0 } owner ? Strings.Format(locale, Str.Kit_OwnerProblem, ("name", owner), ("reason", stamped)) : stamped;
        })];
    }

    /// <summary>[S-7042] The Refresh line: with the age of the oldest copy on screen, or plain while nothing is held.</summary>
    public static string RefreshLabel(Locale locale, IReadOnlyList<KitOutcome> outcomes, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        DateTimeOffset? oldest = null;
        foreach (var outcome in outcomes)
        {
            if (outcome.HeldAt is { } at && (oldest is not { } held || at < held))
            {
                oldest = at;
            }
        }

        if (oldest is not { } stamp)
        {
            return Strings.Get(locale, Str.Common_Refresh);
        }

        var age = now - stamp;
        return Strings.Format(locale, Str.Kit_RefreshUpdated, ("age", LocaleFormat.Relative(locale, age > TimeSpan.Zero ? -age : TimeSpan.Zero, RelativeStyle.Short)));
    }

    /// <summary>A clock time today, the weekday and time within six days, else a short date ([S-2612]).</summary>
    public static string When(Locale locale, DateTimeOffset at, DateTimeOffset now)
    {
        var local = at.ToOffset(now.Offset);
        var today = DateOnly.FromDateTime(now.DateTime);
        var day = DateOnly.FromDateTime(local.DateTime);
        var time = LocaleFormat.Time(locale, local.DateTime);

        if (day >= today)
        {
            return time;
        }

        if (today.DayNumber - day.DayNumber < 7)
        {
            return $"{LocaleFormat.WeekdayName(locale, local.DayOfWeek, NameWidth.Short)} {time}";
        }

        return LocaleFormat.Date(locale, day, DateStyle.MonthDayShort);
    }
}
