using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-13607]..[S-13609] Every search's one key rule, its one status line and its latest-wins answers.</summary>
public sealed class SearchRulesTests
{
    [Fact]
    public void S13607_enter_takes_the_best_and_down_goes_to_the_first_only_where_there_are_results()
    {
        Assert.Equal(SearchMove.PickBest, SearchKeys.InBox(WidgetKey.Enter, typed: true, results: 3));
        Assert.Equal(SearchMove.ToFirst, SearchKeys.InBox(WidgetKey.Down, typed: true, results: 3));
        Assert.Equal(SearchMove.ToFirst, SearchKeys.InBox(WidgetKey.Down, typed: false, results: 1));

        Assert.Equal(SearchMove.None, SearchKeys.InBox(WidgetKey.Enter, typed: true, results: 0));
        Assert.Equal(SearchMove.None, SearchKeys.InBox(WidgetKey.Down, typed: true, results: 0));
    }

    [Fact]
    public void S13607_escape_empties_the_box_and_an_empty_box_lets_go()
    {
        Assert.Equal(SearchMove.Clear, SearchKeys.InBox(WidgetKey.Escape, typed: true, results: 0));
        Assert.Equal(SearchMove.LetGo, SearchKeys.InBox(WidgetKey.Escape, typed: false, results: 5));
    }

    [Fact]
    public void S13607_other_keys_in_the_box_are_the_box_own()
    {
        foreach (var key in new[] { WidgetKey.Up, WidgetKey.Left, WidgetKey.Right, WidgetKey.Home, WidgetKey.End, WidgetKey.Tab, WidgetKey.Space })
        {
            Assert.Equal(SearchMove.None, SearchKeys.InBox(key, typed: true, results: 4));
        }
    }

    [Fact]
    public void S13607_up_on_the_first_result_goes_back_to_the_box_and_escape_on_a_result_empties_it()
    {
        Assert.Equal(SearchMove.ToBox, SearchKeys.OnResult(WidgetKey.Up, 0, typed: true));
        Assert.Equal(SearchMove.None, SearchKeys.OnResult(WidgetKey.Up, 1, typed: true));
        Assert.Equal(SearchMove.None, SearchKeys.OnResult(WidgetKey.Down, 0, typed: true));

        Assert.Equal(SearchMove.Clear, SearchKeys.OnResult(WidgetKey.Escape, 2, typed: true));
        Assert.Equal(SearchMove.LetGo, SearchKeys.OnResult(WidgetKey.Escape, 2, typed: false));
    }

    [Fact]
    public void S13608_searching_wins_over_everything_while_the_question_is_out()
        => Assert.Equal(Str.Shell_Place_Searching.Text(), LookupStatus.Line("micro", asking: true, found: 0, note: "ignored"));

    [Fact]
    public void S13608_the_answer_word_stands_in_the_status_line_with_or_without_results()
    {
        Assert.Equal("Can't reach the service", LookupStatus.Line("msft", asking: false, found: 1, note: "Can't reach the service"));
        Assert.Equal("No places match", LookupStatus.Line("qqqq", asking: false, found: 0, note: "No places match"));
    }

    [Fact]
    public void S13608_nothing_typed_and_nothing_listed_says_nothing_else_no_results_or_how_many()
    {
        Assert.Equal(string.Empty, LookupStatus.Line(string.Empty, asking: false, found: 0, note: null));
        Assert.Equal(string.Empty, LookupStatus.Line("  ", asking: false, found: 0, note: " "));
        Assert.Equal(Str.Common_NoResults.Text(), LookupStatus.Line("qqqq", asking: false, found: 0, note: null));
        Assert.Equal(Str.EditBar_Page_Found.Format(("count", LocaleFormat.Integer(3))), LookupStatus.Line(string.Empty, asking: false, found: 3, note: null));
    }

    [Fact]
    public void S13609_a_question_is_asked_once_and_its_answer_kept()
    {
        var answers = new LookupAnswers<string>();
        Assert.True(answers.Wants("place", "zur"));

        answers.Asking("place", "zur");
        Assert.False(answers.Wants("place", "zur"));
        Assert.Null(answers.For("place", "zur"));

        Assert.True(answers.Answered("place", "zur", "Zürich"));
        Assert.Equal("Zürich", answers.For("place", "zur"));
        Assert.False(answers.Wants("place", "zur"));
    }

