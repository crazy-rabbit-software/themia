using Themia.App.Services;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>[S-2431] The GPU details' options, all on its properties menu ([S-3719]): Appearance, then the adapter (offered with two or more). It has no settings page.</summary>
internal static class GpuDetailsMenu
{
    /// <param name="adapters">The host's adapters (<c>GpuAdapters.List()</c>).</param>
    public static void Lines(WidgetMenuLines menu, GpuDetailsConfig config, IReadOnlyList<GpuAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        if (GpuAdapterLine.Line(menu, config.Adapter, value => config.Adapter = value, adapters) is { } adapter)
        {
            menu.Lines.Add(adapter);
        }
    }
}
