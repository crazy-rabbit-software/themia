using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Stocks.Tests;

/// <summary>[S-2778] [S-1307] [S-2776] The Stocks widget's options on its properties menu: the symbol as a search, the holding checked as typed.</summary>
public sealed class StocksMenuTests
{
    private static readonly SymbolHit Microsoft = new("MSFT", "Microsoft Corporation", QuoteMarket.Listed, AssetClass.Equity, "NASDAQ");

    private int _saves;

    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(StocksConfig config, params SymbolHit[] hits)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        StocksMenu.Lines(lines, config, (_, _) => Task.FromResult<IReadOnlyList<SymbolHit>>(hits));
        return lines;
    }

    [Fact]
    public void S2778_the_chart_offers_its_markers_under_style_the_number_nothing()
    {
        Assert.Equal([StocksStr.Stocks_ShowMinMax.Text()], Build(new StocksConfig { Style = StocksStyle.Chart }).StyleLines.Select(l => l.Text));
        Assert.Empty(Build(new StocksConfig { Style = StocksStyle.Number }).StyleLines);
    }

    [Fact]
    public void S2778_a_listed_symbol_over_a_day_has_every_line_after_appearance()
    {
        var lines = Build(new StocksConfig { Symbol = "MSFT", Market = QuoteMarket.Listed, Range = QuoteRange.Day });

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [
                StocksStr.Stocks_Tab_Symbols.Text(),
                StocksStr.Stocks_DimWhenClosed.Text(),
                StocksStr.Stocks_ExtendedHours.Text(),
                StocksStr.WidgetsC_Stocks_Settings_Holding_Label.Text(),
                StocksStr.Stocks_Timeframe.Text(),
                StocksStr.Stocks_UpColor.Text(),
                StocksStr.Stocks_DownColor.Text(),
            ],
            lines.Lines.Select(l => l.Text));
        Assert.Equal("MSFT", lines.Lines[0].Search!.Value);
    }

    [Fact]
    public void S2778_a_coin_has_neither_the_closed_market_nor_the_extended_hours()
    {
        var texts = Build(new StocksConfig { Symbol = "bitcoin", Market = QuoteMarket.Crypto }).Lines.Select(l => l.Text).ToList();

        Assert.DoesNotContain(StocksStr.Stocks_DimWhenClosed.Text(), texts);
        Assert.DoesNotContain(StocksStr.Stocks_ExtendedHours.Text(), texts);
    }

    [Fact]
    public async Task S1307_the_typed_symbol_comes_first_then_the_hits_and_a_pick_sets_symbol_market_and_name()
    {
        var config = new StocksConfig();
        var search = Build(config, Microsoft).Lines[0].Search!;

        var answer = await search.Find("msft", CancellationToken.None);
        Assert.Equal([StocksStr.Stocks_Menu_UseTyped.Format(("symbol", "msft")), StocksMenu.Describe(Microsoft)], answer.Lines.Select(l => l.Text));

        answer.Lines[1].Action!();
        Assert.Equal("MSFT", config.Symbol);
        Assert.Equal("Microsoft Corporation", config.DisplayName);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public async Task S1307_a_typed_symbol_is_a_listed_one_and_text_no_symbol_has_is_refused()
    {
        var config = new StocksConfig();
        var search = Build(config).Lines[0].Search!;

        (await search.Find("BTC-USD", CancellationToken.None)).Lines[0].Action!();
        Assert.Equal("BTC-USD", config.Symbol);
        Assert.Equal(QuoteMarket.Listed, config.Market);

        var refused = await search.Find("a/b?c", CancellationToken.None);
        Assert.Empty(refused.Lines);
        Assert.Equal(StocksStr.WidgetsC_Stocks_Settings_SymbolRefused.Text(), refused.Note);
    }

    [Fact]
    public void S2776_a_holding_is_refused_unless_it_is_an_amount_and_empty_clears_it()
    {
        Assert.NotNull(StocksMenu.HoldingRefusal("lots"));
        Assert.NotNull(StocksMenu.HoldingRefusal("-3"));
        Assert.Null(StocksMenu.HoldingRefusal("12"));
        Assert.Null(StocksMenu.HoldingRefusal(string.Empty));

        var config = new StocksConfig { Symbol = "MSFT" };
        Build(config).Lines.Single(l => l.Text == StocksStr.WidgetsC_Stocks_Settings_Holding_Label.Text()).Action!();
        _asked!.Done("12");
        Assert.Equal(12, config.SharesOwned);
        Assert.Equal(1, _saves);

        Build(config).Lines.Single(l => l.Text == StocksStr.WidgetsC_Stocks_Settings_Holding_Label.Text()).Action!();
        _asked!.Done(string.Empty);
        Assert.Null(config.SharesOwned);
        Assert.Equal(2, _saves);
    }
}
