using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.NetworkDetails.Tests;

/// <summary>[S-2417] The network details' options on its properties menu: Appearance, the public address switch and whom it asks.</summary>
public sealed class NetworkDetailsMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(NetworkDetailsConfig config)
    {
        var menu = new WidgetMenuLines(() => _saves++, _ => { });
        NetworkDetailsMenu.Lines(menu, config);
        return menu;
    }

    [Fact]
    public void S2417_the_switch_is_off_by_default_and_says_whom_it_asks()
    {
        var menu = Build(new NetworkDetailsConfig());

        Assert.Equal(2, menu.Lines.Count);
        Assert.Equal(false, menu.Lines[0].Checked);
        Assert.True(menu.Lines[1].Note);
        Assert.Contains("api.ipify.org", menu.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void S2417_a_click_turns_it_on_and_stores_once()
    {
        var config = new NetworkDetailsConfig();
        Build(config).Lines[0].Action!();

        Assert.True(config.ShowPublicAddress);
        Assert.Equal(1, _saves);
    }
}
