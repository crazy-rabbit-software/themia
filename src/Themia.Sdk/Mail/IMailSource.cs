using Themia.Core.Mail;

namespace Themia.App.Services.Mail;

/// <summary>
/// Where Mail widgets read from: the app's one <see cref="MailService"/>.
/// Everything here is on the UI thread.
/// </summary>
public interface IMailSource
{
    /// <summary>Raised when a mail account is added, removed, renamed or changes health.</summary>
    event Action? AccountsChanged;

    /// <summary>Every account that can feed a Mail widget, in the order they were added.</summary>
    IReadOnlyList<MailAccount> Accounts { get; }

    /// <summary>
    /// A shared, live feed for a query. Dispose it when the widget lets go; the last one to let go
    /// stops the feed's requests.
    /// </summary>
    IMailFeed Open(MailQuery query);

    /// <summary>
    /// Change a message on its server. Returns null when it worked, or a sentence saying why not —
    /// never throws, because it runs from a menu click.
    /// </summary>
    Task<string?> ChangeAsync(MailMessage message, MailChange change);

    /// <summary>[S-547] Open a message where the user reads mail. False when there is nowhere to open it.</summary>
    bool Open(MailMessage message);

    /// <summary>[S-547] Start a reply. False when there is no way to.</summary>
    bool Reply(MailMessage message);

    /// <summary>[S-558] Start a new message from <paramref name="account"/>. False when the shell would not open it.</summary>
    /// <param name="known">The messages on screen: they tell which Outlook on the web a Microsoft account lives on.</param>
    bool Compose(MailAccount account, IReadOnlyList<MailMessage> known);
}

/// <summary>One widget's handle on a shared feed.</summary>
public interface IMailFeed : IDisposable
{
    /// <summary>Raised on the UI thread whenever <see cref="Snapshot"/> changes.</summary>
    event Action? Updated;

    MailFeedSnapshot Snapshot { get; }

    /// <summary>[S-546] Fetch every account now, whatever the schedule says (a throttled one still waits).</summary>
    void Refresh();
}
