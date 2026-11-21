using System.Reflection;
using System.Runtime.CompilerServices;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6902] The test host's registry, installed before any test runs as the app installs its own:
/// every module project's widget and provider from the generated <see cref="ModuleRegistry"/>
/// (Core's providers among them), so the state documents here hold every widget of the build.
/// </summary>
internal static class HostCatalog
{
    /// <summary>The module projects' widgets, in the registry's order.</summary>
    public static IReadOnlyList<WidgetDescriptor> ModuleWidgets { get; } = [.. ModuleRegistry.Widgets.Select(m => m.Descriptor)];

#pragma warning disable CA2255 // A test assembly's one global setup: the registry every test reads.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Install()
    {
        ModuleCatalog.Install(ModuleWidgets);
        ProviderCatalog.Install(ModuleRegistry.Providers);

        // [S-14961] The network kinds' Adapter line lists the sample PC's adapters, unless a test changes them (TestAdapters).
        AdapterList.Install(() => TestAdapters.Now, TestAdapters.Watch);
    }
}

/// <summary>[S-14962] The test host's network adapters: the sample PC's, which a test may change and announce.</summary>
internal static class TestAdapters
{
    private static readonly Lock Gate = new();
    private static readonly List<Action> Watching = [];

    /// <summary>What <c>AdapterList.Now()</c> answers.</summary>
    public static IReadOnlyList<Themia.Core.Widgets.NetworkAdapterEntry> Now { get; private set; } = SamplePc.Adapters;

    /// <summary>Windows' change notice, as the host's: held until disposed.</summary>
    public static IDisposable Watch(Action changed)
    {
        lock (Gate)
        {
            Watching.Add(changed);
        }

        return new Unwatch(changed);
    }

    /// <summary>The adapters become <paramref name="now"/> (null: the sample PC's again) and every watch is told.</summary>
    public static void Change(IReadOnlyList<Themia.Core.Widgets.NetworkAdapterEntry>? now)
    {
        Now = now ?? SamplePc.Adapters;
        Action[] told;
        lock (Gate)
        {
            told = [.. Watching];
        }

        foreach (var changed in told)
        {
            changed();
        }
    }

    private sealed class Unwatch(Action changed) : IDisposable
    {
        public void Dispose()
        {
            lock (Gate)
            {
                Watching.Remove(changed);
            }
        }
    }
}
