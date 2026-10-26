using Microsoft.UI.Xaml;
using Themia.App.Modules;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Services;

/// <summary>
/// [S-6902] Installs the generated <see cref="ModuleRegistry"/> into <see cref="ModuleCatalog"/>
/// and <see cref="ProviderCatalog"/> ([S-6910]), first thing in the process, so nothing reads a
/// widget or an account before the registry is there.
/// </summary>
internal static class ModuleHost
{
    private static Dictionary<WidgetId, IWidgetModule>? _byKind;

    /// <summary>The module of an installed kind.</summary>
    public static IWidgetModule Module(WidgetId kind)
        => (_byKind ?? throw new InvalidOperationException("the module registry is not installed ([S-6902])"))
            .TryGetValue(kind, out var module) ? module : throw new KeyNotFoundException($"no widget module declares the kind {kind}");

    /// <summary>Build the view for a kind: a <see cref="FrameworkElement"/> that also implements <c>IWidgetView</c>.</summary>
    public static FrameworkElement CreateView(WidgetId kind, IWidgetHost host) => Module(kind).CreateView(host);

    /// <summary>
    /// [S-3719] A widget's own lines on its properties menu, built by its module over
    /// <paramref name="widget"/>'s config (the live one: the lines change it in place and
    /// <paramref name="save"/> stores it), asking for text and confirmations beside the menu. A
    /// module that throws is logged and has none. [S-3749] <paramref name="openSetting"/> opens the
    /// level a link row leads to (⚙ › Accounts, [S-3558]; a format; an update interval).
    /// </summary>
    public static WidgetMenuLines? MenuLines(WidgetInstance widget, Action save, Action<WidgetTextAsk> ask, Action<WidgetConfirm> confirm, Action<SettingLink>? openSetting = null)
    {
        ArgumentNullException.ThrowIfNull(widget);

        // [S-2851] Where Windows' location stands, and its settings page where there is a host to open it.
        var lines = new WidgetMenuLines(save, ask, confirm)
        {
            OpenSetting = openSetting,
            Device = AppServices.DeviceLocationNow,
            OpenLocationSettings = AppServices.DeviceLocation is null ? null : DeviceLocationService.OpenSettings,
        };
        try
        {
            Module(widget.Kind).MenuLines(lines, widget.Config);
        }
        catch (Exception ex)
        {
            Themia.Core.Diagnostics.Log.Error("modules", $"the {widget.Kind} widget's menu lines could not be built", ex);
            return null;
        }

        return lines;
    }

    public static void Install()
    {
        var widgets = ModuleRegistry.Widgets;
        for (var i = 0; i < widgets.Count; i++)
        {
            // The id the attribute declares is the id the descriptor carries: one id per widget, everywhere.
            if (!string.Equals(widgets[i].Descriptor.Id.Value, ModuleRegistry.WidgetIds[i], StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"module {widgets[i].GetType().Name} is declared as '{ModuleRegistry.WidgetIds[i]}' but its descriptor says '{widgets[i].Descriptor.Id}'");
            }
        }

        ModuleCatalog.Install(widgets.Select(m => m.Descriptor));
        _byKind = widgets.ToDictionary(m => m.Descriptor.Id);

        // [S-6910] The providers the same way: the id the attribute declares is the module's own.
        var providers = ModuleRegistry.Providers;
        for (var i = 0; i < providers.Count; i++)
        {
            if (!string.Equals(providers[i].Id.Value, ModuleRegistry.ProviderIds[i], StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"provider {providers[i].GetType().Name} is declared as '{ModuleRegistry.ProviderIds[i]}' but says it is '{providers[i].Id}'");
            }
        }

        ProviderCatalog.Install(providers);
    }
}
