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
/// <param name="Refused">A sentence when the widget's own input was refused before anything was asked (an address its rules refuse); empty for nothing to read at all.</param>
/// <param name="WebHosts">The hosts an item may link to, or null for any public web address ([S-7002]).</param>
/// <param name="Accounts">The signed-in accounts offering what it needs, or null for a widget that reads none.</param>
/// <param name="Chosen">The accounts it names; empty for every one.</param>
public sealed record KitRead(
    IReadOnlyList<KitItem> Items,
    IReadOnlyList<KitOutcome> Outcomes,
    string? Title = null,
    string? Refused = null,
    IReadOnlyCollection<string>? WebHosts = null,
    IReadOnlyList<KitAccount>? Accounts = null,
    IReadOnlyList<string>? Chosen = null);
