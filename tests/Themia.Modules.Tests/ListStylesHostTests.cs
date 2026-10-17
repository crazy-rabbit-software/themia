using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6400] The list and media widgets' styles, over every widget of the build (some of them are
/// module projects now, which only this host has). The rest of the list styles' tests are Core's
/// ListStylesTests and the modules' own.
/// </summary>
public sealed class ListStylesHostTests
{
    [Theory]
    [InlineData("tasks", "list,compact")]
    [InlineData("shortcuts", "grid,list,details")]
    [InlineData("gallery", "slideshow,grid,scatter,mosaic")]
    [InlineData("email", "list,compact,table")]
    [InlineData("quote", "plain,marks")]
    [InlineData("music", "artwork,cover,text")]
    [InlineData("habitTracker", "dots,heatmap,checklist")]
    public void S6400_each_kind_offers_its_styles_in_order_the_old_default_first(string kindId, string names)
    {
        var kind = new WidgetId(kindId);
        Assert.True(WidgetStyles.Offers(kind));
        Assert.Equal(names, string.Join(",", WidgetStyles.For(kind).Select(s => s.Name)));
        Assert.Equal(WidgetStyles.For(kind)[0].Value, WidgetStyles.Current(WidgetConfig.CreateDefault(kind)));
    }
}
