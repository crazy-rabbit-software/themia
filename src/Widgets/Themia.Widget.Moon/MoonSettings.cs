using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3100] The Moon rows: the place alone. The phase is the same everywhere; the rise and set, and
/// which way round the lit side is drawn, are the place's.
/// </summary>
internal static class MoonSettings
{
    public static void Rows(List<FrameworkElement> rows, MoonConfig config, Action save)
        => SkyPlaceRows.Add(rows, config, save, longitude: config.Offers(MoonSetting.Longitude));
}
