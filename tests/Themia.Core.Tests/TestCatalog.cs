using System.Reflection;
using System.Runtime.CompilerServices;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6902] The tests' host: installs the widget registry before any test runs, as the app does
/// first thing. The app installs the generated registry of its modules; Core's tests cannot see the
/// app, so they install the same descriptors from where they are declared (each built-in widget's
/// config, until its module moves to its own project), and <c>WidgetRegistryTests</c> checks that
/// the app declares a module for exactly these.
/// </summary>
internal static class TestCatalog
{
    /// <summary>Every <c>public static WidgetDescriptor Descriptor</c> on a concrete config in Core (the assembly of <see cref="AppState"/>).</summary>
    public static IReadOnlyList<(Type Config, WidgetDescriptor Descriptor)> Declared { get; } =
    [
        .. typeof(AppState).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(WidgetConfig)) && !t.IsAbstract)
            .Select(t => (t, t.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as WidgetDescriptor))
            .Where(p => p.Item2 is not null)
            .Select(p => (p.t, p.Item2!))
            .OrderBy(p => p.Item2.Id.Value, StringComparer.Ordinal),
    ];

#pragma warning disable CA2255 // A test assembly's one global setup: the registry every test reads.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Install() => ModuleCatalog.Install(Declared.Select(d => d.Descriptor));
}
