using Themia.App.Services;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>[S-1200] The GPU reading's options, all on its properties menu ([S-3719]): Appearance, the adapter (offered with two or more, [S-433]). It has no settings page.</summary>
internal static class GpuMenu
{
    /// <param name="adapters">The host's adapters (<c>GpuAdapters.List()</c>).</param>
    public static void Lines(WidgetMenuLines menu, GpuConfig config, IReadOnlyList<GpuAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        if (GpuAdapterLine.Line(menu, config.Adapter, value => config.Adapter = value, adapters) is { } adapter)
        {
            menu.Lines.Add(adapter);
        }

    }
}
