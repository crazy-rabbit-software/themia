using Themia.Core.Accounts;

namespace Themia.Core.Model;

/// <summary>
/// [S-2848] A widget about one place on Earth: the weather family (current conditions, forecast,
/// air quality, rain) and the sky (Sun, Moon). One rule for all of them: the place can be chosen and
/// it can be cleared, and a cleared place is no place, so the widget waits for setup ([S-1860])
/// rather than falling back to somewhere the user never picked.
/// </summary>
public interface IPlaced : ISetupRule
{
    /// <summary>Whether a place is chosen: both coordinates set and finite.</summary>
    bool HasLocation { get; }

    /// <summary>
    /// Forget the place: the coordinates and the name it resolved to. Display preferences (whether
    /// the header names the place) are the user's and stay.
    /// </summary>
    void ClearLocation();

    /// <summary>[S-2848] No place chosen, or cleared, is the Set up state.</summary>
    bool ISetupRule.NeedsSetup(AccountFeatures signedIn) => !HasLocation;
}
