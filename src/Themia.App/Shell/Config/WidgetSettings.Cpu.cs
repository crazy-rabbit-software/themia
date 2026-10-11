using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

internal static partial class WidgetSettings
{
    /// <summary>[S-1140] [S-1762] The processor meter shows its reading and its history only: the rows every meter has.</summary>
    public static void Cpu(List<FrameworkElement> rows, CpuConfig cpu, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(save);

        MeterRows(rows, cpu, save);
    }
}
