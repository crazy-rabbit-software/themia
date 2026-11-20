using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2361] A page drawn again over the same rows keeps every card that would be drawn the same.</summary>
public sealed class KeptCardsTests
{
    private static readonly string[] Theme = ["theme", "theme/accent", "theme/blur"];

    private static PageMarks Marks(
        string? open = null,
        IEnumerable<string>? unfolded = null,
        Dictionary<string, string>? typed = null,
        Dictionary<string, string>? refused = null,
        Dictionary<string, int>? answered = null)
        => new(
            open,
            new HashSet<string>(unfolded ?? [], StringComparer.Ordinal),
            typed ?? [],
            refused ?? [],
            answered ?? []);

    [Fact]
    public void S2361_a_card_whose_marks_did_not_change_is_kept()
        => Assert.Equal(KeptCards.Of(Theme, false, Marks()), KeptCards.Of(Theme, false, Marks()));

    [Fact]
    public void S2361_another_card_opened_or_closed_keeps_this_one()
    {
        // The owner, 2026-11-20: "when expanding or closing a section ... it lags": only the card that opens is drawn anew.
        var before = KeptCards.Of(Theme, false, Marks());

        Assert.Equal(before, KeptCards.Of(Theme, false, Marks(open: "cards")));
        Assert.Equal(before, KeptCards.Of(Theme, false, Marks(open: "cards", unfolded: ["cards/radius"])));
    }

    [Fact]
    public void S2361_the_card_opened_or_closed_is_drawn_anew()
    {
        var shut = KeptCards.Of(Theme, false, Marks());
        var open = KeptCards.Of(Theme, false, Marks(open: "theme"));

        Assert.NotEqual(shut, open);
        Assert.Equal(open, KeptCards.Of(Theme, false, Marks(open: "theme")));
    }

    [Fact]
    public void S2361_a_picker_unfolded_inside_the_card_draws_it_anew()
        => Assert.NotEqual(
            KeptCards.Of(Theme, false, Marks(open: "theme")),
            KeptCards.Of(Theme, false, Marks(open: "theme", unfolded: ["theme/accent"])));

    [Fact]
    public void S2361_a_row_at_any_depth_of_the_card_counts()
    {
        // A lookup's box inside the card: what is typed there, its answers and a refused text are the card's.
        string[] card = ["place", "place/city", "place/city/find"];

        Assert.NotEqual(KeptCards.Of(card, false, Marks()), KeptCards.Of(card, false, Marks(typed: new() { ["place/city"] = "Zur" })));
        Assert.NotEqual(
            KeptCards.Of(card, false, Marks(typed: new() { ["place/city"] = "Zur" })),
            KeptCards.Of(card, false, Marks(typed: new() { ["place/city"] = "Zuri" })));
        Assert.NotEqual(KeptCards.Of(card, false, Marks()), KeptCards.Of(card, false, Marks(answered: new() { ["place/city"] = 1 })));
        Assert.NotEqual(
            KeptCards.Of(card, false, Marks(answered: new() { ["place/city"] = 1 })),
            KeptCards.Of(card, false, Marks(answered: new() { ["place/city"] = 2 })));
        Assert.NotEqual(KeptCards.Of(card, false, Marks()), KeptCards.Of(card, false, Marks(refused: new() { ["place/city/find"] = "Too long" })));
    }

    [Fact]
    public void S2361_marks_of_other_cards_rows_are_not_this_cards()
        => Assert.Equal(
            KeptCards.Of(Theme, false, Marks()),
            KeptCards.Of(Theme, false, Marks(typed: new() { ["place/city"] = "Zur" }, refused: new() { ["name"] = "Empty" }, answered: new() { ["place/city"] = 3 })));

    [Fact]
    public void S2361_a_card_drawn_inside_another_is_not_the_same_drawing()
        => Assert.NotEqual(KeptCards.Of(Theme, false, Marks()), KeptCards.Of(Theme, true, Marks()));

    [Fact]
    public void S2361_the_order_of_the_rows_and_marks_does_not_matter()
    {
        var one = KeptCards.Of(["a", "b", "c"], false, Marks(open: "b", unfolded: ["c", "a"]));
        var two = KeptCards.Of(["c", "a", "b", "a"], false, Marks(open: "b", unfolded: ["a", "c"]));

        Assert.Equal(one, two);
        Assert.Equal(one.GetHashCode(), two.GetHashCode());
        Assert.Equal(3, one.Count);
    }
}
