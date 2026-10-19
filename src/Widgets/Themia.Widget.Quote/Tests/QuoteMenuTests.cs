using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Quote.Tests;

/// <summary>[S-1502] The quote widget's options on its properties menu: the list, the author switch, the shuffle, the cadence.</summary>
public sealed class QuoteMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(QuoteConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        QuoteMenu.Lines(lines, config);
        return lines;
    }

    private static QuoteConfig Two() => new() { Quotes = [new() { Text = "First", Author = "A" }, new() { Text = "Second" }] };

    private ItemMenuEntry Quotes(QuoteConfig config) => Build(config).Lines[0];

    [Fact]
    public void S1502_appearance_then_quotes_author_shuffle_and_refresh()
    {
        var lines = Build(Two());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [QuoteStr.WidgetsC_Quote_Settings_Quotes.Text(), QuoteStr.WidgetsC_Quote_Settings_ShowAuthor_Label.Text(), QuoteStr.Quote_Shuffle.Text(), Str.Common_Refresh.Text()],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    [Fact]
    public void S1502_with_one_quote_or_none_there_is_no_rotation_to_set()
    {
        foreach (var config in (QuoteConfig[])[new() { Quotes = [new() { Text = "Only" }] }, new() { Quotes = [] }, new() { Quotes = [new() { Text = "Only" }, new() { Text = " " }], Shuffle = true }])
        {
            var lines = Build(config).Lines;

            Assert.Equal(
                [QuoteStr.WidgetsC_Quote_Settings_Quotes.Text(), QuoteStr.WidgetsC_Quote_Settings_ShowAuthor_Label.Text()],
                lines.Select(l => l.Text));
            Assert.DoesNotContain(lines, l => l.IsSeparator);
        }
    }

    [Fact]
    public void S1502_reshuffle_is_offered_with_the_shuffle_only_and_deals_a_new_seed()
    {
        var config = Two();
        config.Shuffle = true;
        var seed = config.Seed;

        var reshuffle = Build(config).Lines.Single(l => l.Text == QuoteStr.WidgetsC_Quote_Settings_Reshuffle_Label.Text());
        reshuffle.Action!();

        Assert.Equal(seed + 1, config.Seed);
        Assert.Equal(1, _saves);
        Assert.DoesNotContain(Build(Two()).Lines, l => l.Text == QuoteStr.WidgetsC_Quote_Settings_Reshuffle_Label.Text());
    }

    [Fact]
    public void S1502_the_list_names_each_quote_by_its_text_and_adds_a_typed_one()
    {
        var config = Two();
        var list = Quotes(config).Children!;
        Assert.Equal(["First", "Second"], list.Where(l => l.Children is not null).Select(l => l.Text));

        list.Single(l => l.Text == Str.Menu_Add.Text()).Action!();
        _asked!.Done("  Third  ");

        Assert.Equal("Third", config.Quotes[^1].Text);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1502_with_no_quote_left_the_list_says_so()
    {
        var list = Quotes(new QuoteConfig { Quotes = [] }).Children!;
        Assert.Contains(list, l => l.Note && l.Text == QuoteStr.WidgetsC_Quote_Settings_EmptyList.Text());
    }

    [Fact]
    public void S1502_a_quote_s_text_is_changed_by_rename_and_its_author_by_author()
    {
        var config = Two();
        var first = Quotes(config).Children!.Single(l => l.Text == "First").Children!;

        first.Single(l => l.Text == Str.Menu_Rename.Text()).Action!();
        Assert.Equal("First", _asked!.Initial);
        _asked.Done("Changed");
        Assert.Equal("Changed", config.Quotes[0].Text);

        first.Single(l => l.Text == QuoteStr.WidgetsC_Quote_Menu_Author.Text()).Action!();
        Assert.True(_asked!.AllowEmpty);
        _asked.Done(" ");
        Assert.Equal(string.Empty, config.Quotes[0].Author);
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S1502_the_text_or_author_it_holds_typed_again_saves_nothing()
    {
        var item = new QuoteItem { Text = "Same", Author = "Who" };

        Assert.False(QuoteMenu.SetText(item, " Same "));
        Assert.False(QuoteMenu.SetText(item, "   "));
        Assert.False(QuoteMenu.SetAuthor(item, "Who"));
        Assert.Equal("Same", item.Text);
    }

    [Fact]
    public void S1502_a_cadence_picked_is_stored_once()
    {
        var config = Two();
        Build(config).Lines.Single(l => l.Text == Str.Common_Refresh.Text()).Children!
            .Single(l => l.Text == QuoteStr.WidgetsC_Quote_Settings_Cadence_Hourly.Text()).Action!();

        Assert.Equal(QuoteCadence.Hourly, config.Cadence);
        Assert.Equal(1, _saves);
    }
}
