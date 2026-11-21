using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Office;

/// <summary>What an office problem's message is about: the one word in it that differs per widget.</summary>
public enum SyncSubject
{
    Calendar,
    Contacts,
}

/// <summary>
/// [S-588] Why the calendar or contacts widget has nothing (or nothing new) from an account, in the one failure
/// vocabulary (<see cref="FetchStatus"/>, [S-2691]): no second set of names. Each needs a different thing from the
/// user: signing in again and granting access are done in ⚙ › Accounts, the network ones fix themselves, and the rest
/// go to the log. An account the widget names that has been signed out is <see cref="FetchStatus.NotConfigured"/>.
/// </summary>
public static class OfficeProblems
{
    /// <summary>Whether a sync's status is a problem the widget says: anything but a sync that worked, found nothing, or has not run yet.</summary>
    public static bool IsProblem(FetchStatus status)
        => status is not (FetchStatus.Ok or FetchStatus.Empty or FetchStatus.Loading);

    /// <summary>Whether only the user can mend it, in ⚙ › Accounts.</summary>
    public static bool NeedsUser(FetchStatus status)
        => status is FetchStatus.NotConfigured or FetchStatus.SignInRequired or FetchStatus.Forbidden;

    /// <summary>
    /// One sentence for the widget, naming what is wrong for <paramref name="subject"/>, in
    /// <paramref name="locale"/>. The refusal is one whole sentence per subject, not a noun glued in.
    /// </summary>
    public static string Message(FetchStatus status, SyncSubject subject, Locale locale) => status switch
    {
        FetchStatus.Ok or FetchStatus.Empty or FetchStatus.Loading => string.Empty,
        FetchStatus.Forbidden => Strings.Get(locale, subject == SyncSubject.Contacts
            ? Str.WidgetsA_Office_Problem_ForbiddenContacts
            : Str.WidgetsA_Office_Problem_ForbiddenCalendar),
        _ => Strings.Get(locale, status switch
        {
            FetchStatus.NotConfigured => Str.WidgetsA_Office_Problem_NoAccount,
            FetchStatus.SignInRequired => Str.WidgetsA_Office_Problem_SignIn,
            FetchStatus.Offline => Str.WidgetsA_Office_Problem_Offline,
            FetchStatus.Unreachable => Str.WidgetsA_Office_Problem_Unreachable,
            FetchStatus.TimedOut => Str.WidgetsA_Office_Problem_TimedOut,
            FetchStatus.RateLimited => Str.WidgetsA_Office_Problem_RateLimited,
            _ => Str.WidgetsA_Office_Problem_BadResponse,
        }),
    };
}
