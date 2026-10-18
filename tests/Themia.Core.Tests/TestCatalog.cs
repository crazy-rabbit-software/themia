using System.Reflection;
using System.Runtime.CompilerServices;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6902] The tests' host: installs the registry before any test runs, as the app does first
/// thing. Every widget is a module project Core's tests cannot see, so the registry holds none, and
/// the providers Core declares.
/// </summary>
internal static class TestCatalog
{
#pragma warning disable CA2255 // A test assembly's one global setup: the registry every test reads.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Install()
    {
        ModuleCatalog.Install([]);
        ProviderCatalog.Install(Providers);
    }

    /// <summary>[S-6910] Every provider Core declares with <c>[assembly: ThemiaProvider]</c>, as the generated registry holds them.</summary>
    public static IReadOnlyList<IProviderModule> Providers { get; } =
    [
        .. typeof(AppState).Assembly.GetCustomAttributes<ThemiaProviderAttribute>()
            .OrderBy(a => a.Id, StringComparer.Ordinal)
            .Select(a => (IProviderModule)Activator.CreateInstance(a.Module)!),
    ];
}
