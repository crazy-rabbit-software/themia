using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Developer;

/// <summary>An account as a developer widget sees it: no secrets, and why it cannot be used, if it cannot.</summary>
public sealed record DevAccount(string Id, string Label, string? Problem);

/// <summary>How one account's last attempt for a widget ended.</summary>
/// <param name="Detail">A sentence the widget may show for this failure, or null to use the generic one.</param>
/// <param name="RetryAt">When the service may be asked again, for a rate limit.</param>
public sealed record DevOutcome(string AccountId, FetchStatus Status, string? Detail = null, DateTimeOffset? RetryAt = null);

/// <summary>[S-628] What a developer widget shows instead of, or as well as, its content.</summary>
public enum DevScreenKind
{
    /// <summary>No account of the right kind is signed in to Themia.</summary>
    NoAccounts,

    /// <summary>The accounts this widget was set to read are no longer signed in.</summary>
    AccountsGone,

    /// <summary>Every account it reads needs the user to sign in again.</summary>
    SignInRequired,

    /// <summary>The widget itself needs a setting (a repository) before it can ask anything.</summary>
    NeedsSetup,

    Loading,

    /// <summary>Every account failed and there is nothing to show.</summary>
    Failed,

    /// <summary>Every account answered and there was nothing in it.</summary>
    Empty,

    Content,
}

/// <param name="Text">The sentence in place of the content; empty for <see cref="DevScreenKind.Content"/>.</param>
/// <param name="OfferAccounts">Whether to show the way to the Accounts page beside it.</param>
/// <param name="Warning">[S-638] A problem to flag while content is still shown, or null.</param>
public sealed record DevScreen(DevScreenKind Kind, string Text, bool OfferAccounts, string? Warning);

/// <summary>
/// [S-623][S-628] Which accounts a developer widget reads, and what it says when that goes wrong.
/// <para>
/// Every state is a sentence of its own, because they need different things from the reader:
/// "sign in to GitHub" sends them to the Accounts page, "GitHub asked Themia to slow down" says
/// when it will be back, "no network" is not the widget's fault at all. A widget that has content
/// keeps showing it through a failure and flags the failure instead ([S-638]).
/// </para>
/// </summary>
public static class DevScreens
{
    /// <summary>[S-746] The id requests without an account go under: its own rate-limit gate and cache.</summary>
    public const string AnonymousId = "github:anonymous";

    /// <summary>
    /// [S-746] The stand-in "account" a widget reads public data with when no account is signed in
    /// and it was not set to a particular one. It carries no token: nothing is sent but the request.
    /// </summary>
    public static DevAccount Anonymous { get; } = new(AnonymousId, "Public access", null);

    /// <summary>
    /// [S-623] The accounts a widget reads: the chosen ones still signed in, or — when none were
    /// chosen — every account of the kind (only the first when the widget reads one). Order is the
    /// order they were added, so "the first" is stable.
    /// </summary>
    /// <param name="anonymous">[S-746] With no account signed in and none chosen, read without one.</param>
    public static IReadOnlyList<DevAccount> Targets(IReadOnlyList<DevAccount> accounts, IReadOnlyList<string> chosen, bool single, bool anonymous = false)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(chosen);

        if (anonymous && accounts.Count == 0 && chosen.Count == 0)
        {
            return [Anonymous];
        }

        IReadOnlyList<DevAccount> picked;
        if (chosen.Count == 0)
        {
            picked = accounts;
        }
        else
        {
            var wanted = new HashSet<string>(chosen, StringComparer.Ordinal);
            picked = [.. accounts.Where(a => wanted.Contains(a.Id))];
        }

