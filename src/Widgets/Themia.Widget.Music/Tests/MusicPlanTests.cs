using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>
/// [S-16502] Now playing's one plan: the shown set and the style decide its face and its sizing. Every style, every element
/// hidden on its own and some sets together: at its least, natural and large boxes and boxes off its proportions, every
/// shown element is drawn (WR-7: nothing gives way), each inside the card's inset, none over another, the block reaching the
/// inset on the axis that stops it (WR-1..WR-3), its least at the floor (WR-14); a hidden element takes no room.
/// </summary>
public sealed class MusicPlanTests
{
    private const double Tolerance = 0.01;
    private static readonly string Template = MusicPlan.TimeTemplate(TimeSpan.FromSeconds(222));

    private static readonly MusicStyle[] Styles = Enum.GetValues<MusicStyle>();

    /// <summary>The sets the tests walk: all, each element hidden alone, and the few sets the owner named.</summary>
    public static TheoryData<MusicStyle, string> Sets()
    {
        var data = new TheoryData<MusicStyle, string>();
        var sets = new List<string> { "all" };
        sets.AddRange(MusicElements.All.Select(e => $"-{e}"));
        sets.AddRange(["cover", "title", "playPause", "title,artist", "elapsed,bar,length", "previous,playPause,next", "cover,title,playPause",
            "title,artist,album,previous,playPause,next", "cover,elapsed,bar,length", "elapsed,length", "bar", "length"]);
        foreach (var style in Styles)
        {
            foreach (var set in sets)
            {
                data.Add(style, set);
            }
        }

        return data;
    }

    private static List<MusicElement> Parse(string set)
    {
        if (set == "all")
        {
            return [.. MusicElements.All];
        }

        if (set.StartsWith('-'))
        {
            var hidden = Enum.Parse<MusicElement>(set[1..], ignoreCase: true);
            return [.. MusicElements.All.Where(e => e != hidden)];
        }

        return [.. set.Split(',').Select(n => Enum.Parse<MusicElement>(n, ignoreCase: true))];
    }

    /// <summary>The parts an element set draws in a style, whatever the size.</summary>
    private static HashSet<MusicPart> Expected(MusicStyle style, IReadOnlyCollection<MusicElement> shown)
    {
        bool Has(MusicElement e) => shown.Contains(e);
        var parts = new HashSet<MusicPart>();
        var column = shown.Any(e => e != MusicElement.Cover);
        if (!column)
        {
            parts.Add(MusicPart.Cover);
            return parts;
        }

        if (Has(MusicElement.Cover) && style != MusicStyle.Strip)
        {
            parts.Add(MusicPart.Cover);
            if (style == MusicStyle.FullArt)
            {
                parts.Add(MusicPart.Scrim);
            }
        }

        var sub = Has(MusicElement.Artist) || Has(MusicElement.Album);
        if (style == MusicStyle.Strip)
        {
            if (Has(MusicElement.Title) || sub)
            {
                parts.Add(MusicPart.Line);
            }
        }
        else
        {
            if (Has(MusicElement.Title))
            {
                parts.Add(MusicPart.Title);
            }

            if (sub)
            {
                parts.Add(MusicPart.Subtitle);
            }
        }

        foreach (var (element, part) in new[]
        {
            (MusicElement.Elapsed, MusicPart.Elapsed), (MusicElement.Bar, MusicPart.Bar), (MusicElement.Length, MusicPart.Length),
            (MusicElement.Previous, MusicPart.Previous), (MusicElement.PlayPause, MusicPart.PlayPause), (MusicElement.Next, MusicPart.Next),
        })
        {
            if (Has(element))
            {
                parts.Add(part);
            }
        }

        if (Has(MusicElement.Elapsed) && Has(MusicElement.Length) && !Has(MusicElement.Bar))
        {
            parts.Add(MusicPart.Slash);
        }

        return parts;
    }

