using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.ExchangeRates.Tests;

/// <summary>[S-5148] The exchange-rate widget's options on its properties menu: the pair as two searches over the published codes.</summary>
public sealed class ExchangeRatesMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(ExchangeRatesConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        ExchangeRatesMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S5148_appearance_then_base_currency_and_the_source()
    {
        var lines = Build(new ExchangeRatesConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [ExchangeRatesStr.Online_Rates_Settings_Base_Label.Text(), ExchangeRatesStr.Online_Rates_Settings_Quote_Label.Text()],
            lines.Lines.Where(l => l.Search is not null).Select(l => l.Text));
        Assert.True(lines.Lines[^1].Note);
    }

    [Fact]
    public async Task S5148_the_unset_choice_heads_the_codes_and_typing_narrows_them()
    {
        var search = Build(new ExchangeRatesConfig()).Lines[0].Search!;

        var all = await search.Find(string.Empty, CancellationToken.None);
        Assert.Equal(ExchangeRules.Published.Count + 1, all.Lines.Count);
        Assert.True(all.Lines[0].Checked);

        var swiss = await search.Find("ch", CancellationToken.None);
        Assert.Equal(["CHF"], swiss.Lines.Skip(1).Select(l => l.Text));
    }

    [Fact]
    public async Task S5148_a_pick_stores_the_code_once_and_the_current_one_nothing()
    {
        var config = new ExchangeRatesConfig { QuoteCurrency = "USD" };
        var search = Build(config).Lines[1].Search!;
        Assert.Equal("USD", search.Value);

        var answer = await search.Find("u", CancellationToken.None);
        answer.Lines.Single(l => l.Text == "USD").Action!();
        Assert.Equal(0, _saves);

        (await search.Find("jp", CancellationToken.None)).Lines.Single(l => l.Text == "JPY").Action!();
        Assert.Equal("JPY", config.QuoteCurrency);
        Assert.Equal(1, _saves);
    }
}
