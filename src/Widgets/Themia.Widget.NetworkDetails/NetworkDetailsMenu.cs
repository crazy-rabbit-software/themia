using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.NetworkDetails;

namespace Themia.App.Shell.Config;

/// <summary>[S-2417] The network details' options, all on its properties menu ([S-3719]): Appearance, then the public address switch and whom it asks. It has no settings page.</summary>
internal static class NetworkDetailsMenu
{
    public static void Lines(WidgetMenuLines menu, NetworkDetailsConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(menu.Toggle(Glyphs.Globe, NetworkDetailsStr.Details_Settings_Network_Public_Label.Text(), config.ShowPublicAddress, on => config.ShowPublicAddress = on));

        // Off by default because it asks a third party: whom, and how often, is said before it is turned on.
        menu.Lines.Add(WidgetMenuLines.Note(NetworkDetailsStr.Details_Settings_Network_Public_Desc.Format(("minutes", (long)NetworkDetailRules.PublicAddressLifetime.TotalMinutes))));
    }
}
