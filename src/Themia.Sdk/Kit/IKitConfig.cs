using Themia.Core.Accounts;
using Themia.Core.Model;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7050] A widget built on the kit, as its config states it: what it reads, whether it is set up,
/// which accounts it reads and which optional fields the user has on. Its setup rule
/// (<see cref="ISetupRule"/>, [S-1860]) follows from those, so a kit widget writes no setup rule of
/// its own: it waits for setup while it has nothing to read, or while no signed-in account offers
/// what it needs ([S-1861]). What the background sync keeps fresh for it stays its
/// <c>IInterests</c> ([S-2608]).
/// </summary>
public interface IKitConfig : ISetupRule
{
    /// <summary>The feature its accounts must offer, or null for a widget that reads no account (a web source).</summary>
    FeatureId? Needs { get; }

    /// <summary>False while it has nothing to read (no address, no query): the card's Set up ([S-1860]).</summary>
    bool Configured { get; }

    /// <summary>The accounts it reads; empty is "every account offering <see cref="Needs"/>".</summary>
    IReadOnlyList<string> AccountIds { get; }

    /// <summary>The optional fields the user has on (the settings' Shows rows write them).</summary>
    KitShow Shows { get; }

    bool ISetupRule.NeedsSetup(FeatureSet signedIn) => KitSetup.Needed(this, signedIn);
}

/// <summary>[S-7050] The kit's one setup rule.</summary>
public static class KitSetup
{
    public static bool Needed(IKitConfig config, FeatureSet signedIn)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(signedIn);
        return !config.Configured || (config.Needs is { } need && !signedIn.Has(need));
    }
}
