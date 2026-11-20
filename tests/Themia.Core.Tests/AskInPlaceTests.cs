using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2351] A line of text or a question asked in place, in its own card, never on a page of its own (owner, 2026-11-20:
/// "fence name setting opens a new subpage where i can enter the name, this should be a section not an own page"): which
/// section a page has open, and how it is given, refused, answered and closed.
/// </summary>
public sealed class AskInPlaceTests
{
    [Fact]
    public void S2351_a_section_opens_on_its_head_and_closes_on_it_again_keeping_nothing()
    {
        var asks = new AskInPlace();
        Assert.Null(asks.Open);

        Assert.True(asks.Toggle("w/Fence name"));
        Assert.True(asks.IsOpen("w/Fence name"));

        Assert.False(asks.Toggle("w/Fence name"));
        Assert.Null(asks.Open);
    }

    [Fact]
    public void S2351_one_section_is_open_at_a_time_per_page()
    {
        var asks = new AskInPlace();
        asks.Toggle("w/Fence name");

        // Another opened: the first closes, nothing of it given.
        Assert.True(asks.Toggle("w/Rules/Name contains"));
        Assert.False(asks.IsOpen("w/Fence name"));
        Assert.Equal("w/Rules/Name contains", asks.Open);

        // A question asked after a step of its own takes the place of whatever was open.
        asks.Show("backup/import");
        Assert.Equal("backup/import", asks.Open);
        Assert.False(asks.Accept("w/Rules/Name contains", "x", allowEmpty: true, refuse: null).Kept);
    }

    [Fact]
    public void S2351_escape_a_press_elsewhere_or_the_page_going_cancel_it_and_give_nothing()
    {
        var asks = new AskInPlace();
        Assert.False(asks.Cancel());

        asks.Toggle("w/Fence name");
        Assert.True(asks.Cancel());
        Assert.Null(asks.Open);

        // A press inside its card leaves it; one anywhere else closes it.
        asks.Toggle("w/Fence name");
        Assert.False(asks.PressedOutside(inside: true));
        Assert.True(asks.IsOpen("w/Fence name"));
        Assert.True(asks.PressedOutside(inside: false));
        Assert.Null(asks.Open);
        Assert.False(asks.PressedOutside(inside: false));

        // Given after it closed: nothing.
        var answer = asks.Accept("w/Fence name", "Work", allowEmpty: true, refuse: null);
        Assert.False(answer.Kept);
    }

    [Fact]
    public void S2351_accept_gives_the_text_trimmed_and_closes()
    {
        var asks = new AskInPlace();
        asks.Toggle("w/Fence name");

        var answer = asks.Accept("w/Fence name", "  Work  ", allowEmpty: true, refuse: null);
        Assert.Equal(new AskAnswer(true, "Work", null), answer);
        Assert.Null(asks.Open);
    }

    [Fact]
    public void S3721_a_refused_text_keeps_the_section_open_and_says_why()
    {
        var asks = new AskInPlace();
        asks.Toggle("w/Feeds/Add…");
        static string? Refuse(string typed) => typed.StartsWith("https://", StringComparison.Ordinal) ? null : "Type a web address";

        var answer = asks.Accept("w/Feeds/Add…", " feed ", allowEmpty: false, Refuse);
        Assert.False(answer.Kept);
        Assert.Equal("Type a web address", answer.Why);
        Assert.Equal("feed", answer.Text);
        Assert.True(asks.IsOpen("w/Feeds/Add…"));

        Assert.True(asks.Accept("w/Feeds/Add…", "https://a.example/", allowEmpty: false, Refuse).Kept);
    }

    [Fact]
    public void S3721_an_empty_field_is_refused_without_words_unless_it_may_be_empty()
    {
        // Greyed while empty, nothing said; the check itself sees the text trimmed.
        Assert.Equal(string.Empty, AskInPlace.Reason("   ", allowEmpty: false, refuse: null));
        Assert.Null(AskInPlace.Reason("   ", allowEmpty: true, refuse: null));
        Assert.Null(AskInPlace.Reason(" a ", allowEmpty: false, refuse: t => t == "a" ? null : "untrimmed"));

        var asks = new AskInPlace();
        asks.Toggle("w/Name");
        Assert.Equal(new AskAnswer(false, string.Empty, string.Empty), asks.Accept("w/Name", " ", allowEmpty: false, refuse: null));
        Assert.Equal(new AskAnswer(true, string.Empty, null), asks.Accept("w/Name", " ", allowEmpty: true, refuse: null));
    }

    [Fact]
    public void S3727_only_the_answer_of_the_open_question_does_it()
    {
        var asks = new AskInPlace();
        Assert.False(asks.Answer("view:work/delete"));

        asks.Toggle("view:work/delete");
        Assert.False(asks.Answer("view:home/delete"));
        Assert.True(asks.Answer("view:work/delete"));
        Assert.Null(asks.Open);
        Assert.False(asks.Answer("view:work/delete"));
    }
}
