using Themia.Core.Accounts;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Tasks;

/// <summary>
/// [S-615] Tasks' one stale rule ([S-7042]): an account list held from its last good refresh is a kit
/// outcome — named by its account, its last refresh's status in the kit's vocabulary, stamped with
/// when the tasks on screen are from — so the header's problem glyph says a failed refresh as every
/// kit widget does and the Refresh line carries the age. A list on this PC has no refresh and no age.
/// </summary>
public static class TaskFreshness
{
    /// <param name="account">The account's name, or null for a list on this PC.</param>
    /// <param name="loaded">Something is held for the list.</param>
    /// <param name="failure">Why the last refresh failed, or null.</param>
    /// <param name="fetchedAt">When what is held was fetched.</param>
    public static IReadOnlyList<KitOutcome> Outcomes(string? account, bool loaded, AccountException? failure, DateTimeOffset? fetchedAt)
    {
        if (account is null)
        {
            return [];
        }

        if (!loaded)
        {
            return [new KitOutcome(account, FetchStatus.Loading)];
        }

        return [new KitOutcome(account, failure?.ToFetchStatus() ?? FetchStatus.Ok, HeldAt: fetchedAt)];
    }
}
