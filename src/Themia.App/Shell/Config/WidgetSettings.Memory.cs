using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

internal static partial class WidgetSettings
{
    /// <summary>[S-1130] [S-1762] The memory meter shows its reading and its history only: the rows every meter has.</summary>
    public static void Memory(List<FrameworkElement> rows, MemoryConfig memory, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(save);

        MeterRows(rows, memory, save);
    }
}
