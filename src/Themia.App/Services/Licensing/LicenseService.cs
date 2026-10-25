using System.Security.Cryptography;
using Microsoft.UI.Dispatching;
using Themia.App.Services.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Licensing;
using Themia.Core.Storage;
using Windows.Services.Store;

namespace Themia.App.Services.Licensing;

/// <summary>
/// [S-1063] Themia's licence, from the Microsoft Store and nowhere else.
/// <para>
/// One instance, owned by the host. A packaged copy asks <see cref="StoreContext"/> for the app's
/// licence and the durable Pro add-on (<see cref="StoreIds.ProAddOn"/>); a copy without package
/// identity (F5, a loose publish folder) never touches the Store and is unrestricted, labelled
/// "Developer build" ([S-1064]). The tier itself comes from <see cref="LicenseRules.Decide"/>, so every
/// rule is pinned by tests and this class only gathers the facts.
/// </para>
/// <para>
/// Live: <see cref="Changed"/> fires (on the UI thread) after a refresh, a purchase, and whenever
/// Windows reports that the Store's licences changed (<see cref="StoreContext.OfflineLicensesChanged"/>,
/// e.g. a purchase made in the Store app or a refund). Refreshes are single-flight.
/// </para>
/// </summary>
internal sealed class LicenseService : IDisposable
{
    private const string Scope = "license";

    private readonly bool _packaged;
    private readonly DispatcherQueue? _ui;
    private readonly string _cachePath;

    private StoreContext? _store;
    private Task? _refreshing;
    private StoreAnswer _answer = StoreAnswer.NotAsked;
    private DateTimeOffset? _confirmedPro;
    private bool _disposed;

    public LicenseService(bool packaged)
    {
        _packaged = packaged;
        _ui = DispatcherQueue.GetForCurrentThread();
        _cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppStateStore.FolderName,
            "license.dat");

