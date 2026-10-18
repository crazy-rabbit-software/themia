using Themia.Core.Accounts;
using Themia.Core.Localization;

namespace Themia.Core.Office;

/// <summary>
/// [S-588] Why the calendar or contacts widget has nothing (or nothing new) from an account. Named,
/// because each needs a different thing from the user: signing in again and granting access are
/// done on the Accounts page, the network ones fix themselves, and the rest go to the log.
/// </summary>
/// <summary>What a <see cref="SyncProblem"/> message is about: the one word in it that differs per widget.</summary>
public enum SyncSubject
{
    Calendar,
    Contacts,
}

public enum SyncProblem
{
    None,

    /// <summary>The account the widget names has been signed out.</summary>
    NoAccount,

    /// <summary>The sign-in was revoked, the password changed, or the secret cannot be opened here.</summary>
    SignInRequired,

    /// <summary>Signed in, but this account did not grant calendar or contacts access (403).</summary>
    Forbidden,

    Offline,
    Unreachable,
    TimedOut,
    RateLimited,
    ServerError,
    BadResponse,
}

public static class SyncProblems
{
    public static SyncProblem From(AccountError error) => error switch
    {
        AccountError.UnknownAccount => SyncProblem.NoAccount,
        AccountError.SignInRequired => SyncProblem.SignInRequired,
        AccountError.Forbidden => SyncProblem.Forbidden,
        AccountError.Offline => SyncProblem.Offline,
        AccountError.Unreachable => SyncProblem.Unreachable,
        AccountError.TimedOut => SyncProblem.TimedOut,
        AccountError.RateLimited => SyncProblem.RateLimited,
        AccountError.ServerError => SyncProblem.ServerError,
        _ => SyncProblem.BadResponse,
    };

    /// <summary>Whether only the user can mend it, on the Accounts page.</summary>
    public static bool NeedsUser(SyncProblem problem)
        => problem is SyncProblem.NoAccount or SyncProblem.SignInRequired or SyncProblem.Forbidden;

    /// <summary>
    /// [S-588] How long a failure is held before the same request is tried again on a timer. A
    /// refusal only the user can mend is retried hourly, not every poll (the original's [S-39]); a
    /// rate limit waits a quarter of an hour; anything else is retried on the next tick.
    /// </summary>
    public static TimeSpan HoldFor(SyncProblem problem) => problem switch
    {
        SyncProblem.SignInRequired or SyncProblem.Forbidden or SyncProblem.NoAccount => TimeSpan.FromHours(1),
        SyncProblem.RateLimited => TimeSpan.FromMinutes(15),
        _ => TimeSpan.Zero,
    };

    /// <summary>
    /// One sentence for the widget, naming what is wrong for <paramref name="subject"/>, in
    /// <paramref name="locale"/>. The refusal is one whole sentence per subject, not a noun glued in.
    /// </summary>
    public static string Message(SyncProblem problem, SyncSubject subject, Locale locale) => problem switch
    {
        SyncProblem.None => string.Empty,
        SyncProblem.Forbidden => Strings.Get(locale, subject == SyncSubject.Contacts
            ? Str.WidgetsA_Office_Problem_ForbiddenContacts
            : Str.WidgetsA_Office_Problem_ForbiddenCalendar),
        _ => Strings.Get(locale, problem switch
        {
            SyncProblem.NoAccount => Str.WidgetsA_Office_Problem_NoAccount,
            SyncProblem.SignInRequired => Str.WidgetsA_Office_Problem_SignIn,
            SyncProblem.Offline => Str.WidgetsA_Office_Problem_Offline,
            SyncProblem.Unreachable => Str.WidgetsA_Office_Problem_Unreachable,
            SyncProblem.TimedOut => Str.WidgetsA_Office_Problem_TimedOut,
            SyncProblem.RateLimited => Str.WidgetsA_Office_Problem_RateLimited,
            SyncProblem.ServerError => Str.WidgetsA_Office_Problem_ServerError,
            _ => Str.WidgetsA_Office_Problem_BadResponse,
        }),
    };
}
