using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-14962] The network kinds' one Adapter line (owner, 2026-11-21: "local address you should be able to select the network
/// adapter, for the similar widgets the same"): every kind that reads one adapter's figures (<see cref="IHoldsAdapter"/>)
/// adds this line, marked as the shared <see cref="SharedSettings.NetworkAdapter"/>.
/// </summary>
public static class AdapterMenuLines
{
    private const string Scope = "network-adapter";

    private static readonly Lock Gate = new();
    private static readonly List<(WeakReference<WidgetMenuLines> Menu, IReadOnlyList<NetworkAdapterEntry> Shown)> Waiting = [];
    private static IDisposable? _watch;

    /// <summary>
    /// [S-14962] Which adapter: Automatic, then every adapter Windows has by its name (one that is down "(not connected)"),
    /// and a chosen one that is not there still offered by the name it was chosen under (<see cref="NetworkAdapterRules.Options"/>).
    /// Picking one stores its id and its name. The menu is built again when the adapters change while it is shown.
    /// </summary>
    /// <param name="adapters">The adapters Windows has (the host's walk, <c>AdapterList.Now()</c>).</param>
    public static SettingsLine Adapter(WidgetMenuLines menu, IHoldsAdapter held, IReadOnlyList<NetworkAdapterEntry> adapters)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(adapters);

        var (options, current) = NetworkAdapterRules.Options(Strings.Current, adapters, held.Adapter, held.AdapterName);
        Follow(menu, adapters);

        return WidgetMenuLines.Shared(SharedSettings.NetworkAdapter, menu.Choice(Glyphs.Network, Str.NetworkAdapter_Label.Text(), options, current, value =>
        {
            var id = NetworkAdapterRules.Normalise(value);
            held.Adapter = id;
            held.AdapterName = id is null ? null : NetworkAdapterRules.Find(adapters, id)?.Name ?? held.AdapterName;
        }));
    }

    /// <summary>
    /// [S-14962] Keep <paramref name="menu"/> told while it is shown: when Windows' adapters change, a menu whose list
    /// differs from what it showed is built again (<see cref="WidgetMenuLines.Outdate"/>). Held weakly: a menu the page
    /// let go of is forgotten; the host's watch is held only while a menu waits.
    /// </summary>
    private static void Follow(WidgetMenuLines menu, IReadOnlyList<NetworkAdapterEntry> shown)
    {
        lock (Gate)
        {
            Waiting.RemoveAll(w => !w.Menu.TryGetTarget(out var held) || ReferenceEquals(held, menu));
            Waiting.Add((new WeakReference<WidgetMenuLines>(menu), shown));
            _watch ??= AdapterList.Watch(Changed);
        }
    }

    /// <summary>Windows says the adapters may have changed: any thread, so nothing may escape.</summary>
    private static void Changed()
    {
        try
        {
            var now = AdapterList.Now();
            var outdated = new List<WidgetMenuLines>();
            lock (Gate)
            {
                for (var i = Waiting.Count - 1; i >= 0; i--)
                {
                    if (!Waiting[i].Menu.TryGetTarget(out var menu))
                    {
                        Waiting.RemoveAt(i);
                    }
                    else if (!NetworkAdapterRules.Same(Waiting[i].Shown, now))
                    {
                        outdated.Add(menu);
                        Waiting.RemoveAt(i);
                    }
                }

                if (Waiting.Count == 0)
                {
                    _watch?.Dispose();
                    _watch = null;
                }
            }

            foreach (var menu in outdated)
            {
                menu.Outdate();
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not follow the network adapters for a menu", ex);
        }
    }
}
