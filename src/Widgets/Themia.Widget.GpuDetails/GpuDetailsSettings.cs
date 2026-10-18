using Microsoft.UI.Xaml;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>[S-2431] The GPU details' settings page: the adapter (the shared row, offered with two or more).</summary>
internal static class GpuDetailsSettings
{
    public static void Rows(List<FrameworkElement> rows, GpuDetailsConfig gpu, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(save);

        GpuAdapterRow.Add(rows, gpu.Adapter, value => gpu.Adapter = value, save);
    }
}
