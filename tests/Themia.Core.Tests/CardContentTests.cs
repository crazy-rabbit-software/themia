using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5700] [S-12480] [S-12483] A card has no header: its view's top is the card's inset, none where the view owns its
/// top edge. Joining is the cards' chrome only: a card joined to another lays its content out exactly as alone.
/// </summary>
public sealed class CardContentTests
{
    [Fact]
    public void S5700_a_card_starts_its_view_at_the_inset_and_a_view_that_owns_its_top_edge_gets_its_whole_box()
    {
        Assert.Equal(FamilySpace.Inset, CardContent.TopInset(ownsTopEdge: false));
        Assert.Equal(0, CardContent.TopInset(ownsTopEdge: true));
        Assert.Equal(new PixelRect(10, 20 + FamilySpace.Inset, 300, 80 - FamilySpace.Inset), CardContent.Box(new PixelRect(10, 20, 300, 80), ownsTopEdge: false));
    }

    [Fact]
    public void S12480_the_content_starts_at_the_inset_with_no_header_room()
        => Assert.Equal(FamilySpace.Inset, FamilySpace.ContentTop);

    /// <summary>
    /// [S-12483] Regression (owner, 2026-11-17: "the widgets now change their internal layout when connecting to
    /// other widgets. this should not be done"): three cards stacked join (their chrome changes: square inner corners,
    /// no line at the seams), and each one's content box is the one it has alone.
    /// </summary>
    [Fact]
    public void S12483_a_cards_content_box_is_the_same_joined_and_alone()
    {
        PixelRect[] cells = [new(0, 0, 300, 40), new(0, 40, 300, 80), new(0, 120, 300, 140)];
        var joined = CardJoins.Paint(cells, [0, 0, 0], 1);
        Assert.All(joined, o => Assert.NotEqual(CardOutline.Free, o));

        for (var i = 0; i < cells.Length; i++)
        {
            var alone = CardJoins.Paint([cells[i]], [0], 1)[0];
            Assert.Equal(CardOutline.Free, alone);
            foreach (var owns in new[] { false, true })
            {
                Assert.Equal(CardContent.Box(cells[i], owns), CardContent.Box(cells[i], owns));
            }
        }

        // One path: nothing tells a view which of its sides a card joins.
        var guarded = RepoFiles.CSharp(RepoFiles.Src)
            .Where(f => File.ReadLines(f).Any(line => System.Text.RegularExpressions.Regex.IsMatch(line, @"\b(JoinedSides|IWidgetJoins|ApplyJoins|BlockEdge)\b")))
            .Select(f => Path.GetRelativePath(RepoFiles.Root, f).Replace('\\', '/'))
            .ToList();
        Assert.Empty(guarded);
    }
}
