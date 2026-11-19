using Themia.Core.Links;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11950] [S-11962] [S-11964] A whole card that is one drive (`LinkCardState`, what `LinkCard` draws): the card under
/// the pointer shows its hover, every other card of that drive on the view echoes it, a card of another drive does
/// not, and a card that let go of a hover it took over from another is lit by nothing.
/// </summary>
public sealed class ViewLinksCardTests
{
    private static readonly DriveLink C = new("C:");
    private static readonly DriveLink D = new("D:");

    private static LinkCardState<DriveLink> Card(LinkBoard board, DriveLink? drive)
    {
        var member = board.Join();
        return new LinkCardState<DriveLink>(() => member.Get(LinkTopics.HoveredDrive), value => member.Set(LinkTopics.HoveredDrive, value))
        {
            Key = drive,
        };
    }

    [Fact]
    public void S11962_the_card_under_the_pointer_shows_its_hover()
    {
        var board = new LinkBoard();
        var disk = Card(board, C);

        disk.Enter(wholeCard: true);

        // Owner, 2026-11-19: "when hovering over a widget it also does not show the hover effect".
        Assert.Equal(LinkLight.Pointed, disk.Light);
        Assert.Equal(C, board.Peek(LinkTopics.HoveredDrive));

        disk.Leave();

        Assert.Equal(LinkLight.None, disk.Light);
        Assert.Null(board.Peek(LinkTopics.HoveredDrive));
    }

    [Fact]
    public void S11950_every_other_card_of_the_drive_echoes_it_and_another_drive_does_not()
    {
        var board = new LinkBoard();
        var disk = Card(board, C);
        var history = Card(board, C);
        var facts = Card(board, C);
        var other = Card(board, D);

        disk.Enter(wholeCard: true);

        Assert.Equal(LinkLight.Pointed, disk.Light);
        Assert.Equal(LinkLight.Pointed, history.Light);
        Assert.Equal(LinkLight.Pointed, facts.Light);
        Assert.Equal(LinkLight.None, other.Light);

        disk.Leave();

        Assert.All(new[] { disk, history, facts, other }, card => Assert.Equal(LinkLight.None, card.Light));
    }

    [Fact]
    public void S11954_a_part_of_the_card_publishes_without_lighting_the_card_under_its_own_pointer()
    {
        // A folder's header or path: it has its own hover; the drive card beside it echoes.
        var board = new LinkBoard();
        var folder = Card(board, C);
        var drive = Card(board, C);

        folder.Enter(wholeCard: false);

        Assert.Equal(LinkLight.None, folder.Light);
        Assert.Equal(LinkLight.Pointed, drive.Light);
    }

    [Fact]
    public void S11964_moving_from_card_to_card_leaves_nothing_lit()
    {
        var board = new LinkBoard();
        var disk = Card(board, C);
        var history = Card(board, C);

        // Touching cards: the next one hears the pointer before the last lets go, so it sets the same drive again and
        // owns the hover without anyone hearing a change; the late leave of the first is not its to clear.
        disk.Enter(wholeCard: true);
        history.Enter(wholeCard: true);
        disk.Leave();

        Assert.Equal(LinkLight.Pointed, history.Light);
        Assert.Equal(LinkLight.Pointed, disk.Light);

        history.Leave();

        // The card that let go never hears its own clear: lit from a remembered value it stayed lit (the stuck echo).
        Assert.Equal(LinkLight.None, history.Light);
        Assert.Equal(LinkLight.None, disk.Light);
    }

    [Fact]
    public void S11964_a_card_whose_drive_changes_under_the_pointer_publishes_the_new_one()
    {
        var board = new LinkBoard();
        var drive = Card(board, C);
        var other = Card(board, D);

        drive.Enter(wholeCard: true);
        drive.Key = D;

        Assert.Equal(D, board.Peek(LinkTopics.HoveredDrive));
        Assert.Equal(LinkLight.Pointed, other.Light);
        Assert.Equal(LinkLight.Pointed, drive.Light);
    }

    [Fact]
    public void S11952_a_card_of_no_drive_takes_no_part()
    {
        var board = new LinkBoard();
        var all = Card(board, null);
        var disk = Card(board, C);

        all.Enter(wholeCard: true);

        Assert.Equal(LinkLight.None, all.Light);
        Assert.Null(board.Peek(LinkTopics.HoveredDrive));
        Assert.Equal(LinkLight.None, disk.Light);
    }

    [Fact]
    public void S11908_a_quiet_board_lights_no_echo()
    {
        var board = new LinkBoard { Quiet = true };
        var disk = Card(board, C);
        var history = Card(board, C);

        disk.Enter(wholeCard: true);

        Assert.Equal(LinkLight.None, history.Light);
    }
}
