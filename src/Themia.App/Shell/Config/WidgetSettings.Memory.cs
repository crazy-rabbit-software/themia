using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

internal static partial class WidgetSettings
{
    /// <summary>[S-1130] [S-1762] The memory meter's page: its heading, under which goes the Style row ([S-6315]), then the rows every meter has.</summary>
    public static void Memory(List<FrameworkElement> rows, MemoryConfig memory, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(save);

        // [S-6010] The kind's heading, so the shared Style row is the first row under it.
        rows.Add(FluentLayout.Header(Str.WidgetsA_Catalog_Memory_Label.Text()));

        MeterRows(rows, memory, save);
    }
}
