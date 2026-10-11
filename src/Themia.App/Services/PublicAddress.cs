using System.Net.NetworkInformation;
using Themia.App.Services.Web;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-2417] [S-2418] This connection's public address, for the network details widget, asked of
/// <c>api.ipify.org</c> (plain text, no key, no account) through the guarded <see cref="WebFetch"/>
/// client, so the request meets the network guard, the proxy and the TLS floor like every other one.
/// <para>
/// Only while a widget that wants it lives (<see cref="Acquire"/>), at most every
/// <see cref="NetworkDetailRules.PublicAddressLifetime"/>, and again at once after the machine's
/// addresses change (a new network is a new public address). One request in flight; the answer is
/// validated as an address before it is shown, so a captive portal's page never reaches the desktop.
/// Touched on the UI thread; the request's continuation comes back to it.
/// </para>
/// </summary>
internal static class PublicAddress
{
    private const string Scope = "metrics";

    private static readonly Uri Service = new("https://api.ipify.org");

    private static int _leases;
    private static string? _address;
    private static long? _askedAt;
    private static bool _failed;
    private static Task? _inFlight;

    // Set from the system's thread pool when the addresses change; read on the UI thread's tick.
    private static volatile bool _changed;

    /// <summary>Start wanting the address; dispose to stop.</summary>
    public static IDisposable Acquire()
    {
        if (_leases++ == 0)
        {
            NetworkChange.NetworkAddressChanged += OnAddressChanged;
        }

        return new SharedLease(Release);
    }

    /// <summary>The address as the widget shows it; starts a lookup when the last one is stale.</summary>
    public static PublicAddressReading Current()
    {
        if (_leases == 0)
        {
            return new PublicAddressReading(PublicAddressState.Off);
        }

        var now = Environment.TickCount64;
        var stale = _askedAt is not { } at || now - at >= (long)NetworkDetailRules.PublicAddressLifetime.TotalMilliseconds || now < at;

        if (_changed)
        {
            _changed = false;
            stale = true;
        }

        // A task that finished before it was stored (no network at all) is not in flight.
        if (stale && _inFlight is not { IsCompleted: false })
        {
            _askedAt = now;
            _inFlight = FetchAsync();
        }

        return _address is { } address
            ? new PublicAddressReading(PublicAddressState.Known, address)
            : new PublicAddressReading(_failed ? PublicAddressState.Failed : PublicAddressState.Checking);
    }

    private static async Task FetchAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await WebFetch.GetAsync(Service, "text/plain", timeout.Token).ConfigureAwait(true);

            if (response.Status == FetchStatus.Ok && NetworkDetailRules.PublicAddress(response.Body) is { } address)
            {
                _address = address;
                _failed = false;
                return;
            }

            // The body stays out of the log: it is somebody else's text (a captive portal's page).
            Log.Info(Scope, $"public address lookup failed: {response.Status} {response.Detail}");
            _failed = _address is null;
        }
        catch (OperationCanceledException)
        {
            Log.Info(Scope, "public address lookup timed out");
            _failed = _address is null;
        }
        catch (Exception ex)
        {
            // Fire-and-forget from a tick: an escape here would go unobserved.
            Log.Warn(Scope, "public address lookup failed", ex);
            _failed = _address is null;
        }
    }

    private static void OnAddressChanged(object? sender, EventArgs args)
    {
        // A system callback: only a flag, nothing that can throw.
        _changed = true;
    }

    private static void Release()
    {
        _leases = Math.Max(0, _leases - 1);

        if (_leases > 0)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged -= OnAddressChanged;

        // The next holder starts from "not asked yet": an address from an hour ago is not current.
        _address = null;
        _askedAt = null;
        _failed = false;
    }
}
