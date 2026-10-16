using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

internal static partial class WidgetSettings
{
    /// <summary>[S-1140] [S-1762] The processor meter's page: its heading, under which goes the Style row ([S-6311]), then the rows every meter has.</summary>
    public static void Cpu(List<FrameworkElement> rows, CpuConfig cpu, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(save);

        // [S-6010] The kind's heading, so the shared Style row is the first row under it.
        rows.Add(FluentLayout.Header(Str.WidgetsA_Catalog_Cpu_Label.Text()));

        MeterRows(rows, cpu, save);
    }
}
