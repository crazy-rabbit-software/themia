using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Ping.Tests;

/// <summary>[S-1175] [S-109] The ping meter's options on its properties menu: Appearance, the host (checked as typed), the span under Style.</summary>
public sealed class PingMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(PingConfig config)
    {
        var menu = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        PingMenu.Lines(menu, config);
        return menu;
    }

    [Fact]
    public void S1175_the_first_level_is_appearance_then_the_host_and_the_span_is_under_style()
    {
        var menu = Build(new PingConfig());

        Assert.Equal(0, menu.CardAt);
        var host = Assert.Single(menu.Lines);
        Assert.Equal(Str.WidgetsB_Settings_Ping_Host_Label.Text(), host.Text);
        Assert.Equal(Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text(), Assert.Single(menu.StyleLines).Text);
        Assert.Empty(Build(new PingConfig { Style = PingStyle.Signal }).StyleLines);
    }

    [Fact]
    public void S1175_with_no_host_typed_the_line_says_the_default_resolver()
    {
        Assert.Equal(HostAddress.DefaultPingHost, Assert.Single(Build(new PingConfig()).Lines).Value);
        Assert.Equal("example.com", Assert.Single(Build(new PingConfig { Host = "example.com" }).Lines).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("example.com")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    public void S1175_a_plausible_host_or_none_is_accepted(string typed)
        => Assert.Null(PingHostLine.Refuse(typed));

    [Theory]
    [InlineData("-t example.com")]
    [InlineData("/c calc")]
    [InlineData("exa mple.com")]
    [InlineData(".example.com")]
    public void S1175_what_is_not_a_host_is_refused_as_typed(string typed)
        => Assert.Equal(Str.WidgetsB_Settings_Ping_NotAHost.Text(), PingHostLine.Refuse(typed));

    [Fact]
    public void S1175_a_host_given_is_stored_trimmed_once_and_the_same_host_stores_nothing()
    {
        var config = new PingConfig { Host = "example.com" };
        Assert.Single(Build(config).Lines).Action!();

        Assert.NotNull(_asked);
        Assert.Equal("example.com", _asked.Initial);
        Assert.True(_asked.AllowEmpty);

        _asked.Done("example.com");
        Assert.Equal(0, _saves);

        _asked.Done("  1.1.1.1 ");
        Assert.Equal("1.1.1.1", config.Host);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1175_an_emptied_box_measures_the_default_and_a_refused_one_is_never_stored()
    {
        var config = new PingConfig { Host = "example.com" };
        Assert.Single(Build(config).Lines).Action!();

        _asked!.Done("-bad");
        Assert.Equal("example.com", config.Host);
        Assert.Equal(0, _saves);

        _asked.Done(string.Empty);
        Assert.Equal(string.Empty, config.Host);
        Assert.Equal(1, _saves);
    }
}
