using System.Text.RegularExpressions;
using Themia.Sdk.Kit;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2377] A page's row icons are never cut by their box (owner, 2026-11-19: "in the settings pages the icons show
/// clipped"): a widget's entries build their glyph for a menu (WinUI's FontIcon at 20, which a menu's own slot scales),
/// and the page put it in a fixed 16 box, which cut it through its end, top and foot. Every icon a page draws goes
/// through the one slot, which draws a glyph at the row's glyph size and scales down, never cuts, what is larger. The
/// slot is WinUI's, so these read the source; the sizes are tokens.
/// </summary>
public sealed partial class PageIconSlotTests
{
    private static string Pages => RepoFiles.At("src", "Themia.App", "Shell", "Pages");

    [Fact]
    public void S2377_a_rows_icon_slot_holds_its_glyph_in_its_column()
    {
        Assert.Equal(FamilyShape.Icon, ShellMetrics.Page.IconGlyph);
        Assert.True(ShellMetrics.Page.IconSlot >= ShellMetrics.Page.IconGlyph, "the slot is smaller than the glyph it holds");

        // The slot stands in the icon column, before the name.
        Assert.True(ShellMetrics.Page.IconAt + ShellMetrics.Page.IconSlot <= ShellMetrics.Page.NameAt, "the slot runs into the name");
    }

    [Fact]
    public void S2377_every_icon_a_page_draws_goes_through_the_one_slot()
    {
        var unslotted = new List<string>();
        foreach (var file in RepoFiles.CSharp(Pages))
        {
            var source = File.ReadAllText(file);
            foreach (Match built in IconBuilt().Matches(source))
            {
                var name = built.Groups["name"].Value;
                var block = Block(source, built.Index + built.Length);
                if (!block.Contains("IconSlot(" + name + ")", StringComparison.Ordinal))
                {
                    unslotted.Add($"{Path.GetFileName(file)} at {source[..built.Index].Count(c => c == '\n') + 1}: {built.Value}");
                }
            }

            // The fixed box the icons stood in, which cut whatever was larger than it.
            if (FixedBox().Match(source) is { Success: true } box)
            {
                unslotted.Add($"{Path.GetFileName(file)} at {source[..box.Index].Count(c => c == '\n') + 1}: a fixed icon box");
            }
        }

        Assert.Empty(unslotted);
    }

    [Fact]
    public void S2377_the_slot_draws_a_glyph_at_the_rows_size_and_never_cuts_what_is_larger()
    {
        var source = File.ReadAllText(RepoFiles.Source("PageView.Rows.cs"));
        var at = source.IndexOf(" IconSlot(UIElement icon)", StringComparison.Ordinal);
        Assert.True(at >= 0, "PageView has no IconSlot");
        var slot = Block(source, at);

        Assert.Contains("FontSize = P.IconGlyph", slot, StringComparison.Ordinal);
        Assert.Contains("Width = P.IconSlot", slot, StringComparison.Ordinal);
        Assert.Contains("Height = P.IconSlot", slot, StringComparison.Ordinal);
        Assert.Contains("StretchDirection.DownOnly", slot, StringComparison.Ordinal);
    }

    /// <summary>The braced block that starts at or after <paramref name="from"/>.</summary>
    private static string Block(string source, int from)
    {
        var open = source.IndexOf('{', from);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return source[open..(i + 1)];
            }
        }

        return source[open..];
    }

    /// <summary>A row's icon built where it is drawn: <c>if (row.Icon?.Invoke() is { } icon)</c>.</summary>
    [GeneratedRegex(@"if \([\w.]+\.Icon\?\.Invoke\(\) is \{ \} (?<name>\w+)\)")]
    private static partial Regex IconBuilt();

    [GeneratedRegex(@"Width = Fluent\.IconSize, Height = Fluent\.IconSize, Child = icon")]
    private static partial Regex FixedBox();
}
