using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Stock.Tests;

/// <summary>[S-2778] [S-1307] [S-2776] The Stocks widget's options on its properties menu: the symbol as a search, the holding checked as typed.</summary>
public sealed class StockMenuTests
{
    private static readonly SymbolHit Microsoft = new("MSFT", "Microsoft Corporation", QuoteMarket.Listed, AssetClass.Equity, "NASDAQ");

    private int _saves;

    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(StockConfig config, params SymbolHit[] hits)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        StockMenu.Lines(lines, config, (_, _) => Task.FromResult<IReadOnlyList<SymbolHit>>(hits));
        return lines;
    }

    [Fact]
    public void S2778_nothing_stands_under_style()
        => Assert.Empty(Build(new StockConfig()).StyleLines);

    [Fact]
    public void S2778_a_listed_symbol_over_a_day_has_every_line_after_appearance_in_three_groups()
    {
        var lines = Build(new StockConfig { Symbol = "MSFT", Market = QuoteMarket.Listed, Range = QuoteRange.Day });

        Assert.Equal(
            [
                Str.Stocks_Tab_Symbols.Text(),
                StockStr.WidgetsC_Stocks_Settings_Holding_Label.Text(),
                string.Empty,
                Str.Stocks_Timeframe.Text(),
                Str.Stocks_ExtendedHours.Text(),
                StockStr.Stocks_DimWhenClosed.Text(),
                string.Empty,
                Str.EditBar_Link_UpdateInterval.Text(),
            ],
            lines.Lines.Select(l => l.IsSeparator ? string.Empty : l.Text));
        Assert.Equal(Str.SettingsMenu_OnlineUpdates.Text(), lines.Lines[^1].LinkTo);
        Assert.Equal("MSFT", lines.Lines[0].Search!.Value);
    }

    [Fact]
    public void S2778_a_coin_has_neither_the_closed_market_nor_the_extended_hours()
    {
        var texts = Build(new StockConfig { Symbol = "bitcoin", Market = QuoteMarket.Crypto }).Lines.Select(l => l.Text).ToList();

        Assert.DoesNotContain(StockStr.Stocks_DimWhenClosed.Text(), texts);
        Assert.DoesNotContain(Str.Stocks_ExtendedHours.Text(), texts);
    }

    [Theory]
    [InlineData(QuoteRange.Month)]
    [InlineData(QuoteRange.Year)]
    public void S2778_extended_hours_are_offered_over_one_or_five_days_only(QuoteRange range)
    {
        Assert.Contains(Build(new StockConfig { Symbol = "MSFT", Range = QuoteRange.FiveDays }).Lines, l => l.Text == Str.Stocks_ExtendedHours.Text());
        Assert.DoesNotContain(Build(new StockConfig { Symbol = "MSFT", Range = range }).Lines, l => l.Text == Str.Stocks_ExtendedHours.Text());
    }

    [Fact]
    public async Task S1307_the_typed_symbol_comes_first_then_the_hits_and_a_pick_sets_symbol_market_and_name()
    {
        var config = new StockConfig();
        var search = Build(config, Microsoft).Lines[0].Search!;

        var answer = await search.Find("msft", CancellationToken.None);
        Assert.Equal([Str.Stocks_Menu_UseTyped.Format(("symbol", "msft")), StockQuoteLines.Describe(Microsoft)], answer.Lines.Select(l => l.Text));

        answer.Lines[1].Action!();
        Assert.Equal("MSFT", config.Symbol);
        Assert.Equal("Microsoft Corporation", config.DisplayName);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public async Task S1307_issue_187_a_search_no_service_answered_says_why_and_still_offers_the_typed_symbol()
    {
        var config = new StockConfig();
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        StockMenu.Lines(lines, config, (_, _) => throw new SymbolSearchFailedException(FetchStatus.TimedOut));
        var search = lines.Lines[0].Search!;

        var typed = await search.Find("600519.SS", CancellationToken.None);
        Assert.Null(typed.Note);
        Assert.Equal([Str.Stocks_Menu_UseTyped.Format(("symbol", "600519.SS")), FetchState.Message(FetchStatus.TimedOut)], typed.Lines.Select(l => l.Text));
        Assert.True(typed.Lines[1].Note);

        var named = await search.Find("Kweichow Moutai", CancellationToken.None);
        Assert.Equal([FetchState.Message(FetchStatus.TimedOut)], named.Lines.Select(l => l.Text));
    }

    [Fact]
    public async Task S1307_a_typed_symbol_is_a_listed_one_and_text_no_symbol_has_is_refused()
    {
        var config = new StockConfig();
        var search = Build(config).Lines[0].Search!;

        (await search.Find("BTC-USD", CancellationToken.None)).Lines[0].Action!();
        Assert.Equal("BTC-USD", config.Symbol);
        Assert.Equal(QuoteMarket.Listed, config.Market);

        var refused = await search.Find("a/b?c", CancellationToken.None);
        Assert.Empty(refused.Lines);
        Assert.Equal(Str.WidgetsC_Stocks_Settings_SymbolRefused.Text(), refused.Note);
    }

    [Fact]
    public void S2776_a_holding_is_refused_unless_it_is_an_amount_and_empty_clears_it()
    {
        Assert.NotNull(StockMenu.HoldingRefusal("lots"));
        Assert.NotNull(StockMenu.HoldingRefusal("-3"));
        Assert.Null(StockMenu.HoldingRefusal("12"));
        Assert.Null(StockMenu.HoldingRefusal(string.Empty));

        var config = new StockConfig { Symbol = "MSFT" };
        Build(config).Lines.Single(l => l.Text == StockStr.WidgetsC_Stocks_Settings_Holding_Label.Text()).Action!();
        _asked!.Done("12");
        Assert.Equal(12, config.SharesOwned);
        Assert.Equal(1, _saves);

        Build(config).Lines.Single(l => l.Text == StockStr.WidgetsC_Stocks_Settings_Holding_Label.Text()).Action!();
        _asked!.Done(string.Empty);
        Assert.Null(config.SharesOwned);
        Assert.Equal(2, _saves);
    }
}