    [Fact]
    public void S13609_a_slow_answer_to_an_older_question_never_replaces_the_newer_one()
    {
        var answers = new LookupAnswers<string>();
        answers.Asking("symbol", "mic");
        answers.Asking("symbol", "micro");

        Assert.False(answers.Answered("symbol", "mic", "old"));
        Assert.Null(answers.For("symbol", "micro"));
        Assert.Null(answers.For("symbol", "mic"));

        Assert.True(answers.Answered("symbol", "micro", "new"));
        Assert.Equal("new", answers.For("symbol", "micro"));
        Assert.True(answers.Wants("symbol", "mic"));
    }

    [Fact]
    public void S13609_lookups_keep_their_answers_apart_and_go_together()
    {
        var answers = new LookupAnswers<string>();
        answers.Asking("base", "e");
        answers.Asking("quote", "u");
        Assert.True(answers.Answered("base", "e", "EUR"));
        Assert.True(answers.Answered("quote", "u", "USD"));
        Assert.Equal("EUR", answers.For("base", "e"));
        Assert.Equal("USD", answers.For("quote", "u"));

        answers.Clear();
        Assert.True(answers.Wants("base", "e"));
        Assert.Null(answers.For("quote", "u"));
    }

    /// <summary>
    /// [S-16409] The loop the owner saw ("when searching for a place it can go into an infinite reload loop"), as the page
    /// lived it: an answer to "zu" draws the lookup's box anew (box B, "zu") while the box it replaces (A) still has the
    /// user's "e" on its way. Each box raises its words later, A "zue", B "zu"; and every box the page draws for a change
    /// raises its own starting words after it. Taken as typing (the old rule: every change of the words), the two texts flip
    /// what is typed for ever and every flip draws a box anew and asks again; taken as <see cref="LookupTyping"/> takes them
    /// (only the box shown now types), the page settles at once.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S16409_a_lookup_drawn_anew_never_types_its_own_words_so_answers_settle(bool fixedRule)
    {
        var held = "zu";
        var shown = "B";
        var drawings = 0;
        var boxes = 0;
        bool Types(string box, string text) => fixedRule ? LookupTyping.Types(box == shown, text, held) : text != held;

        // Waiting: A's "zue" (drawn over), then B's starting "zu".
        var events = new Queue<(string Box, string Text)>([("A", "zue"), ("B", "zu")]);
        while (events.Count > 0 && drawings < 50)
        {
            var (box, text) = events.Dequeue();
            if (!Types(box, text))
            {
                continue;
            }

            // Typed: the page draws the lookup anew (a new box with what is typed, its words raised later) and asks.
            held = text;
            drawings++;
            shown = $"N{++boxes}";
            events.Enqueue((shown, text));
        }

        if (fixedRule)
        {
            Assert.Equal(("zu", 0), (held, drawings));
        }
        else
        {
            Assert.Equal(50, drawings);
        }
    }

    [Fact]
    public void S16409_only_the_box_shown_types_and_only_new_words()
    {
        Assert.True(LookupTyping.Types(shows: true, "zur", "zu"));
        Assert.False(LookupTyping.Types(shows: false, "zur", "zu"));
        Assert.False(LookupTyping.Types(shows: true, "zu", "zu"));
        Assert.True(LookupTyping.Types(shows: true, string.Empty, "zu"));
        Assert.False(LookupTyping.Types(shows: true, string.Empty, null));
    }

    [Fact]
    public void S16409_a_question_cut_short_is_asked_again_not_searching_for_good()
    {
        var answers = new LookupAnswers<string>();
        answers.Asking("place", "zur");
        Assert.False(answers.Wants("place", "zur"));

        // Cut short (the page's token cancelled): forgotten, so the lookup drawn again asks it again.
        answers.Drop("place", "zur");
        Assert.True(answers.Wants("place", "zur"));
        Assert.Null(answers.For("place", "zur"));

        // A newer question or an answer already given is kept.
        answers.Asking("place", "zuri");
        answers.Drop("place", "zur");
        Assert.False(answers.Wants("place", "zuri"));
        Assert.True(answers.Answered("place", "zuri", "Zurich"));
        answers.Drop("place", "zuri");
        Assert.Equal("Zurich", answers.For("place", "zuri"));
    }

    [Fact]
    public void S13609_an_answer_never_asked_for_is_dropped()
        => Assert.False(new LookupAnswers<string>().Answered("place", "zur", "Zürich"));
}
