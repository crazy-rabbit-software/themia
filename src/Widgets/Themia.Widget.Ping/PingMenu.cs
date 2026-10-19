using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Widget.Ping;

namespace Themia.App.Shell.Config;

/// <summary>[S-1175] The ping meter's options, all on its properties menu ([S-3719]): Appearance, then the host ([S-109]); under Style the chart span every meter has ([S-1122]). It has no settings page.</summary>
internal static class PingMenu
{
    public static void Lines(WidgetMenuLines menu, PingConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(PingHostLine.Line(menu, config.Host, value => config.Host = value));
        MeterMenuLines.Lines(menu, config);
    }
}