    /// <summary>The boxes a set is checked in: its least, its natural, its large, and boxes off its proportions.</summary>
    private static IEnumerable<SizeDip> Boxes(WidgetSizing sizing)
    {
        var n = sizing.Natural;
        yield return sizing.Min;
        yield return n;
        yield return SizeStretch.Large(sizing);
        yield return new(n.Width * 3, n.Height);
        yield return new(n.Width, n.Height * 3);
        yield return new(Math.Max(sizing.Min.Width, n.Width * 0.8), n.Height * 1.4);
        yield return new(900, 300);
        yield return new(350, 70);
        yield return new(520, 600);
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void S16502_every_shown_element_is_drawn_in_the_inset_at_every_size_and_none_over_another(MusicStyle style, string set)
    {
        var shown = Parse(set);
        var sizing = SizeStretch.Relax(MusicPlan.Sizing(style, shown));
        Assert.Empty(sizing.Problems());
        var expected = Expected(style, shown);
        foreach (var box in Boxes(sizing).Select(sizing.Fit))
        {
            var laid = MusicPlan.Lay(style, shown, box.Width, box.Height, Template);
            Assert.True(expected.SetEquals(laid.Parts.Keys), $"{style} {set} {box}: {string.Join(",", laid.Parts.Keys)} for {string.Join(",", expected)}");

            foreach (var (part, b) in laid.Parts)
            {
                var band = part is MusicPart.Scrim || (part == MusicPart.Cover && laid.Band);
                var inset = band ? 0 : MusicPlan.Inset;
                Assert.True(b.X >= inset - Tolerance && b.Y >= inset - Tolerance && b.Right <= box.Width - inset + Tolerance && b.Bottom <= box.Height - inset + Tolerance,
                    $"{style} {set} {box}: {part} {b} out of the inset");
                Assert.True(b.Width > 0 && b.Height > 0, $"{style} {set} {box}: {part} empty");
            }

            var drawn = laid.Parts.Where(p => p.Key is not (MusicPart.Scrim or MusicPart.Cover) || !laid.Band).ToList();
            foreach (var (a, ab) in drawn)
            {
                foreach (var (b, bb) in drawn.Where(p => p.Key > a))
                {
                    var across = Math.Min(ab.Right, bb.Right) - Math.Max(ab.X, bb.X);
                    var down = Math.Min(ab.Bottom, bb.Bottom) - Math.Max(ab.Y, bb.Y);
                    Assert.False(across > Tolerance && down > Tolerance, $"{style} {set} {box}: {a} {ab} over {b} {bb}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void S16502_the_natural_box_is_scale_one_the_least_the_floor_and_the_block_reaches_the_inset(MusicStyle style, string set)
    {
        var shown = Parse(set);
        var sizing = SizeStretch.Relax(MusicPlan.Sizing(style, shown));
        var natural = MusicPlan.Lay(style, shown, sizing.Natural.Width, sizing.Natural.Height, Template);
        if (natural.Band)
        {
            Assert.Equal(new MusicBox(0, 0, sizing.Natural.Width, sizing.Natural.Height), natural[MusicPart.Cover]);
            return;
        }

        Assert.InRange(natural.Scale, 1 - 0.02, 1 + 0.02);
        var floor = MusicPlan.Floor(style, shown);
        var least = MusicPlan.Lay(style, shown, sizing.Min.Width, sizing.Min.Height, Template);
        if (sizing.Min.Width > WidgetSizing.LeastScaled && sizing.Min.Height > WidgetSizing.LeastScaled)
        {
            Assert.InRange(least.Scale, floor - 0.02, floor + 0.02);
        }

        // [WR-14] The floor: a time's Caption at 9, else the Body text at 9; a button's glyph at 12.
        Assert.True(floor * 16 >= 12 - Tolerance || !shown.Any(e => e is MusicElement.Previous or MusicElement.PlayPause or MusicElement.Next));

        // [WR-1..WR-3] On the axis that stops it the block reaches the inset at both ends; centred on the other.
        foreach (var box in Boxes(sizing).Select(sizing.Fit))
        {
            var laid = MusicPlan.Lay(style, shown, box.Width, box.Height, Template);
            var ink = laid.Parts.Where(p => p.Key != MusicPart.Scrim && !(p.Key == MusicPart.Cover && laid.Band)).Select(p => p.Value).ToList();
            var (left, top) = (ink.Min(b => b.X), ink.Min(b => b.Y));
            var (right, bottom) = (box.Width - ink.Max(b => b.Right), box.Height - ink.Max(b => b.Bottom));
            if (laid.Band)
            {
                // Full art: the column stands on the foot's inset across the card.
                Assert.InRange(bottom, MusicPlan.Inset - Tolerance, MusicPlan.Inset + Tolerance);
                Assert.InRange(left, MusicPlan.Inset - Tolerance, MusicPlan.Inset + Tolerance);
                continue;
            }

            var tall = Math.Abs(top - MusicPlan.Inset) < 0.5 && Math.Abs(bottom - MusicPlan.Inset) < 0.5;
            var wide = Math.Abs(left - MusicPlan.Inset) < 0.5;
            Assert.True(tall || wide, $"{style} {set} {box}: the block reaches neither axis ({left:0.0} {top:0.0} {right:0.0} {bottom:0.0})");
            if (!tall)
            {
                Assert.InRange(top - bottom, -0.5, 0.5);
            }

            // Where the height stops it, a line or a bar takes the width the scale leaves: the block reaches the far inset too.
            var elastic = shown.Any(e => e is MusicElement.Title or MusicElement.Artist or MusicElement.Album or MusicElement.Bar);
            if (!(style == MusicStyle.Stacked && laid.Has(MusicPart.Cover)) && elastic)
            {
                Assert.InRange(right, MusicPlan.Inset - 0.5, MusicPlan.Inset + 0.5);
            }

            if (style == MusicStyle.Stacked && laid.Has(MusicPart.Cover) && !wide)
            {
                Assert.InRange(left - right, -0.5, 0.5);
            }
        }
    }

    [Fact]
    public void S16502_hiding_an_element_closes_its_room()
    {
        foreach (var style in Styles)
        {
            var all = MusicPlan.Sizing(style, MusicElements.All);
            foreach (var hidden in MusicElements.All)
            {
                var less = MusicPlan.Sizing(style, [.. MusicElements.All.Where(e => e != hidden)]);
                Assert.True(less.Natural.Width <= all.Natural.Width + Tolerance && less.Natural.Height <= all.Natural.Height + Tolerance, $"{style} -{hidden}");
                Assert.True(less.Min.Width <= all.Min.Width + Tolerance && less.Min.Height <= all.Min.Height + Tolerance, $"{style} -{hidden}");
            }
        }

        // A line, a group or a cover hidden shrinks the face: the title's line, the progress line, the transport, the cover.
        var side = MusicPlan.Sizing(MusicStyle.Side, MusicElements.All);
        Assert.Equal(side.Natural.Height - 20, MusicPlan.Sizing(MusicStyle.Side, [.. MusicElements.All.Where(e => e != MusicElement.Title)]).Natural.Height);
        var noProgress = MusicElements.All.Where(e => e is not (MusicElement.Elapsed or MusicElement.Bar or MusicElement.Length)).ToList();
        Assert.Equal(side.Natural.Height - 16 - 4 + 8 - 8, MusicPlan.Sizing(MusicStyle.Side, noProgress).Natural.Height);
        var noCover = MusicElements.All.Where(e => e != MusicElement.Cover).ToList();
        Assert.Equal(side.Natural.Width - 96 - 12, MusicPlan.Sizing(MusicStyle.Side, noCover).Natural.Width);
    }

    [Fact]
    public void S16503_the_side_cover_is_as_tall_as_the_column_at_its_left()
    {
        var sizing = SizeStretch.Relax(MusicPlan.Sizing(MusicStyle.Side, MusicElements.All));

        // The inset, the title, the artist line, 8, the progress line, 4, the transport, the inset: 120; the cover 96.
        Assert.Equal(new SizeDip(12 + 96 + 12 + 220 + 12, 12 + 20 + 20 + 8 + 16 + 4 + 28 + 12), sizing.Natural);
        foreach (var (w, h) in new[] { (352.0, 120.0), (900.0, 300.0), (350.0, 96.0), (600.0, 400.0) })
        {
            var laid = MusicPlan.Lay(MusicStyle.Side, MusicElements.All, w, h, Template);
            var cover = laid[MusicPart.Cover];
            Assert.Equal(cover.Width, cover.Height, 6);
            Assert.Equal(MusicPlan.Inset, cover.X, 6);
            Assert.Equal(cover.Y, laid[MusicPart.Title].Y, 6);
            Assert.Equal(cover.Bottom, laid[MusicPart.PlayPause].Bottom, 6);
            Assert.True(laid[MusicPart.Title].X > cover.Right);
        }
    }

    [Fact]
    public void S16504_stacked_keeps_its_proportions_the_cover_a_square_over_the_column()
    {
        foreach (var (w, h) in new[] { (184.0, 292.0), (520.0, 600.0), (900.0, 300.0) })
        {
            var laid = MusicPlan.Lay(MusicStyle.Stacked, MusicElements.All, w, h, Template);
            var cover = laid[MusicPart.Cover];
            Assert.Equal(cover.Width, cover.Height, 6);
            Assert.Equal(cover.Width, laid[MusicPart.Title].Width, 6);
            Assert.True(laid[MusicPart.Title].Y > cover.Bottom);

            // The transport stands centred under the cover.
            var transport = (laid[MusicPart.Previous].X + laid[MusicPart.Next].Right) / 2;
            Assert.Equal(cover.X + (cover.Width / 2), transport, 6);
        }
    }

    [Fact]
    public void S16505_full_art_the_cover_is_the_band_and_the_column_stands_over_the_scrim_at_its_foot()
    {
        foreach (var (w, h) in new[] { (240.0, 240.0), (900.0, 300.0), (300.0, 600.0) })
        {
            var laid = MusicPlan.Lay(MusicStyle.FullArt, MusicElements.All, w, h, Template);
            Assert.True(laid.Band);
            Assert.Equal(new MusicBox(0, 0, w, h), laid[MusicPart.Cover]);
            var scrim = laid[MusicPart.Scrim];
            Assert.Equal(h, scrim.Bottom, 6);
            Assert.True(scrim.Y < laid[MusicPart.Title].Y);
            Assert.Equal(h - MusicPlan.Inset, laid[MusicPart.PlayPause].Bottom, 6);

            // The art keeps its room over the column: at least the plan's at the scale drawn.
            Assert.True(laid[MusicPart.Title].Y >= MusicPlan.Inset + (MusicPlan.ArtRoom * laid.Scale) - Tolerance);
        }
    }

    [Fact]
    public void S16506_the_strip_is_one_line_centred_down_with_no_cover()
    {
        var sizing = SizeStretch.Relax(MusicPlan.Sizing(MusicStyle.Strip, MusicElements.All));
        Assert.Equal(12 + 28 + 12, sizing.Natural.Height);
        var laid = MusicPlan.Lay(MusicStyle.Strip, MusicElements.All, 600, 60, Template);
        Assert.False(laid.Has(MusicPart.Cover));
        var middle = laid[MusicPart.PlayPause].Y + (laid[MusicPart.PlayPause].Height / 2);
        foreach (var part in new[] { MusicPart.Line, MusicPart.Elapsed, MusicPart.Bar, MusicPart.Length, MusicPart.Previous, MusicPart.Next })
        {
            Assert.Equal(middle, laid[part].Y + (laid[part].Height / 2), 6);
        }

        Assert.True(laid[MusicPart.Line].Right < laid[MusicPart.Elapsed].X && laid[MusicPart.Length].Right < laid[MusicPart.Previous].X);
    }

    [Fact]
    public void S16507_a_cover_alone_fills_the_card_and_goes_down_to_the_grids_least()
    {
        foreach (var style in Styles)
        {
            var sizing = SizeStretch.Relax(MusicPlan.Sizing(style, [MusicElement.Cover]));
            Assert.Equal(new SizeDip(MusicPlan.CoverSide, MusicPlan.CoverSide), sizing.Natural);
            Assert.Equal(new SizeDip(WidgetSizing.LeastScaled, WidgetSizing.LeastScaled), sizing.Min);
            var laid = MusicPlan.Lay(style, [MusicElement.Cover], 300, 200, Template);
            Assert.True(laid.Band);
            Assert.Equal(new MusicBox(0, 0, 300, 200), Assert.Single(laid.Parts).Value);
        }
    }

    [Fact]
    public void S16510_the_times_hold_their_template_and_the_length_ends_on_the_line()
    {
        // WR-15: the elapsed time and the length stand in the template's box whatever they say, the length at the line's end.
        Assert.Equal("8:88", MusicPlan.TimeTemplate(TimeSpan.FromSeconds(222)));
        Assert.Equal("88:88", MusicPlan.TimeTemplate(TimeSpan.FromMinutes(12)));
        Assert.Equal("8:88:88", MusicPlan.TimeTemplate(TimeSpan.FromSeconds(3750)));
        Assert.Equal("88:88", MusicPlan.TimeTemplate(TimeSpan.Zero));
        Assert.True(MusicPlan.TimeWidth("8:88:88") > MusicPlan.TimeWidth("8:88"));

        foreach (var style in Styles)
        {
            var laid = MusicPlan.Lay(style, MusicElements.All, 600, 300, Template);
            var elapsed = laid[MusicPart.Elapsed];
            var length = laid[MusicPart.Length];
            Assert.Equal(MusicPlan.TimeWidth(Template) * laid.Scale, length.Width, 6);
            Assert.Equal(elapsed.Width, length.Width, 6);
            Assert.Equal(laid[MusicPart.Bar].Right + (MusicPlan.TimeGap * laid.Scale), length.X, 6);
        }

        // The box holds the widest figures at Caption: 0.58 em a figure, the colon 0.25, and the slack.
        Assert.True(MusicPlan.TimeWidth("8:88") >= Math.Ceiling(((3 * 0.58) + 0.25) * 12));
    }

    [Fact]
    public void S16502_the_large_check_box_draws_the_same_parts_larger()
    {
        foreach (var style in Styles)
        {
            var sizing = SizeStretch.Relax(MusicPlan.Sizing(style, MusicElements.All));
            var natural = MusicPlan.Lay(style, MusicElements.All, sizing.Natural.Width, sizing.Natural.Height, Template);
            var large = SizeStretch.Large(sizing);
            var big = MusicPlan.Lay(style, MusicElements.All, large.Width, large.Height, Template);
            Assert.Equal(natural.Parts.Keys.Order(), big.Parts.Keys.Order());
            Assert.True(big.Scale > natural.Scale, $"{style}");

            // The natural's shape drawn larger keeps its proportions: a line is as long at the large box as at the natural
            // (the strip at twice its natural had cut its title to its least while the face grew taller).
            var (line, length) = style switch
            {
                MusicStyle.Strip => (MusicPart.Line, MusicPlan.LineNatural),
                MusicStyle.Stacked => (MusicPart.Title, MusicPlan.StackSide),
                MusicStyle.FullArt => (MusicPart.Title, MusicPlan.ArtColumn),
                _ => (MusicPart.Title, MusicPlan.ColumnNatural),
            };
            Assert.True(big[line].Width / big.Scale >= length - 0.5, $"{style}: {big[line].Width / big.Scale}");
        }
    }
}
