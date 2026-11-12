using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the screen-time app list shows ([S-11510]): its config's, at every size.</summary>
/// <param name="Rows">The rows the config asks for.</param>
/// <param name="Lines">[S-6712] The lines every row shows: the second holds the share and a window's average.</param>
/// <param name="Lead">The lead's size: the app's icon, 16 on a one-line row, 32 on a two-line one.</param>
public readonly record struct ScreenTimeAppsPlan(int Rows, int Lines, double Lead);

/// <summary>[S-11510] The app list on the family's rows ([S-6711], [S-6712]), from its config alone.</summary>
public static class ScreenTimeAppsLayout
{
    /// <summary>[S-11510] The list as its config says: its rows, its lines (two only with figures for the second), the icon by the lines.</summary>
    public static ScreenTimeAppsPlan Apps(ScreenTimeAppsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var shown = config.RowsShown;
        return new ScreenTimeAppsPlan(shown.Rows, shown.Lines, shown.Lines > 1 ? FamilyShape.IconLarge : FamilyShape.Icon);
    }
}