        _confirmedPro = _packaged ? ReadCache() : null;
        Status = LicenseRules.Decide(_packaged, _answer, _confirmedPro, DateTimeOffset.UtcNow);
    }

    /// <summary>The current tier and why. Read on the UI thread.</summary>
    public LicenseStatus Status { get; private set; }

    /// <summary>Whether a Store is there to ask (the copy is packaged).</summary>
    public bool HasStore => _packaged;

    /// <summary>Whether the Pro add-on can be bought: packaged, and its Store id is a real one.</summary>
    public bool CanPurchase => _packaged && LicenseRules.IsStoreId(StoreIds.ProAddOn);

    /// <summary>Raised on the UI thread whenever <see cref="Status"/> may have changed.</summary>
    public event Action? Changed;

    /// <summary>Ask the Store once at start, and listen for licence changes. Does nothing for a developer build.</summary>
    public void Start()
    {
        if (!_packaged)
        {
            Log.Info(Scope, "no package identity: developer build, unrestricted");
            return;
        }

        _ = RefreshAsync();
    }

    /// <summary>
    /// Ask the Store again ("Restore purchases"). Joins a refresh already under way rather than
    /// starting a second one.
    /// </summary>
    public Task RefreshAsync()
    {
        if (!_packaged)
        {
            return Task.CompletedTask;
        }

        return _refreshing ??= RunRefreshAsync();
    }

    private async Task RunRefreshAsync()
    {
        try
        {
            var answer = await AskStoreAsync().ConfigureAwait(true);
            Apply(answer);
        }
        finally
        {
            _refreshing = null;
        }
    }

    /// <summary>
    /// Open the Store's purchase dialog for Pro, owned by <paramref name="owner"/> (the window of the
    /// display whose ⚙ it was asked from, [S-3572]): a desktop app must hand the Store a window before it can show any UI
    /// (<c>IInitializeWithWindow</c>).
    /// </summary>
    public async Task<PurchaseOutcome> PurchaseProAsync(nint owner)
    {
        if (!_packaged)
        {
            return PurchaseOutcome.Unavailable;
        }

        if (!LicenseRules.IsStoreId(StoreIds.ProAddOn))
        {
            Log.Warn(Scope, "the Pro add-on's Store id is still the placeholder; no purchase offered");
            return PurchaseOutcome.NotConfigured;
        }

        try
        {
            var store = Context();
            WinRT.Interop.InitializeWithWindow.Initialize(store, owner);

            var result = await store.RequestPurchaseAsync(StoreIds.ProAddOn);
            var outcome = result.Status switch
            {
                StorePurchaseStatus.Succeeded => PurchaseOutcome.Purchased,
                StorePurchaseStatus.AlreadyPurchased => PurchaseOutcome.AlreadyOwned,
                StorePurchaseStatus.NotPurchased => PurchaseOutcome.Cancelled,
                StorePurchaseStatus.NetworkError => PurchaseOutcome.NetworkError,
                _ => PurchaseOutcome.ServerError,
            };

            if (result.ExtendedError is { } error)
            {
                Log.Warn(Scope, $"purchase ended {result.Status}", error);
            }
            else
            {
                Log.Info(Scope, $"purchase ended {result.Status}");
            }

            // Whatever happened, the Store is the judge: ask it rather than assume.
            await RefreshAsync().ConfigureAwait(true);
            return outcome;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not start the Store purchase", ex);
            return PurchaseOutcome.Unavailable;
        }
    }

    /// <summary>Pro's price as the Store shows it to this user ("CHF 19.00"), or null when it cannot say.</summary>
    public async Task<string?> ProPriceAsync()
    {
        if (!CanPurchase)
        {
            return null;
        }

        try
        {
            var result = await Context().GetStoreProductsAsync(["Durable"], [StoreIds.ProAddOn]);
            if (result.ExtendedError is { } error)
            {
                Log.Warn(Scope, "the Store did not list the Pro add-on", error);
                return null;
            }

            return result.Products.TryGetValue(StoreIds.ProAddOn, out var product) ? product.Price?.FormattedPrice : null;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read the Pro price", ex);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_store is { } store)
        {
            store.OfflineLicensesChanged -= OnLicencesChanged;
        }
    }

    private StoreContext Context()
    {
        if (_store is null)
        {
            _store = StoreContext.GetDefault();
            _store.OfflineLicensesChanged += OnLicencesChanged;
        }

        return _store;
    }

    private void OnLicencesChanged(StoreContext sender, object args)
    {
        try
        {
            Log.Info(Scope, "the Store reports a licence change");
            if (_ui is null || !_ui.TryEnqueue(() => _ = RefreshAsync()))
            {
                Log.Warn(Scope, "could not schedule a licence refresh on the UI thread");
            }
        }
        catch (Exception ex)
        {
            // A WinRT event callback: nothing may escape it.
            Log.Error(Scope, "licence change handler failed", ex);
        }
    }

    private async Task<StoreAnswer> AskStoreAsync()
    {
        try
        {
            var licence = await Context().GetAppLicenseAsync();
            if (licence is null)
            {
                Log.Warn(Scope, "the Store returned no app licence");
                return StoreAnswer.Unavailable;
            }

            var addOns = licence.AddOnLicenses.Values.Select(l => (l.SkuStoreId, l.IsActive)).ToList();
            var owned = LicenseRules.OwnsAddOn(addOns, StoreIds.ProAddOn);
            Log.Info(Scope, $"Store licence: app active {licence.IsActive}, {addOns.Count} add-on licence(s), Pro {(owned ? "owned" : "not owned")}");
            return owned ? StoreAnswer.Owned : StoreAnswer.NotOwned;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not ask the Store for the licence", ex);
            return StoreAnswer.Unavailable;
        }
    }

    private void Apply(StoreAnswer answer)
    {
        _answer = answer;
        var now = DateTimeOffset.UtcNow;

        // [S-1069] The cache follows the Store's clear answers only; "unavailable" leaves it alone.
        if (answer == StoreAnswer.Owned)
        {
            _confirmedPro = now;
            WriteCache(now);
        }
        else if (answer == StoreAnswer.NotOwned && _confirmedPro is not null)
        {
            _confirmedPro = null;
            ClearCache();
        }

        var before = Status;
        Status = LicenseRules.Decide(_packaged, _answer, _confirmedPro, now);
        if (Status != before)
        {
            Log.Info(Scope, $"licence is now {Status.Tier} ({Status.Basis})");
        }

        Raise();
    }

    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a licence listener failed", ex);
        }
    }

    private DateTimeOffset? ReadCache()
    {
        string stored;
        try
        {
            stored = File.ReadAllText(_cachePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "could not read the licence cache; no offline grace", ex);
            return null;
        }

        var opened = Dpapi.Open(stored.Trim());
        if (opened.State != SecretState.Open)
        {
            Log.Warn(Scope, $"licence cache is {opened.State}; no offline grace");
            return null;
        }

        var (state, confirmed) = LicenseCache.Parse(opened.Value);
        if (state == LicenseCacheState.Corrupt)
        {
            Log.Warn(Scope, "licence cache is corrupt; no offline grace");
        }

        return confirmed;
    }

    private void WriteCache(DateTimeOffset confirmed)
    {
        var temp = _cachePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            File.WriteAllText(temp, Dpapi.Seal(LicenseCache.Format(confirmed)));
            File.Move(temp, _cachePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // Only the offline grace is lost; the Store still answers on the next start.
            Log.Warn(Scope, "could not save the licence cache", ex);
        }
    }

    private void ClearCache()
    {
        try
        {
            File.Delete(_cachePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Harmless: a stale confirmation only matters when the Store is unreachable, and it expires.
            Log.Warn(Scope, "could not remove the licence cache", ex);
        }
    }
}
