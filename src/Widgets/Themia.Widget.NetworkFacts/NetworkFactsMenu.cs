using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.NetworkFacts;

namespace Themia.App.Shell.Config;

/// <summary>[S-2417] The network facts' options, all on its properties menu ([S-3719]): Appearance, then the pick and whom the public address asks. It has no settings page.</summary>
internal static class NetworkFactsMenu
{
    public static void Lines(WidgetMenuLines menu, NetworkFactsConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // [S-11020] Which facts, in what order ([S-11007]); the public address among them asks a third party.
        menu.Lines.Add(AtomMenuLines.Facts(menu, config));

        // Never a default because it asks a third party: whom, and how often, is said before it is picked.
        menu.Lines.Add(WidgetMenuLines.Note(NetworkFactsStr.Details_Settings_Network_Public_Desc.Format(("minutes", (long)NetworkDetailRules.PublicAddressLifetime.TotalMinutes))));
    }
}
