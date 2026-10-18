using Themia.Core.Mail;

namespace Themia.Core.Sync;

/// <param name="AccountIds">The widget's accounts; empty means every mail account ([S-541]).</param>
public sealed record MailInterest(IReadOnlyList<string> AccountIds, MailFolder Folder, int Count);

/// <param name="AccountIds">Empty means every calendar account ([S-562]).</param>
/// <param name="Calendars">The widget's calendar choice per account ([S-567]).</param>
public sealed record CalendarInterest(IReadOnlyList<string> AccountIds, IReadOnlyDictionary<string, List<string>> Calendars, bool Birthdays);

/// <param name="AccountIds">Empty means every contacts account ([S-580]).</param>
public sealed record ContactsInterest(IReadOnlyList<string> AccountIds);

/// <param name="AccountId">Empty means the first task account ([S-590]).</param>
/// <param name="ListId">A list, Today, or empty for the account's default list.</param>
public sealed record TaskInterest(string AccountId, string ListId, bool ShowCompleted, TimeSpan Interval);
