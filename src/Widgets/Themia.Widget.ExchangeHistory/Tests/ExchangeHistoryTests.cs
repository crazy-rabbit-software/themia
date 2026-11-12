using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.ExchangeHistory.Tests;

/// <summary>[S-11155]..[S-11157] The rate's history: its pair, its size and its menu.</summary>
public sealed class ExchangeHistoryTests
{
    private static readonly WidgetDescriptor Kind = new ExchangeHistoryModule().Descriptor;

    [Fact]
    public void S11156_the_chart_starts_headerless_200_to_480_wide_its_band_48_to_200_in_the_inset()
    {
        var sizing = Kind.Sizing(new ExchangeHistoryConfig());

        Assert.True(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));
        Assert.Equal(new SizeDip(280, 24 + 72), sizing.Natural);
        Assert.Equal((200.0, 480.0), (sizing.Min.Width, sizing.Max.Width));
        Assert.Equal((24 + 48.0, 24 + 200.0), (sizing.Min.Height, sizing.Max.Height));
    }

    [Fact]
    public void S11155_its_pair_is_the_rates_and_a_copy_keeps_it()
    {
        var config = new ExchangeHistoryConfig { BaseCurrency = "chf", QuoteCurrency = "XYZ" };
        Assert.NotEmpty(config.Repair());
        Assert.Equal(("CHF", string.Empty), (config.BaseCurrency, config.QuoteCurrency));
        Assert.Equal("CHF", ((ExchangeHistoryConfig)config.Clone()).BaseCurrency);
    }

    [Fact]
    public void S11157_the_menu_is_the_pair_and_the_source()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        new ExchangeHistoryModule().MenuLines(menu, new ExchangeHistoryConfig());

        Assert.Equal(
            [Str.Online_Rates_Settings_Base_Label.Text(), Str.Online_Rates_Settings_Quote_Label.Text()],
            menu.Lines.Where(l => l.Search is not null).Select(l => l.Text));
        Assert.True(menu.Lines[^1].Note);
    }
}
