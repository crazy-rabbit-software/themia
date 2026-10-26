using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Scratchpad.Tests;

/// <summary>[S-1501] The scratchpad's options on its properties menu: the pen's colour and size, Show tools; nothing that clears.</summary>
public sealed class ScratchpadMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(ScratchpadConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        ScratchpadMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S1501_appearance_then_pen_colour_and_size_then_show_tools_apart()
    {
        var lines = Build(new ScratchpadConfig());

        Assert.Equal(
            [ScratchpadStr.Scratchpad_PenColorLabel.Text(), ScratchpadStr.Scratchpad_PenSizeLabel.Text(), "|", ScratchpadStr.WidgetsC_Scratchpad_Settings_ShowTools_Label.Text()],
            lines.Lines.Select(l => l.IsSeparator ? "|" : l.Text));
        Assert.DoesNotContain(lines.Lines, l => l.Danger);
    }

    [Fact]
    public void S1501_the_pen_colour_offers_the_six_inks_and_no_default()
    {
        var colour = Build(new ScratchpadConfig()).Lines[0].Colour!;

        Assert.Null(colour.Options.DefaultLabel);
        Assert.Equal(InkPalette.Inks.Select(i => i.Hex), colour.Options.Presets.Select(p => p.Hex));
    }

    [Fact]
    public void S1501_an_ink_picked_is_the_pen_colour_and_saves_once()
    {
        var config = new ScratchpadConfig();
        var ink = InkPalette.Inks[^1].Hex;
        Build(config).Lines[0].Colour!.Choose(ink);

        Assert.Equal(ink, config.PenColor, ignoreCase: true);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1501_the_pen_size_is_typed_from_1_to_24_px()
    {
        var config = new ScratchpadConfig();
        var size = Build(config).Lines[1];
        Assert.Equal("3 px", size.Value);

        size.Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.NotNull(ask.Refuse("0"));
        Assert.NotNull(ask.Refuse("25"));

        ask.Done("8");
        Assert.Equal(8, config.PenWidth);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1501_show_tools_turns_them_off_and_saves_once()
    {
        var config = new ScratchpadConfig { ShowTools = true };
        Build(config).Lines.Single(l => l.Text == ScratchpadStr.WidgetsC_Scratchpad_Settings_ShowTools_Label.Text()).Action!();

        Assert.False(config.ShowTools);
        Assert.Equal(1, _saves);
    }
}
