using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6030] The time widgets' styles, over every widget of the build (they are module projects, which
/// only this host has). The rest of the time styles' tests are Core's TimeStylesTests and the
/// modules' own.
/// </summary>
public sealed class TimeStylesHostTests
{
    [Theory]
    [InlineData("countdown", "text,ring,flip")]
    [InlineData("pomodoro", "ring,bar,digits")]
    [InlineData("sun", "arc,dayBar,times")]
    [InlineData("moon", "disc,phaseMonth")]
    [InlineData("clock", "digital,analog,stacked,flip,word,minimal,date")]
    public void S6030_each_kind_offers_its_styles_in_order_the_old_look_first(string kindId, string names)
    {
        var kind = new WidgetId(kindId);
        Assert.Equal(names, string.Join(",", WidgetStyles.For(kind).Select(s => s.Name)));
    }
}
