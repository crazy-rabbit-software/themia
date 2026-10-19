using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.App.Services;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>The adapter line the GPU meter's and the GPU details' properties menus share.</summary>
public static class GpuAdapterLine
{
    /// <summary>
    /// [S-433] [S-2431] Which adapter, by name: a choice over <paramref name="adapters"/> (the host's
    /// list, <c>GpuAdapters.List()</c>), offered only with two or more (null otherwise). A chosen
    /// adapter that is gone shows as the first.
    /// </summary>
    public static ItemMenuEntry? Line(WidgetMenuLines menu, int adapter, Action<int> choose, IReadOnlyList<GpuAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(choose);
        ArgumentNullException.ThrowIfNull(adapters);

        if (adapters.Count < 2)
        {
            return null;
        }

        var current = adapter >= 0 && adapter < adapters.Count ? adapter : 0;
        return menu.Choice(Glyphs.Gpu, Str.Details_Settings_Gpu_Adapter_Label.Text(),
            [.. adapters.Select(a => (a.Index, a.Name))], current, choose);
    }
}
