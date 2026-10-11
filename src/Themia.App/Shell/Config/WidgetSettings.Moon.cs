using Microsoft.UI.Xaml;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3100] The Moon rows: the place alone. The phase is the same everywhere; the rise and set, and
/// which way round the lit side is drawn, are the place's.
/// </summary>
internal static partial class WidgetSettings
{
    public static void Moon(List<FrameworkElement> rows, MoonConfig config, Action save) => SkyPlace(rows, config, save);
}
