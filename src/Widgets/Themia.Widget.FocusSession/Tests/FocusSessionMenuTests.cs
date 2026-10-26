using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.FocusSession.Tests;

/// <summary>[S-4428] The focus session's options on its properties menu: Appearance, the length typed, do not disturb.</summary>
public sealed class FocusSessionMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(FocusSessionConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        FocusSessionMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S4428_appearance_then_length_then_do_not_disturb()
    {
        var lines = Build(new FocusSessionConfig());

        Assert.Equal([FocusSessionStr.Panels_Focus_LengthLabel.Text(), FocusSessionStr.Panels_Focus_QuietLabel.Text()], lines.Lines.Select(l => l.Text));
        Assert.Empty(lines.StyleLines);
        Assert.Equal(FocusSessionMenu.Minutes(30), lines.Lines[0].Value);
    }

    [Fact]
    public void S4428_the_length_is_typed_in_five_minute_steps_inside_its_range()
    {
        var config = new FocusSessionConfig();
        Build(config).Lines[0].Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);

        Assert.NotNull(ask.Refuse("3"));
        Assert.NotNull(ask.Refuse("241"));
        Assert.NotNull(ask.Refuse("soon"));
        Assert.Null(ask.Refuse("45"));

        ask.Done("47");
        Assert.Equal(45, config.Minutes);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S4428_the_current_length_typed_again_saves_nothing()
    {
        Build(new FocusSessionConfig { Minutes = 30 }).Lines[0].Action!();
        _asked!.Done("30");

        Assert.Equal(0, _saves);
    }

    [Fact]
    public void S4428_the_do_not_disturb_switch_turns_it_off_and_saves_once()
    {
        var config = new FocusSessionConfig { DoNotDisturb = true };
        Build(config).Lines[1].Action!();

        Assert.False(config.DoNotDisturb);
        Assert.Equal(1, _saves);
    }
}
