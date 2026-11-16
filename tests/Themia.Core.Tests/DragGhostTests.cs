using Themia.Core.Files;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5564] [S-5565] The picture a drag of items shows, as Explorer's: one icon, or a stack of at most
/// three with the number of dragged items in a badge; the layout as a rule and the pixels composed from it.
/// </summary>
public sealed class DragGhostTests
{
    private static readonly GhostColour Blue = new(0, 120, 212);
    private static readonly GhostColour White = new(255, 255, 255);

    /// <summary>An opaque icon of one colour.</summary>
    private static GhostIcon Icon(int side, byte b, byte g, byte r)
    {
        var pixels = new byte[side * side * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (b, g, r, 255);
        }

        return new GhostIcon(side, side, pixels);
    }

    private static byte[] Pixel(DragPicture picture, int x, int y) => picture.Bgra.AsSpan(((y * picture.Width) + x) * 4, 4).ToArray();

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, null)]
    [InlineData(2, "2")]
    [InlineData(12, "12")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    [InlineData(5000, "99+")]
    public void S5564_the_badge_counts_several_items_and_stops_at_99_plus(int count, string? expected)
        => Assert.Equal(expected, DragGhost.BadgeText(count));

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 2)]
    [InlineData(3, 3, 3)]
    [InlineData(12, 12, 3)]
    [InlineData(12, 2, 2)]
    [InlineData(12, 0, 0)]
    [InlineData(0, 5, 0)]
    public void S5564_at_most_three_icons_are_shown_and_never_more_than_there_are_pictures(int count, int pictures, int shown)
        => Assert.Equal(shown, DragGhost.Shown(count, pictures));

    [Fact]
    public void S5564_one_item_is_its_icon_alone_held_where_it_was_grabbed()
    {
        var plan = DragGhost.Plan(count: 1, pictures: 1, icon: 48, grabX: 30, grabY: 12, badgeText: 0);

        Assert.NotNull(plan);
        Assert.Equal([new GhostBox(0, 0, 48, 48)], plan.Icons);
        Assert.Null(plan.Badge);
        Assert.Equal((48d, 48d), (plan.Width, plan.Height));
        Assert.Equal((30d, 12d), (plan.HotX, plan.HotY));
    }

    [Theory]
    [InlineData(-20, 70, 0, 48)] // pressed on the name, left of and below the icon
    [InlineData(60, -5, 48, 0)]
    [InlineData(double.NaN, double.PositiveInfinity, 24, 24)]
    public void S5564_a_press_beside_the_icon_holds_its_nearest_point(double grabX, double grabY, double hotX, double hotY)
    {
        var plan = DragGhost.Plan(1, 1, 48, grabX, grabY, 0);

        Assert.NotNull(plan);
        Assert.Equal((hotX, hotY), (plan.HotX, plan.HotY));
    }

    [Fact]
    public void S5564_several_items_are_one_stack_the_grabbed_icon_in_front_and_lowest_right()
    {
        var plan = DragGhost.Plan(count: 3, pictures: 3, icon: 48, grabX: 10, grabY: 20, badgeText: 7);

        Assert.NotNull(plan);
        Assert.Equal(3, plan.Icons.Count);

        // Front first: each one behind a step up and to the left of the one before it.
        Assert.Equal(new GhostBox(2 * DragGhost.Step, 2 * DragGhost.Step, 48, 48), plan.Icons[0]);
        Assert.Equal(new GhostBox(DragGhost.Step, DragGhost.Step, 48, 48), plan.Icons[1]);
        Assert.Equal(new GhostBox(0, 0, 48, 48), plan.Icons[2]);

        // The pointer holds the front icon.
        Assert.Equal((plan.Icons[0].X + 10, plan.Icons[0].Y + 20), (plan.HotX, plan.HotY));
    }

    [Fact]
    public void S5564_the_badge_hangs_out_of_the_front_icons_lower_right_corner()
    {
        var plan = DragGhost.Plan(count: 2, pictures: 2, icon: 48, grabX: 0, grabY: 0, badgeText: 7);

        Assert.NotNull(plan);
        var badge = Assert.IsType<GhostBadge>(plan.Badge);
        var front = plan.Icons[0];
        Assert.Equal("2", badge.Text);
        Assert.Equal(DragGhost.BadgeSide, badge.Box.Height);
        Assert.Equal(DragGhost.BadgeSide, badge.Box.Width); // a short number: the least width
        Assert.Equal(front.Right + DragGhost.BadgeOut, badge.Box.Right);
        Assert.Equal(front.Bottom + DragGhost.BadgeOut, badge.Box.Bottom);

        // The picture is the box round the stack and the badge.
        Assert.Equal((badge.Box.Right, badge.Box.Bottom), (plan.Width, plan.Height));
    }

    [Fact]
    public void S5564_twelve_items_show_three_icons_and_the_number_twelve()
    {
        var plan = DragGhost.Plan(count: 12, pictures: 12, icon: 48, grabX: 0, grabY: 0, badgeText: 14.2);

        Assert.NotNull(plan);
        Assert.Equal(DragGhost.MaxShown, plan.Icons.Count);
        Assert.Equal("12", plan.Badge!.Text);

        // A wider number widens the badge: the whole DIPs of its ink and the room either side.
        Assert.Equal(15 + (2 * DragGhost.BadgePad), plan.Badge.Box.Width);
    }

    [Fact]
    public void S5564_a_badge_wider_than_a_small_icon_moves_the_stack_right_and_nothing_is_left_of_the_picture()
    {
        // A 16-DIP row icon with "99+": the badge is wider than the icon.
        var plan = DragGhost.Plan(count: 150, pictures: 1, icon: 16, grabX: 8, grabY: 8, badgeText: 22);

        Assert.NotNull(plan);
        Assert.Equal("99+", plan.Badge!.Text);
        Assert.Equal(0, plan.Badge.Box.X);
        Assert.True(plan.Icons[0].X > 0);
        Assert.All(plan.Icons, icon => Assert.True(icon.X >= 0 && icon.Y >= 0 && icon.Right <= plan.Width && icon.Bottom <= plan.Height));
        Assert.Equal(plan.Icons[0].Right + DragGhost.BadgeOut, plan.Badge.Box.Right);
        Assert.Equal((plan.Icons[0].X + 8, plan.Icons[0].Y + 8), (plan.HotX, plan.HotY));
    }

    [Theory]
    [InlineData(0, 3, 48)]
    [InlineData(3, 0, 48)]
    [InlineData(3, 3, 0)]
    [InlineData(3, 3, double.NaN)]
    public void S5564_nothing_to_show_is_no_plan(int count, int pictures, double icon)
        => Assert.Null(DragGhost.Plan(count, pictures, icon, 0, 0, 0));

    [Fact]
    public void S5565_one_icon_is_drawn_see_through_at_the_displays_scale_with_the_pointer_in_pixels()
    {
        var plan = DragGhost.Plan(1, 1, icon: 4, grabX: 1, grabY: 3, badgeText: 0)!;

        var picture = DragGhost.Compose(plan, [Icon(8, 10, 20, 30)], scale: 2, opacity: 0.5, Blue, White, default);

        Assert.NotNull(picture);
        Assert.True(picture.Drawable);
        Assert.Equal((8, 8, 2, 6, 2d), (picture.Width, picture.Height, picture.HotX, picture.HotY, picture.Scale));

        // Premultiplied: every channel halved, alpha with them.
        Assert.Equal([(byte)5, (byte)10, (byte)15, (byte)127], Pixel(picture, 3, 3));
    }

    [Fact]
    public void S5565_the_front_icon_is_drawn_over_the_ones_behind_it()
    {
        // The one behind at 0..40, the front one a step down and right at 6..46; the badge from 30 on.
        var plan = DragGhost.Plan(2, 2, icon: 40, grabX: 0, grabY: 0, badgeText: 0)!;

        // Opaque, so what is on top is what shows: blue in front, red behind.
        var picture = DragGhost.Compose(plan, [Icon(40, 255, 0, 0), Icon(40, 0, 0, 255)], scale: 1, opacity: 1, Blue, White, default)!;

        Assert.Equal([(byte)0, (byte)0, (byte)255, (byte)255], Pixel(picture, 1, 1));   // only the one behind
        Assert.Equal([(byte)255, (byte)0, (byte)0, (byte)255], Pixel(picture, 20, 20)); // both: the front one
        Assert.Equal([(byte)255, (byte)0, (byte)0, (byte)255], Pixel(picture, 43, 10)); // only the front one
        Assert.Equal([(byte)0, (byte)0, (byte)0, (byte)0], Pixel(picture, 44, 2));      // neither
    }

    [Fact]
    public void S5565_the_stack_is_faded_as_one_so_the_icons_behind_never_show_through_the_front()
    {
        var plan = DragGhost.Plan(2, 2, icon: 40, grabX: 0, grabY: 0, badgeText: 0)!;

        var picture = DragGhost.Compose(plan, [Icon(40, 255, 0, 0), Icon(40, 0, 0, 255)], scale: 1, opacity: 0.5, Blue, White, default)!;

        // Where both are: the front one alone, faded (premultiplied halves), no red from behind.
        Assert.Equal([(byte)127, (byte)0, (byte)0, (byte)127], Pixel(picture, 20, 20));
    }

    [Fact]
    public void S5565_the_badge_is_opaque_in_its_colour_with_round_corners_and_its_number_in_the_text_colour()
    {
        var plan = DragGhost.Plan(2, 2, icon: 48, grabX: 0, grabY: 0, badgeText: 2)!;
        var box = plan.Badge!.Box;

        // The number: a 2 × 2 block of full coverage.
        var ink = new GhostInk(2, 2, [255, 255, 255, 255]);
        var picture = DragGhost.Compose(plan, [Icon(48, 9, 9, 9), Icon(48, 9, 9, 9)], scale: 1, opacity: 0.65, Blue, White, ink)!;

        // Inside the badge, off its number: the fill, fully opaque although the icons are see-through.
        Assert.Equal([Blue.B, Blue.G, Blue.R, (byte)255], Pixel(picture, (int)box.X + 5, (int)box.Y + 3));

        // Its middle: the number.
        Assert.Equal([(byte)255, (byte)255, (byte)255, (byte)255], Pixel(picture, (int)(box.X + (box.Width / 2)), (int)(box.Y + (box.Height / 2))));

        // Its very corner lies outside the rounding: no fill there (the picture's own corner is empty).
        Assert.Equal(0, Pixel(picture, picture.Width - 1, picture.Height - 1)[3]);
    }

    [Fact]
    public void S5565_a_badge_whose_number_could_not_be_drawn_is_still_a_badge()
    {
        var plan = DragGhost.Plan(5, 1, icon: 32, grabX: 0, grabY: 0, badgeText: 0)!;

        var picture = DragGhost.Compose(plan, [Icon(32, 1, 2, 3)], 1, 1, Blue, White, default)!;

        var box = plan.Badge!.Box;
        Assert.Equal([Blue.B, Blue.G, Blue.R, (byte)255], Pixel(picture, (int)(box.X + (box.Width / 2)), (int)(box.Y + (box.Height / 2))));
    }

    [Fact]
    public void S5565_an_icon_held_at_other_pixels_is_fitted_into_its_square()
    {
        // Held at 96 pixels, drawn into 48: every pixel of the square is the icon's colour.
        var plan = DragGhost.Plan(1, 1, icon: 48, grabX: 0, grabY: 0, badgeText: 0)!;

        var smaller = DragGhost.Compose(plan, [Icon(96, 40, 80, 120)], 1, 1, Blue, White, default)!;
        var larger = DragGhost.Compose(plan, [Icon(16, 40, 80, 120)], 1, 1, Blue, White, default)!;

        foreach (var picture in new[] { smaller, larger })
        {
            Assert.Equal((48, 48), (picture.Width, picture.Height));
            Assert.Equal([(byte)40, (byte)80, (byte)120, (byte)255], Pixel(picture, 0, 0));
            Assert.Equal([(byte)40, (byte)80, (byte)120, (byte)255], Pixel(picture, 47, 47));
            Assert.Equal([(byte)40, (byte)80, (byte)120, (byte)255], Pixel(picture, 23, 31));
        }
    }

    [Fact]
    public void S5565_an_icon_that_is_not_square_keeps_its_shape_centred()
    {
        var wide = new GhostIcon(4, 2, [.. Enumerable.Repeat((byte)255, 4 * 2 * 4)]);
        var plan = DragGhost.Plan(1, 1, icon: 4, grabX: 0, grabY: 0, badgeText: 0)!;

        var picture = DragGhost.Compose(plan, [wide], 1, 1, Blue, White, default)!;

        Assert.Equal(0, Pixel(picture, 1, 0)[3]);
        Assert.Equal(255, Pixel(picture, 1, 1)[3]);
        Assert.Equal(255, Pixel(picture, 2, 2)[3]);
        Assert.Equal(0, Pixel(picture, 2, 3)[3]);
    }

    [Fact]
    public void S5565_resizing_averages_when_smaller_and_never_bleeds_colour_out_of_transparent_pixels()
    {
        // 2 × 1: an opaque white pixel beside a transparent one (premultiplied: all zero).
        var icon = new GhostIcon(2, 1, [255, 255, 255, 255, 0, 0, 0, 0]);

        var one = DragGhost.Resize(icon, 1, 1);

        // Half covered: half the alpha and, premultiplied, half of every channel.
        Assert.Equal([(byte)128, (byte)128, (byte)128, (byte)128], one);
    }

    [Fact]
    public void S5565_an_icon_without_pixels_leaves_its_square_empty_and_the_rest_is_drawn()
    {
        var plan = DragGhost.Plan(2, 2, icon: 40, grabX: 0, grabY: 0, badgeText: 0)!;

        var picture = DragGhost.Compose(plan, [Icon(40, 7, 7, 7), new GhostIcon(40, 40, new byte[3])], 1, 1, Blue, White, default);

        Assert.NotNull(picture);
        Assert.Equal(0, Pixel(picture, 1, 1)[3]);
        Assert.Equal(255, Pixel(picture, 20, 20)[3]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(1000)] // a picture far past the largest a drag shows
    public void S5565_a_scale_that_gives_no_usable_picture_gives_none(double scale)
    {
        var plan = DragGhost.Plan(1, 1, icon: 48, grabX: 0, grabY: 0, badgeText: 0)!;

        Assert.Null(DragGhost.Compose(plan, [Icon(48, 1, 1, 1)], scale, 1, Blue, White, default));
    }

    [Fact]
    public void S5565_fewer_icons_than_the_plan_shows_is_no_picture()
    {
        var plan = DragGhost.Plan(3, 3, icon: 8, grabX: 0, grabY: 0, badgeText: 0)!;

        Assert.Null(DragGhost.Compose(plan, [Icon(8, 1, 1, 1)], 1, 1, Blue, White, default));
    }

    [Fact]
    public void S5565_the_pictures_opacity_is_the_dragged_look_of_the_selection()
        => Assert.Equal(0.65, Themia.Core.Widgets.SelectionLook.DragOpacity);
}
