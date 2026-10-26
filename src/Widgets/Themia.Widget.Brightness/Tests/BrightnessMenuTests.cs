using Themia.App.Services.Brightness;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Brightness.Tests;

/// <summary>[S-4446] The brightness widget's options on its properties menu.</summary>
public sealed class BrightnessMenuTests
{
    private int _saves;

    private static readonly BrightnessSnapshot Two = new(true, [new(@"\.\DISPLAY1", BrightnessPath.BuiltIn, 50), new(@"\.\DISPLAY2", BrightnessPath.Ddc, 80)]);

    private static readonly BrightnessSnapshot One = new(true, [new(@"\.\DISPLAY1", BrightnessPath.BuiltIn, 50)]);

    private WidgetMenuLines Build(BrightnessConfig config, BrightnessSnapshot? displays = null)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        BrightnessMenu.Lines(lines, config, displays ?? Two);
        return lines;
    }

    [Fact]
    public void S4446_appearance_then_the_screens_it_dims_as_a_choice()
    {
        var lines = Build(new BrightnessConfig());

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

    [Fact]
    public void S4447_one_display_with_a_brightness_has_no_screens_line()
    {
        var lines = Build(new BrightnessConfig(), One);

        Assert.Empty(lines.Lines);
    }

    [Fact]
    public void S4447_the_own_screen_chosen_keeps_the_line_so_it_can_go_back_and_so_does_a_pending_read()
    {
        Assert.Single(Build(new BrightnessConfig { Screens = BrightnessScreens.ThisDisplay }, One).Lines);
        Assert.Single(Build(new BrightnessConfig(), BrightnessSnapshot.Pending).Lines);
    }
}
