using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Brightness.Tests;

/// <summary>[S-4446] The brightness widget's options on its properties menu.</summary>
public sealed class BrightnessMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(BrightnessConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        BrightnessMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S4446_appearance_then_the_screens_it_dims_as_a_choice()
    {
        var lines = Build(new BrightnessConfig());

        Assert.Equal(0, lines.CardAt);
        var screens = Assert.Single(lines.Lines);
        Assert.Equal(BrightnessStr.Panels_Brightness_ScreensLabel.Text(), screens.Text);
        Assert.Equal([true, false], screens.Children!.Select(c => c.Checked == true));
    }

    [Fact]
    public void S4446_picking_the_own_screen_stores_it_once_and_the_current_one_nothing()
    {
        var config = new BrightnessConfig();
        var screens = Build(config).Lines[0].Children!;

        screens[0].Action!();
        Assert.Equal(0, _saves);

        screens[1].Action!();
        Assert.Equal(BrightnessScreens.ThisDisplay, config.Screens);
        Assert.Equal(1, _saves);
    }
}
