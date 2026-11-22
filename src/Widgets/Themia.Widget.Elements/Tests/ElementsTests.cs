using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Elements.Tests;

/// <summary>[S-10100]..[S-10103] The design elements: own text and a divider (the Icon widget, [S-15010], is one too).</summary>
public sealed class ElementsTests
{
    [Theory]
    [InlineData("text")]
    [InlineData("divider")]
    public void S10100_every_element_is_a_design_element_and_a_copy_is_a_config_of_its_own(string kind)
    {
        var config = WidgetConfig.CreateDefault(new WidgetId(kind));

        Assert.IsAssignableFrom<IDesignElement>(config);
        Assert.NotSame(config, config.Clone());
        Assert.Equal(config.GetType(), config.Clone().GetType());
    }

    [Fact]
    public void S10101_own_text_is_one_line_kept_through_a_copy()
    {
        var text = new TextElementConfig { Text = "Zurich\nweek" };

        Assert.NotEmpty(text.Repair());
        Assert.Equal("Zurich week", text.Text);
        Assert.Equal("Zurich week", ((TextElementConfig)text.Clone()).Text);
        Assert.Equal("Rain", TextElementConfig.Clean("  Rain \r\n"));
        Assert.Empty(new TextElementConfig { Text = "Rain" }.Repair());
    }

    [Fact]
    public void S10102_the_icon_widget_is_a_design_element_too()
        => Assert.IsAssignableFrom<IDesignElement>(WidgetConfig.CreateDefault(new WidgetId("icon")));

    [Fact]
    public void S10103_a_divider_is_one_cell_thin_and_as_long_as_it_is_drawn()
    {
        var sizing = SizeFit.Of(new DividerElementConfig());

        Assert.Equal(WidgetSizing.Floor, sizing.Min.Height);
        Assert.Equal(WidgetSizing.Floor, sizing.Natural.Height);
        Assert.True(sizing.Natural.Width > sizing.Natural.Height);
        Assert.True(sizing.Max.Height > sizing.Natural.Width); // it may run down as well as across
    }
}
