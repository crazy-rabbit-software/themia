using Themia.Core.Notes;
using Xunit;

namespace Themia.Widget.Notes.Tests;

/// <summary>
/// The note markdown tokenizer, ported from the original <c>markdown.test.ts</c>. Pins the block and
/// inline grammar and, above all, the invariant the editor rests on: the spans concatenate back to
/// the exact source line. If that breaks, the caret and the saved note drift apart silently.
/// </summary>
public sealed class NoteMarkdownTests
{
    private static string Joined(NoteLine line) => string.Concat(line.Spans.Select(s => s.Text));

    private static NoteLineKind[] Kinds(string source) => [.. NoteMarkdown.Parse(source).Select(l => l.Kind)];

    private static string[] With(NoteLine line, NoteSpanStyle flag) => [.. line.Spans.Where(s => s.Has(flag)).Select(s => s.Text)];

    private static NoteSpan[] Content(NoteLine line) => [.. line.Spans.Where(s => !s.Has(NoteSpanStyle.Marker))];

    public static TheoryData<string> Corpus() =>
    [
        "",
        "plain text",
        "# Heading",
        "#not a heading",
        "   ## indented heading",
        "### h3 with **bold** and `code`",
        "- bullet\n- another",
        "1. first\n2) second",
        "  - nested\n    - deeper",
        "- [ ] todo\n- [x] done",
        "> quoted **text**",
        "---",
        "***",
        "```js\nconst x = 1;\n```",
        "~~~\nfenced\n~~~",
        "**bold** *italic* ~~strike~~ `code`",
        "***bold italic***",
        "**unclosed bold",
        "*unclosed italic",
        "`unclosed code",
        "~~unclosed strike",
        "snake_case_name stays plain",
        "_real italic_ here",
        "a * b * c",
        "****",
        "``",
        "**a**b**c**",
        "trailing newline\n",
        "\n\n\n",
        "unicode: héllo 日本語 🎉 **bold**",
        "\t- tab indented",
        "- [X] capital done",
        "__bold__ and __not_bold",
        "*a **b** c*",
        "~~a *b* c~~ `x`",
    ];

    [Theory]
    [MemberData(nameof(Corpus))]
    public void S1352_spans_rebuild_the_source_exactly(string source)
    {
        var rebuilt = string.Join('\n', NoteMarkdown.Parse(source).Select(Joined));
        Assert.Equal(source, rebuilt);
    }

    [Fact]
    public void S1352_one_line_per_source_line()
    {
        Assert.Equal(3, NoteMarkdown.Parse("a\nb\nc").Count);
        Assert.Equal(2, NoteMarkdown.Parse("trailing\n").Count);
        Assert.Single(NoteMarkdown.Parse(string.Empty));
    }

    [Fact]
    public void S1350_headings_by_hash_count()
        => Assert.Equal([NoteLineKind.Heading1, NoteLineKind.Heading2, NoteLineKind.Heading3], Kinds("# a\n## b\n### c"));

    [Fact]
    public void S1350_a_heading_needs_a_space_after_the_hashes()
    {
        Assert.Equal([NoteLineKind.Paragraph], Kinds("#tag"));
        Assert.Equal([NoteLineKind.Paragraph], Kinds("#### four hashes"));
    }

    [Fact]
    public void S1350_lists_quotes_and_rules()
    {
        Assert.Equal([NoteLineKind.Bullet, NoteLineKind.Bullet, NoteLineKind.Bullet], Kinds("- a\n* b\n+ c"));
        Assert.Equal([NoteLineKind.Ordered, NoteLineKind.Ordered], Kinds("1. a\n2) b"));
        Assert.Equal([NoteLineKind.Quote], Kinds("> quoted"));
        Assert.Equal([NoteLineKind.Rule, NoteLineKind.Rule, NoteLineKind.Rule], Kinds("---\n***\n___"));
    }

    [Fact]
    public void S1350_a_bullet_needs_a_space()
    {
        Assert.Equal([NoteLineKind.Rule], Kinds("---"));
        Assert.Equal([NoteLineKind.Paragraph], Kinds("-nospace"));
    }

    [Fact]
    public void S1350_every_line_between_fences_is_code()
        => Assert.Equal(
            [NoteLineKind.Fence, NoteLineKind.Code, NoteLineKind.Code, NoteLineKind.Fence, NoteLineKind.Paragraph],
            Kinds("```js\ncode()\nmore\n```\nafter"));

    [Fact]
    public void S1350_no_inline_syntax_inside_a_fence()
    {
        var inside = NoteMarkdown.Parse("```\n**not bold**\n```")[1];
        Assert.Equal([new NoteSpan("**not bold**", NoteSpanStyle.Code)], inside.Spans);
    }

    [Fact]
    public void S1350_indent_in_two_space_steps_capped_at_six()
    {
        var depths = NoteMarkdown.Parse("- a\n  - b\n    - c\n" + new string(' ', 40) + "- deep").Select(l => l.Indent);
        Assert.Equal([0, 1, 2, 6], depths);
    }

