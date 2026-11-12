using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>[S-1475] [S-6407] Now playing on the family's blocks: what each size shows, and where its art goes.</summary>
public sealed class MusicFaceTests
{
    // A tier's view area (the card less its 44 header) and its content (the area less the inset either side and at the foot).
    private static MusicPlan At(double w, double h, MusicStyle style, bool transport = true, bool progress = true, bool subtitle = true)
    {
        var area = new WidgetArea(w, h - FamilySpace.ContentTop);
        return MusicFace.Plan(area, w - (2 * FamilySpace.Inset), area.Height - FamilySpace.Inset, style, transport, progress, subtitle);
    }

    [Fact]
    public void S1475_a_small_tile_keeps_the_title_and_the_transport()
    {
        var xs = At(120, 120, MusicStyle.Artwork);

        Assert.Equal(FamilyType.BodyStrong, xs.Title);
        Assert.True(xs.Controls);
        Assert.False(xs.Subtitle);
        Assert.False(xs.Progress);
        Assert.Equal(MusicArt.None, xs.Art);
    }

    [Fact]
    public void S1475_height_goes_to_the_transport_then_the_artist_then_the_progress()
    {
        var s = At(200, 150, MusicStyle.Artwork);
        Assert.Equal(FamilyType.Title, s.Title);
        Assert.True(s.Controls && s.Subtitle);
        Assert.False(s.Progress);

        var m = At(320, 240, MusicStyle.Artwork);
        Assert.True(m.Controls && m.Subtitle && m.Progress);

        // Without the transport, its room goes to the progress row.
        var bare = At(480, 160, MusicStyle.Text, transport: false);
        Assert.False(bare.Controls);
        Assert.True(bare.Subtitle && bare.Progress);
    }

    [Fact]
    public void S1475_on_a_compact_width_the_art_is_a_48_lead_or_a_square_over_the_lines()
    {
        // S: the column beside a side cover would be narrower than the transport, so the lead it is.
        Assert.Equal((MusicArt.Lead, 48d), (At(200, 150, MusicStyle.Artwork).Art, At(200, 150, MusicStyle.Artwork).ArtSize));

        // Tall: the content's width, over the lines.
        Assert.Equal((MusicArt.Over, 168d), (At(200, 400, MusicStyle.Artwork).Art, At(200, 400, MusicStyle.Artwork).ArtSize));

        // A lead beside one line would stand over nothing: no artist, no lead.
        Assert.Equal(MusicArt.None, At(200, 150, MusicStyle.Artwork, subtitle: false).Art);
        Assert.Equal(MusicArt.None, At(560, 420, MusicStyle.Text).Art);
    }

    [Fact]
    public void S2155_from_a_regular_width_the_cover_is_a_square_at_the_left_as_tall_as_the_content()
    {
        // M: 180 of content, half of 288 is 144; Wide: the content's 100; L and XL: half the width.
        Assert.Equal((MusicArt.Side, 144d), (At(320, 240, MusicStyle.Artwork).Art, At(320, 240, MusicStyle.Artwork).ArtSize));
        Assert.Equal((MusicArt.Side, 100d), (At(480, 160, MusicStyle.Artwork).Art, At(480, 160, MusicStyle.Artwork).ArtSize));
        Assert.Equal((MusicArt.Side, 264d), (At(560, 420, MusicStyle.Artwork).Art, At(560, 420, MusicStyle.Artwork).ArtSize));
        Assert.Equal((MusicArt.Side, 384d), (At(800, 560, MusicStyle.Artwork).Art, At(800, 560, MusicStyle.Artwork).ArtSize));

        // Beside it the lines and the transport keep the ladder's order of need; without an artist it stays.
        Assert.True(At(320, 240, MusicStyle.Artwork) is { Subtitle: true, Progress: true, Controls: true });
        Assert.Equal(MusicArt.Side, At(320, 240, MusicStyle.Artwork, subtitle: false).Art);
    }

    [Fact]
    public void S2155_the_cover_gives_way_to_the_controls_and_never_goes_under_48()
    {
        // 240 wide: half of 208 would leave 92 beside it; the transport needs 112, so the cover is 84.
        Assert.Equal((MusicArt.Side, 84d), (At(240, 240, MusicStyle.Artwork).Art, At(240, 240, MusicStyle.Artwork).ArtSize));
        Assert.Equal(MusicFace.ColumnFloor, 240 - (2 * FamilySpace.Inset) - 84 - FamilySpace.ColumnGap);

        // Narrower, the square would be under the family's 48: no side cover (the compact ladder).
        Assert.NotEqual(MusicArt.Side, At(200, 240, MusicStyle.Artwork).Art);
        Assert.Equal(MusicArt.None, At(120, 120, MusicStyle.Artwork).Art);
    }

    [Fact]
    public void S6407_the_cover_band_takes_the_room_left_or_is_not_drawn()
    {
        var m = At(320, 240, MusicStyle.Cover);
        Assert.Equal((MusicArt.Cover, 60d), (m.Art, m.ArtSize));
        Assert.Equal(MusicArt.Cover, At(800, 560, MusicStyle.Cover).Art);

        // A strip has no room above its lines: the style draws them alone.
        Assert.Equal(MusicArt.None, At(480, 160, MusicStyle.Cover).Art);
        Assert.Equal(MusicArt.None, At(200, 150, MusicStyle.Cover).Art);
    }

    [Fact]
    public void S1475_a_time_column_is_as_wide_as_its_widest_case()
    {
        Assert.Equal(Math.Ceiling(HeroFit.Ems("8:88") * 12), MusicFace.TimeWidth("3:42"));
        Assert.Equal(Math.Ceiling(HeroFit.Ems("8:88:88") * 12), MusicFace.TimeWidth("1:02:30"));
        Assert.Equal(MusicFace.TimeWidth("3:42"), MusicFace.TimeWidth(string.Empty));
    }
}
