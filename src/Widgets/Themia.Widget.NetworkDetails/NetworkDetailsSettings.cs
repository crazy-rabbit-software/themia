using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.NetworkDetails;

namespace Themia.App.Shell.Config;

/// <summary>[S-2417] The network details' settings page: whether to ask for the public address.</summary>
internal static class NetworkDetailsSettings
{
    public static void Rows(List<FrameworkElement> rows, NetworkDetailsConfig network, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(NetworkDetailsStr.Details_Catalog_Network_Label.Text()));
        rows.Add(FluentLayout.Row(Glyphs.Globe, NetworkDetailsStr.Details_Settings_Network_Public_Label.Text(), NetworkDetailsStr.Details_Settings_Network_Public_Desc.Format(("minutes", (long)NetworkDetailRules.PublicAddressLifetime.TotalMinutes)),
            FluentLayout.Toggle(network.ShowPublicAddress, on => { network.ShowPublicAddress = on; save(); }),
            "ip", "public ip", "address"));
    }
}
