using Themia.Core.Localization;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.NetworkFacts.Tests;

/// <summary>[S-2417] The network details' options on its properties menu: Appearance, the public address switch and whom it asks.</summary>
public sealed class NetworkFactsMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(NetworkFactsConfig config)
    {
        var menu = new WidgetMenuLines(() => _saves++, _ => { });
        NetworkFactsMenu.Lines(menu, config);
        return menu;
    }

    [Fact]
    public void S2417_the_pick_comes_first_and_the_menu_says_whom_the_public_address_asks()
    {
        var menu = Build(new NetworkFactsConfig());

        Assert.Equal(2, menu.Lines.Count);
        Assert.Equal(Str.Details_Facts.Text(), menu.Lines[0].Text);
        Assert.True(menu.Lines[1].Note);
        Assert.Contains("api.ipify.org", menu.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void S2417_picking_the_public_address_asks_for_it_and_stores_once()
    {
        var config = new NetworkFactsConfig();
        var add = Build(config).Lines[0].Children!.Single(c => c.Text == Str.Details_Network_PublicAddress.Text());
        add.Action!();

        Assert.True(config.ShowsPublicAddress);
        Assert.Equal(1, _saves);
    }
}