    [Fact]
    public void S1351_bold_italic_strike_and_code_runs()
    {
        var line = NoteMarkdown.Parse("**b** *i* ~~s~~ `c`")[0];
        Assert.Equal(["b"], With(line, NoteSpanStyle.Bold));
        Assert.Equal(["i"], With(line, NoteSpanStyle.Italic));
        Assert.Equal(["s"], With(line, NoteSpanStyle.Strike));
        Assert.Equal(["c"], With(line, NoteSpanStyle.Code));
    }

    [Fact]
    public void S1351_italic_nests_inside_bold()
    {
        var line = NoteMarkdown.Parse("**bold *and* more**")[0];
        var both = line.Spans.Where(s => s.Has(NoteSpanStyle.Bold) && s.Has(NoteSpanStyle.Italic) && !s.Has(NoteSpanStyle.Marker));
        Assert.Equal(["and"], both.Select(s => s.Text));
    }

    [Fact]
    public void S1351_underscores_inside_words_are_left_alone()
    {
        var line = NoteMarkdown.Parse("snake_case_name")[0];
        Assert.Empty(With(line, NoteSpanStyle.Italic));
        Assert.Equal([new NoteSpan("snake_case_name", NoteSpanStyle.None)], line.Spans);
    }

    [Fact]
    public void S1351_underscores_at_word_boundaries_count()
        => Assert.Equal(["italic"], With(NoteMarkdown.Parse("_italic_ text")[0], NoteSpanStyle.Italic));

    [Fact]
    public void S1351_a_lone_star_does_not_eat_into_a_double_star_run()
    {
        var line = NoteMarkdown.Parse("**bold**")[0];
        Assert.Empty(With(line, NoteSpanStyle.Italic));
        Assert.Equal(["bold"], With(line, NoteSpanStyle.Bold).Where(t => t != "**"));
    }

    [Fact]
    public void S1351_nothing_nests_inside_a_code_span()
    {
        var line = NoteMarkdown.Parse("`**literal**`")[0];
        Assert.Empty(With(line, NoteSpanStyle.Bold));
        Assert.Equal(["**literal**"], With(line, NoteSpanStyle.Code));
    }

    [Fact]
    public void S1351_inline_syntax_applies_to_heading_content()
    {
        var heading = NoteMarkdown.Parse("## a **b**")[0];
        Assert.Equal(new NoteSpan("## ", NoteSpanStyle.Marker), heading.Spans[0]);
        Assert.Equal(["b"], Content(heading).Where(s => s.Has(NoteSpanStyle.Bold)).Select(s => s.Text));
    }

    [Theory]
    [InlineData("**oops")]
    [InlineData("*oops")]
    [InlineData("~~oops")]
    [InlineData("`oops")]
    public void S1352_a_lone_opener_stays_plain(string source)
        => Assert.Equal([new NoteSpan(source, NoteSpanStyle.None)], NoteMarkdown.Parse(source)[0].Spans);

    [Fact]
    public void S1352_delimiters_need_a_character_between_them()
        => Assert.Equal([new NoteSpan("a **** b", NoteSpanStyle.None)], NoteMarkdown.Parse("a **** b")[0].Spans);

    [Fact]
    public void S1352_a_bare_run_of_asterisks_is_a_rule()
        => Assert.Equal(NoteLineKind.Rule, NoteMarkdown.Parse("****")[0].Kind);

    [Fact]
    public void S1358_unchecked_and_checked_boxes()
    {
        var lines = NoteMarkdown.Parse("- [ ] milk\n- [x] bread");
        Assert.Equal(NoteLineKind.Task, lines[0].Kind);
        Assert.False(lines[0].Checked);
        Assert.True(lines[1].Checked);
    }

    [Fact]
    public void S1358_a_capital_x_is_checked()
        => Assert.True(NoteMarkdown.Parse("- [X] done")[0].Checked);

    [Fact]
    public void S1358_the_box_is_its_own_span()
        => Assert.Equal(["[ ]"], With(NoteMarkdown.Parse("- [ ] milk")[0], NoteSpanStyle.Box));

    [Fact]
    public void S1358_a_checked_items_content_is_struck()
    {
        var line = NoteMarkdown.Parse("- [x] bread")[0];
        Assert.All(Content(line), s => Assert.True(s.Has(NoteSpanStyle.Strike)));
        Assert.Equal("bread", string.Concat(Content(line).Select(s => s.Text)));
    }

    [Fact]
    public void S1358_an_unchecked_item_is_not_struck()
        => Assert.Empty(With(NoteMarkdown.Parse("- [ ] milk")[0], NoteSpanStyle.Strike));

    [Fact]
    public void S1358_without_the_box_it_is_a_plain_bullet()
        => Assert.Equal(NoteLineKind.Bullet, NoteMarkdown.Parse("- [] milk")[0].Kind);

    [Fact]
    public void S1361_lines_that_parse_alike_match()
    {
        var a = NoteMarkdown.Parse("- **a**")[0];
        var b = NoteMarkdown.Parse("- **a**")[0];
        var c = NoteMarkdown.Parse("- *a*")[0];
        Assert.True(a.Matches(b));
        Assert.False(a.Matches(c));
    }
}
