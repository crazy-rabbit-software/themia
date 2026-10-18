using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

internal static class CpuSettings
{
    /// <summary>[S-1140] [S-1762] The processor meter's page: the rows every meter has (its style is on the properties panel, [S-6311]).</summary>
    public static void Rows(List<FrameworkElement> rows, CpuConfig cpu, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(save);

        MeterSettingsRows.Rows(rows, cpu, save);
    }
}
