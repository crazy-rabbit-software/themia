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
    public static FrameworkElement CreateView(WidgetId kind) => Module(kind).CreateView();

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
