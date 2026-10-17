using System.Reflection;
using System.Runtime.CompilerServices;
using Themia.Core.Model;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6902] The test host's registry, installed before any test runs as the app installs its own:
/// every module project's widget and provider from the generated <see cref="ModuleRegistry"/>
/// (Core's providers among them), and the widgets still declared in Core (their descriptors on their
/// configs until they move, as <c>Themia.Core.Tests</c> installs them), so the state documents here
/// hold every widget of the build.
/// </summary>
internal static class HostCatalog
{
    /// <summary>The widgets declared in Core, found as Core's own tests find them.</summary>
    public static IReadOnlyList<WidgetDescriptor> CoreWidgets { get; } =
    [
        .. typeof(AppState).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(WidgetConfig)) && !t.IsAbstract)
            .Select(t => t.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as WidgetDescriptor)
            .OfType<WidgetDescriptor>(),
    ];

    /// <summary>The module projects' widgets, in the registry's order.</summary>
    public static IReadOnlyList<WidgetDescriptor> ModuleWidgets { get; } = [.. ModuleRegistry.Widgets.Select(m => m.Descriptor)];

#pragma warning disable CA2255 // A test assembly's one global setup: the registry every test reads.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Install()
    {
        ModuleCatalog.Install(CoreWidgets.Concat(ModuleWidgets).OrderBy(w => w.Id.Value, StringComparer.Ordinal));
        ProviderCatalog.Install(ModuleRegistry.Providers);
    }
}
