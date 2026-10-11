using System.Net.NetworkInformation;
using System.Security.Cryptography;
using Microsoft.UI.Dispatching;
using Themia.App.Services.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Storage;
using Themia.Core.Sync;

namespace Themia.App.Services.Sync;

/// <summary>
/// [S-2600] What every sync service shares: the sealed on-device store, the widgets' needs across every
/// view ([S-2608]) and the "network is back" signal ([S-2609]). One per host.
/// <para>
/// Everything raised here is raised on the UI thread, where the services keep their state.
/// </para>
/// </summary>
internal sealed class SyncHost : IDisposable
{
    private const string Scope = "sync";

    private readonly DispatcherQueue? _dispatcher;
    private bool _watching;
    private volatile bool _wasAvailable;
    private volatile bool _disposed;
    private string? _interestsKey;

    private SyncHost(DispatcherQueue? dispatcher, SyncVault vault)
    {
        _dispatcher = dispatcher;
        Vault = vault;
    }

    public SyncVault Vault { get; }

    /// <summary>What the widgets in every view need. Starts empty; the host sets it from the state.</summary>
    public SyncInterests Interests { get; private set; } = SyncInterests.None;

    /// <summary>Raised on the UI thread when <see cref="Interests"/> changed.</summary>
    public event Action? InterestsChanged;

    /// <summary>[S-2609] Raised on the UI thread when Windows reports a network again after none.</summary>
    public event Action? NetworkAvailable;

    /// <summary>
    /// [S-2601] Where the shelves live for a state file: under <c>%LOCALAPPDATA%\Themia\sync</c> for the
    /// app's own state, beside any other state file (a portable or test profile takes its synced data
    /// with it, as it takes its accounts).
    /// </summary>
    public static string DirectoryFor(string statePath)
    {
        var own = string.Equals(Path.GetFullPath(statePath), Path.GetFullPath(AppStateStore.DefaultPath()), StringComparison.OrdinalIgnoreCase);
        return own
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppStateStore.FolderName, SyncVault.FolderName)
            : Path.Combine(Path.GetDirectoryName(statePath) ?? ".", SyncVault.FolderName);
    }

    /// <summary>
    /// Open the store in <paramref name="directory"/>, or a detached one for null (a debug render over a
    /// detached state: nothing read, nothing written). A key that cannot be made or opened leaves a
    /// detached store for this run, loudly: never a plain-text one.
    /// </summary>
    public static SyncHost Open(DispatcherQueue? dispatcher, string? directory)
    {
        if (directory is null)
        {
            return new SyncHost(dispatcher, SyncVault.Detached());
        }

        var key = VaultKey.LoadOrCreate(directory);
        if (key is null)
        {
            Log.Error(Scope, "the offline store has no key this run; widgets keep data in memory only");
            return new SyncHost(dispatcher, SyncVault.Detached());
        }

        return new SyncHost(dispatcher, new SyncVault(directory, key));
    }

    public void SetInterests(SyncInterests interests)
    {
        ArgumentNullException.ThrowIfNull(interests);

        var key = interests.Key();
        if (key == _interestsKey)
        {
            return;
        }

        _interestsKey = key;
        Interests = interests;
        Raise(InterestsChanged, "interests");
    }

    /// <summary>Begin listening for the network coming back. Idempotent.</summary>
    public void Start()
    {
        if (_watching || _disposed || _dispatcher is null)
        {
            return;
        }

        _watching = true;
        _wasAvailable = NetworkInterface.GetIsNetworkAvailable();
        NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnAddressChanged;
    }

    public Task FlushAsync() => Vault.FlushAsync();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_watching)
        {
            NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
            NetworkChange.NetworkAddressChanged -= OnAddressChanged;
        }

        InterestsChanged = null;
        NetworkAvailable = null;
    }

    // Both run on a system thread pool thread: they hand over to the UI thread and return.
    private void OnAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs args) => Check(args.IsAvailable);

    private void OnAddressChanged(object? sender, EventArgs args) => Check(null);

    private void Check(bool? available)
    {
        try
        {
            var now = available ?? NetworkInterface.GetIsNetworkAvailable();
            var cameBack = now && !_wasAvailable;

            // An address change on a network that stayed up (a Wi-Fi hop) is a fresh route too: a
            // sync stuck on "unreachable" is worth one more try.
            var hopped = now && available is null;
            _wasAvailable = now;

            if ((cameBack || hopped) && !_disposed)
            {
                _dispatcher?.TryEnqueue(() =>
                {
                    if (!_disposed)
                    {
                        Log.Info(Scope, "the network is available; waking the syncs that were waiting for it");
                        Raise(NetworkAvailable, "network");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            // A system callback: nothing may escape it ([S-60]).
            Log.Error(Scope, "the network-change handler failed", ex);
        }
    }

    private static void Raise(Action? handlers, string what)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Action>())
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                // One service's handler must not stop the others hearing it.
                Log.Error(Scope, $"a {what} listener threw", ex);
            }
        }
    }
}

/// <summary>
/// [S-2603] The shelves' key: 32 random bytes, sealed with DPAPI for this Windows user in
/// <c>sync\vault.key</c>. A key that will not open (another user, another PC, damage) is set aside and
/// replaced; the shelves it sealed then fail to open, are set aside in turn, and resync. Nothing ever
/// writes the key unsealed.
/// </summary>
internal static class VaultKey
{
    private const string Scope = "sync-store";

    public const string FileName = "vault.key";

    public static byte[]? LoadOrCreate(string directory)
    {
        var path = Path.Combine(directory, FileName);

        try
        {
            if (File.Exists(path))
            {
                var stored = File.ReadAllBytes(path);
                if (Dpapi.OpenBytes(stored) is { Length: SealedBlob.KeySize } key)
                {
                    return key;
                }

                Log.Warn(Scope, "the offline store's key does not open for this user; starting a new store");
                File.Move(path, path + ".corrupt", overwrite: true);
            }

            var fresh = SealedBlob.NewKey();
            var sealedKey = Dpapi.SealBytes(fresh);

            Directory.CreateDirectory(directory);
            var temp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllBytes(temp, sealedKey);
            File.Move(temp, path, overwrite: true);
            Log.Info(Scope, "created the offline store's key");
            return fresh;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Log.Error(Scope, "could not open or create the offline store's key", ex);
            return null;
        }
    }
}
