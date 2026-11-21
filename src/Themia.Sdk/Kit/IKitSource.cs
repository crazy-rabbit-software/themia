namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7061] Where a kit widget's items come from, as its binding reads them: the host's one sync of
/// the data (a feed sync, later the service sync) seen through what this widget asks. The binding
/// reads it on attach and whenever it says it changed; it never polls.
/// </summary>
public interface IKitSource
{
    /// <summary>Raised on the UI thread when what <see cref="Read"/> answers may have changed (an answer arrived, a fetch went out).</summary>
    event Action? Changed;

    /// <summary>What is held now: the items, how each source last went, the header title. Never asks the network.</summary>
    KitRead Read();

    /// <summary>A fetch for one of the widget's own sources is out ([S-2661]).</summary>
    bool Busy { get; }

    /// <summary>The widget is on screen: a source never fetched is asked now; <paramref name="refresh"/> is the user's Refresh, past freshness and back-off.</summary>
    void Show(bool refresh);
}

/// <summary>[S-7061] One reading of a kit source.</summary>
/// <param name="Items">The items in the order to show them, before the item rules ([S-7002]).</param>
/// <param name="Outcomes">How each source last went, in the sources' order.</param>
/// <param name="Title">The header title the data suggests, or null for the catalogue's ([S-403]: the user's own wins).</param>
/// <param name="Stated">
/// [S-7040] A state only the widget's own source knows, said in place of the list: its input refused before anything was
/// asked (an address its rules refuse), a store Windows keeps closed, a radio switched off; null while the kit decides.
/// </param>
/// <param name="WebHosts">The hosts an item may link to, or null for any public web address ([S-7002]).</param>
/// <param name="Accounts">The signed-in accounts offering what it needs, or null for a widget that reads none.</param>
/// <param name="Chosen">The accounts it names; empty for every one.</param>
public sealed record KitRead(
    IReadOnlyList<KitItem> Items,
    IReadOnlyList<KitOutcome> Outcomes,
    string? Title = null,
    KitStated? Stated = null,
    IReadOnlyCollection<string>? WebHosts = null,
    IReadOnlyList<KitAccount>? Accounts = null,
    IReadOnlyList<string>? Chosen = null)
{
    /// <summary>[S-9904] The sentence for an empty list whose reason the source's rules know ("No active cycle"), or null for the spec's own.</summary>
    public LocalText? EmptySentence { get; init; }

    /// <summary>[S-9919] Where the user widens what an account may read, for a service that grants per resource; null otherwise.</summary>
    public Uri? GrantPage { get; init; }

    /// <summary>[S-7064] The header badge's count (unread mail, open tasks), or null for none.</summary>
    public long? Count { get; init; }

    /// <summary>
    /// [S-7042] Lines the header's problem glyph says beside the stale rule's, from the widget's own
    /// writes: a change waiting to reach the account or refused by it ([S-2624]).
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>
/// [S-7040] A widget's own state for a list it cannot fill, in the kit's one state row: the sentence, as a problem (the
/// warning) or a plain state (the widget's glyph), and its one way out where it has one ("Allow", "Try again").
/// </summary>
public sealed record KitStated(string Sentence, bool Problem = true)
{
    /// <summary>Only the user can mend it in ⚙ › Accounts: the state row's way out is the Accounts link.</summary>
    public bool OffersAccounts { get; init; }

    /// <summary>The one action the state row offers, or null.</summary>
    public KitFix? Fix { get; init; }
}

/// <summary>[S-7040] A state row's one way out: its words and what it does.</summary>
public sealed record KitFix(string Label, Action Run);
