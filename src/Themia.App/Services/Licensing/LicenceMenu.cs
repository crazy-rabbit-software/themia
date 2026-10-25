using Themia.Core.Diagnostics;
using Themia.Core.Licensing;
using Themia.Core.Localization;
using Themia.Core.Settings;

namespace Themia.App.Services.Licensing;

/// <summary>
/// [S-3570]..[S-3573] What ⚙ › Themia Pro does, over the one licence service: the License page's
/// flows, run from the menu's lines, the same on every display. The price is asked of the Store the
/// first time a settings menu opens (once known, kept); the refusal that opened the menu is said
/// until the last menu closes; the purchase and the asking again say how they went under their lines.
/// </summary>
internal sealed class LicenceMenu
{
    private const string Scope = "license";

    private readonly LicenseService _license;
    private readonly SettingsOutcomes _outcomes;
    private bool _priceAsked;

    public LicenceMenu(LicenseService license, SettingsOutcomes outcomes)
    {
        _license = license ?? throw new ArgumentNullException(nameof(license));
        _outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
    }

    /// <summary>Raised on the UI thread when what the menu shows of the licence changed (the price came).</summary>
    public event Action? Changed;

    public LicenseStatus Status => _license.Status;

    public bool CanPurchase => _license.CanPurchase;

    /// <summary>Pro's price as the Store shows it, once it has said; null until then.</summary>
    public string? Price { get; private set; }

    /// <summary>[S-3573] Why the last create was refused, set by the refusal's "See plans"; said until the last menu closes.</summary>
    public string? Refusal { get; set; }

    /// <summary>A settings menu opened: the price is asked for, once.</summary>
    public void Opened()
    {
        if (_priceAsked || !_license.CanPurchase)
        {
            return;
        }

        _priceAsked = true;
        _ = AskPriceAsync();
    }

    /// <summary>[S-3573] The last settings menu closed: the refusal has been said.</summary>
    public void Closed() => Refusal = null;

    /// <summary>
    /// [S-3572] The Store's purchase dialog for Pro, owned by <paramref name="owner"/> (the window of
    /// the display whose ⚙ it was started from: a desktop app must hand the Store a window before it
    /// shows any UI, <c>IInitializeWithWindow</c>). How it ended stands under Upgrade.
    /// </summary>
    public async void Purchase(nint owner)
    {
        if (_outcomes.Busy(SettingsMenu.Keys.ProUpgrade) || _outcomes.Busy(SettingsMenu.Keys.ProRestore))
        {
            return;
        }

        _outcomes.Say(SettingsMenu.Keys.ProUpgrade, new MenuOutcome(Str.SettingsMenu_Purchasing.Text(), Busy: true));
        try
        {
            var outcome = await _license.PurchaseProAsync(owner).ConfigureAwait(true);
            var failed = outcome is not (PurchaseOutcome.Purchased or PurchaseOutcome.AlreadyOwned or PurchaseOutcome.Cancelled);
            _outcomes.Say(SettingsMenu.Keys.ProUpgrade, new MenuOutcome(LicenseText.Purchase(outcome), Failed: failed));
        }
        catch (Exception ex)
        {
            // An async void: nothing may escape it.
            Log.Error(Scope, "the upgrade failed", ex);
            _outcomes.Say(SettingsMenu.Keys.ProUpgrade, new MenuOutcome(LicenseText.Purchase(PurchaseOutcome.Unavailable), Failed: true));
        }
    }

    /// <summary>[S-3572] Ask the Store again; what this copy is then stands under Restore purchases.</summary>
    public async void Restore()
    {
        if (_outcomes.Busy(SettingsMenu.Keys.ProUpgrade) || _outcomes.Busy(SettingsMenu.Keys.ProRestore))
        {
            return;
        }

        _outcomes.Say(SettingsMenu.Keys.ProRestore, new MenuOutcome(Str.Shell_License_Checking.Text(), Busy: true));
        try
        {
            await _license.RefreshAsync().ConfigureAwait(true);
            _outcomes.Say(SettingsMenu.Keys.ProRestore, new MenuOutcome(LicenseText.Title(_license.Status)));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the licence refresh failed", ex);
            _outcomes.Say(SettingsMenu.Keys.ProRestore, new MenuOutcome(LicenseText.Purchase(PurchaseOutcome.Unavailable), Failed: true));
        }
    }

    private async Task AskPriceAsync()
    {
        try
        {
            if (await _license.ProPriceAsync().ConfigureAwait(true) is { } price)
            {
                Price = price;
                Changed?.Invoke();
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not show the Pro price", ex);
        }

        // No price this time (offline, the Store not listing it yet): asked again when ⚙ next opens.
        _priceAsked = false;
    }
}