        return single && picked.Count > 1 ? [picked[0]] : picked;
    }

    /// <summary>The screen for a widget's current state.</summary>
    /// <param name="service">"GitHub" or "Claude", as the sentences name it.</param>
    /// <param name="accounts">Every account of the widget's kind.</param>
    /// <param name="chosen">The ids the widget's settings name; empty for "all".</param>
    /// <param name="single">Whether the widget reads one account (the repository, Claude usage).</param>
    /// <param name="outcomes">How each account's last attempt ended; an account with none is still loading.</param>
    /// <param name="hasContent">Whether there is anything to draw, from this attempt or an earlier one.</param>
    /// <param name="emptyText">What "nothing here" means for this widget: "No issues assigned to you".</param>
    /// <param name="anonymous">[S-746] The widget can read public data with no account at all.</param>
    /// <param name="savedAt">[S-747] When the content on screen was fetched, if it came from the saved copy and nothing newer has arrived yet.</param>
    /// <param name="locale">[S-1106] The language the sentences are written in.</param>
    public static DevScreen Decide(
        string service,
        IReadOnlyList<DevAccount> accounts,
        IReadOnlyList<string> chosen,
        bool single,
        IReadOnlyList<DevOutcome> outcomes,
        bool hasContent,
        string emptyText,
        DateTimeOffset now,
        Locale locale,
        bool anonymous = false,
        DateTimeOffset? savedAt = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(outcomes);

        if (anonymous && accounts.Count == 0 && chosen.Count == 0)
        {
            accounts = [Anonymous];
        }

        if (accounts.Count == 0)
        {
            // [S-1862] No account at all is the card's Set up button, which says nothing here. A
            // widget that reads anonymously gets here only when the account it was given has gone.
            return anonymous
                ? new DevScreen(DevScreenKind.NoAccounts, Strings.Format(locale, Str.WidgetsA_Dev_SignInTo, ("service", service)), true, null)
                : new DevScreen(DevScreenKind.NoAccounts, string.Empty, false, null);
        }

        var targets = Targets(accounts, chosen, single);
        if (targets.Count == 0)
        {
            return new DevScreen(DevScreenKind.AccountsGone, Strings.Format(locale, Str.WidgetsA_Dev_AccountGone, ("service", service)), true, null);
        }

        var usable = targets.Where(a => a.Problem is null).ToList();
        var broken = targets.Where(a => a.Problem is not null).ToList();

        var byId = new Dictionary<string, DevOutcome>(StringComparer.Ordinal);
        foreach (var outcome in outcomes)
        {
            byId[outcome.AccountId] = outcome;
        }

        // A sign-in the service found to be refused counts as broken even before the account list
        // has heard about it.
        var refused = usable.Where(a => byId.TryGetValue(a.Id, out var o) && o.Status == FetchStatus.NotConfigured).ToList();
        broken.AddRange(refused);
        usable.RemoveAll(refused.Contains);

        if (usable.Count == 0)
        {
            var text = broken.Count == 1
                ? Strings.Format(locale, Str.WidgetsA_Dev_NeedsSignIn, ("account", broken[0].Label), ("service", service))
                : Strings.Format(locale, Str.WidgetsA_Dev_AllNeedSignIn, ("service", service));
            return new DevScreen(DevScreenKind.SignInRequired, text, true, null);
        }

        var failed = usable
            .Select(a => byId.TryGetValue(a.Id, out var o) ? o : null)
            .OfType<DevOutcome>()
            .Where(o => o.Status is not (FetchStatus.Ok or FetchStatus.Empty or FetchStatus.Loading))
            .ToList();

        var warning = broken.Count > 0
            ? Strings.Format(locale, Str.WidgetsA_Dev_NeedsSignIn, ("account", broken[0].Label), ("service", service))
            : failed.Count > 0
                ? Strings.Format(locale, Str.WidgetsA_Dev_CouldNotRefresh, ("problem", Explain(service, failed[0], now, locale)))
                : savedAt is { } saved
                    ? Saved(saved, now, locale)
                    : null;

        if (hasContent)
        {
            return new DevScreen(DevScreenKind.Content, string.Empty, broken.Count > 0, warning);
        }

        if (usable.Any(a => !byId.TryGetValue(a.Id, out var o) || o.Status == FetchStatus.Loading))
        {
            return new DevScreen(DevScreenKind.Loading, Strings.Get(locale, Str.Common_Loading), false, null);
        }

        if (failed.Count == usable.Count)
        {
            return new DevScreen(DevScreenKind.Failed, Strings.Format(locale, Str.WidgetsA_Dev_Sentence, ("text", Explain(service, failed[0], now, locale))), false, null);
        }

        return new DevScreen(DevScreenKind.Empty, emptyText, false, warning);
    }

    /// <summary>[S-747] The note on content shown from the saved copy until the first refresh lands.</summary>
    public static string Saved(DateTimeOffset savedAt, DateTimeOffset now, Locale locale)
        => now - savedAt < TimeSpan.FromMinutes(1)
            ? Strings.Get(locale, Str.WidgetsA_Dev_SavedJustNow)
            : Strings.Format(locale, Str.WidgetsA_Dev_Saved, ("when", LocaleFormat.Relative(locale, savedAt, now, RelativeStyle.Short)));

    /// <summary>A failed attempt in the widget's words.</summary>
    public static string Explain(string service, DevOutcome outcome, DateTimeOffset now, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.Detail is { Length: > 0 } detail && outcome.Status is not (FetchStatus.Offline or FetchStatus.RateLimited))
        {
            return detail.TrimEnd('.');
        }

        return outcome.Status switch
        {
            FetchStatus.Offline => Strings.Get(locale, Str.WidgetsA_Office_Problem_Offline),
            FetchStatus.Unreachable => Strings.Format(locale, Str.WidgetsA_Dev_Unreachable, ("service", service)),
            FetchStatus.TimedOut => Strings.Format(locale, Str.WidgetsA_Dev_TimedOut, ("service", service)),
            FetchStatus.RateLimited => outcome.RetryAt is { } at && at > now
                ? Strings.Format(locale, Str.WidgetsA_Dev_RateLimitedUntil, ("service", service), ("time", Clock(at, now, locale)))
                : Strings.Format(locale, Str.WidgetsA_Dev_RateLimited, ("service", service)),
            FetchStatus.NotConfigured => Strings.Format(locale, Str.WidgetsA_Dev_SignInAgain, ("service", service)),
            FetchStatus.Refused => Strings.Format(locale, Str.WidgetsA_Dev_Refused, ("service", service)),
            _ => Strings.Format(locale, Str.WidgetsA_Dev_Unexpected, ("service", service)),
        };
    }

    /// <summary>A time of day in the reader's offset, on the language's clock.</summary>
    private static string Clock(DateTimeOffset at, DateTimeOffset now, Locale locale)
        => LocaleFormat.Time(locale, at.ToOffset(now.Offset).DateTime);
}
