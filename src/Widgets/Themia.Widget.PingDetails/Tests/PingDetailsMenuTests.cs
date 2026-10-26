using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.PingDetails.Tests;

/// <summary>[S-2426] [S-2427] The ping details' options on its properties menu: Appearance, the host, the window.</summary>
public sealed class PingDetailsMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(PingDetailsConfig config)
    {
        var menu = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        PingDetailsMenu.Lines(menu, config);
        return menu;
    }

    [Fact]
    public void S2426_the_first_level_is_appearance_the_host_then_the_window()
    {
        var menu = Build(new PingDetailsConfig());

        Assert.Equal(
            [Str.WidgetsB_Settings_Ping_Host_Label.Text(), PingDetailsStr.Details_Settings_Ping_Window_Label.Text()],
            menu.Lines.Select(l => l.Text));
        Assert.Equal(HostAddress.DefaultPingHost, menu.Lines[0].Value);
        Assert.Empty(menu.StyleLines);
    }

    [Fact]
    public void S2426_the_host_is_checked_as_the_meters_is_and_stored_once()
    {
        var config = new PingDetailsConfig();
        Build(config).Lines[0].Action!();

        Assert.NotNull(_asked!.Refuse("-x"));
        _asked.Done("router.local");
        Assert.Equal("router.local", config.Host);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S2427_the_window_offers_ten_to_three_hundred_and_holds_the_default_for_one_not_offered()
    {
        var window = Build(new PingDetailsConfig { Window = 45 }).Lines[1];

        Assert.Equal(5, window.Children!.Count);
        Assert.Equal(PingDetailsStr.Details_Settings_Ping_Window_Probes.Format(("count", LocaleFormat.Integer(60))), Assert.Single(window.Children, c => c.Checked == true).Text);

        var config = new PingDetailsConfig();
        Build(config).Lines[1].Children![4].Action!();
        Assert.Equal(300, config.Window);
        Assert.Equal(1, _saves);
    }
}
