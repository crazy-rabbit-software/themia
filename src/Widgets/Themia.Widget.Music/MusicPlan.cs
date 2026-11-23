using System.Globalization;
using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Music;

/// <summary>[S-16508] What Now playing's texts and times say, pure: the track's, or its face kept with nothing to play (WR-10).</summary>
public static class MusicWords
{
    /// <summary>[S-16508] A reading it does not have (WR-10: "its reading as '--'").</summary>
    public const string NoReading = "--";

    /// <summary>[S-16508] A time it does not have, in a time's own shape.</summary>
    public const string NoTime = "-:--";

    /// <summary>
    /// [S-16508] The title and the line under it: the track's title and its artist and album, whichever of the two shows; with
    /// no track "--" in the title's place and why in the line under it (WR-10: "says why in a compact line"), or why in the
    /// title's place where no line under it shows.
    /// </summary>
    public static (string Title, string Subtitle) Lines(NowPlaying now, bool artist, bool album, bool subtitleShown)
    {
        if (now.HasTrack)
        {
            var sub = artist && album ? now.Subtitle : MusicRules.Subtitle(artist ? now.Artist : null, album ? now.Album : null);
            return (now.Title, sub);
        }

        var why = MusicRules.StatusMessage(now.State);
        return subtitleShown ? (NoReading, why) : (why, string.Empty);
    }

    /// <summary>[S-16508] The elapsed time and the length: the track's (a stream with no end has no length), or none with no track.</summary>
    public static (string Elapsed, string Length) Times(NowPlaying now, TimeSpan position)
        => !now.HasTrack ? (NoTime, NoTime)
            : (MusicRules.Clock(position), now.Timeline.Duration > TimeSpan.Zero ? MusicRules.Clock(now.Timeline.Duration) : NoTime);

    /// <summary>
    /// [S-11321] What the cover's picture was read for: the track and the reading of the session's media properties
    /// (<see cref="NowPlaying.Media"/>). Another track, or the same track's properties changed (a browser hands over a video's
    /// picture after its title, and may still hold the last video's then), is another key: the picture is read again.
    /// Nothing playing: no key.
    /// </summary>
    public static string ArtKey(NowPlaying now)
        => now.HasTrack ? string.Create(CultureInfo.InvariantCulture, $"{now.Media}\n{now.TrackKey}") : string.Empty;
}

/// <summary>[S-16502] A part of Now playing's face as the plan lays it.</summary>
public enum MusicPart
{
    /// <summary>The cover: a square beside or over the texts, or the card's whole box (a band: Full art, a cover alone).</summary>
    Cover,

    /// <summary>[S-16505] Full art's scrim: the card's ground fading in over the art under the texts and controls.</summary>
    Scrim,

    /// <summary>The track's title (BodyStrong).</summary>
    Title,

    /// <summary>The artist and the album in one line (Body, secondary), whichever of them shows.</summary>
    Subtitle,

    /// <summary>[S-16506] The strip's one line: the title, then the artist and the album.</summary>
    Line,

    /// <summary>The elapsed time (Caption, tertiary), held to its template (WR-15).</summary>
    Elapsed,

    /// <summary>The seek bar.</summary>
    Bar,

    /// <summary>The "/" between the two times where the bar is hidden.</summary>
    Slash,

    /// <summary>The track's length (Caption, tertiary), held to its template (WR-15).</summary>
    Length,

    Previous,
    PlayPause,
    Next,
}

/// <summary>[S-16502] Where a part stands, in the card's DIPs as drawn (its scale applied).</summary>
public readonly record struct MusicBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>
/// [S-16502] One laid face: the scale its texts and controls are drawn at (each part's own size times it), each shown
/// part's box in the card, and whether the cover is the card's band (it fills the card, the rest over it).
/// </summary>
public sealed record MusicLayout(double Scale, IReadOnlyDictionary<MusicPart, MusicBox> Parts, bool Band)
{
    public bool Has(MusicPart part) => Parts.ContainsKey(part);

    public MusicBox this[MusicPart part] => Parts[part];
}

/// <summary>
/// [S-16502] Now playing's one plan: the shown set and the style decide the face and its sizing, pure. Every style is one
/// block in the card's inset (12, which never scales), drawn at one scale: as large as the card's height allows, held by
/// the width at the block's natural proportions (so a larger card of the natural's shape draws the same face larger, its
/// lines as long), and only at the floor by its least width (a column of texts, the progress line and the transport never
/// narrower than <see cref="ColumnLeast"/>); the texts and the bar take the width the scale leaves (so the block reaches the
/// inset on both axes, WR-1..WR-3), and it stands centred down where the width stops it ("Centred is fine"). Nothing gives way (WR-7): every shown element is drawn at
/// every size, and the least card is the whole block at the legibility floor (<see cref="Floor"/>, WR-14). A hidden element
/// takes no room: its line, its gap and its share of the least and natural boxes go with it.
/// <list type="bullet">
/// <item>[S-16503] Side cover: the cover at the left as tall as the column, 12 from it, the column beside it.</item>
/// <item>[S-16504] Stacked: the cover a square over the column, as wide as it; the block keeps its proportions.</item>
/// <item>[S-16505] Full art: the cover the card's band; the column at its foot over the scrim, the art's room over it.</item>
/// <item>[S-16506] Strip: one line, the title and artist, the progress, the transport; no cover.</item>
/// <item>[S-16507] A cover alone (nothing else shown, or a style with nothing else to draw): the card's band.</item>
/// </list>
/// The column, top to bottom: the title, the artist and album, 8, the progress line (the elapsed time, 8, the bar, 8, the
/// length; without the bar the two times with a "/"), 4, the transport (previous, play/pause, next: 28 buttons 8 apart).
/// </summary>
public static class MusicPlan
{
    /// <summary>The card's edge to the block, every side: the inset, never scaled.</summary>
    public const double Inset = FamilySpace.Inset;

    /// <summary>From the texts to what stands under them (the progress line, or the transport without it).</summary>
    public const double TextGap = FamilySpace.S2;

    /// <summary>From the progress line to the transport.</summary>
    public const double BarGap = FamilySpace.S1;

    /// <summary>A time to the bar.</summary>
    public const double TimeGap = FamilySpace.S2;

    /// <summary>A time to the "/" between the two where the bar is hidden.</summary>
    public const double SlashGap = FamilySpace.S1;

    /// <summary>The cover to the column beside it, and the strip's groups to each other.</summary>
    public const double ColumnGap = FamilySpace.ColumnGap;

    /// <summary>The least width the column's texts keep: about a dozen letters of a title.</summary>
    public const double ColumnLeast = 120;

    /// <summary>A new card's column: a title and its artist whole in most cases.</summary>
    public const double ColumnNatural = 220;

    /// <summary>[S-16504] The stacked block's width: the cover's side and the column's.</summary>
    public const double StackSide = 160;

    /// <summary>[S-16505] Full art's room for the art over the column on a new card, and its column there.</summary>
    public const double ArtRoom = 120, ArtColumn = 216;

    /// <summary>
    /// [S-16505] The most of Full art's height its column takes: it grows as one block until the width or this share stops it,
    /// the art keeping the third over it in sight.
    /// </summary>
    public const double FootShare = 2.0 / 3.0;

    /// <summary>
    /// [S-16502] WR-14: the least card is the block at the floor with a pixel to spare (a DIP at 150 % is a pixel and a half,
    /// and a card is drawn on whole pixels), so no part is ever drawn under the floor.
    /// </summary>
    public const double LeastSlack = 1;

    /// <summary>[S-16510] The widest time a track has: an hour or more ("8:88:88"); the least card holds it at the floor.</summary>
    public static string WidestTemplate => TimeTemplate(TimeSpan.FromHours(1));

    /// <summary>[S-16505] How far over the column the scrim starts fading in.</summary>
    public const double ScrimLead = FamilySpace.S6;

    /// <summary>[S-16505] How strong the card's ground is under the column: the art still shows through it, the texts read on it.</summary>
    public const double ScrimStrength = 0.85;

    /// <summary>[S-16506] The strip's line: its least and its natural width; its bar beside the line.</summary>
    public const double LineLeast = 80, LineNatural = 180, StripBar = 96;

    /// <summary>The bar's least where it takes the room left (no texts beside it).</summary>
    public const double BarLeast = 40;

    /// <summary>[S-16507] A cover alone on a new card: a square of 160.</summary>
    public const double CoverSide = 160;

    /// <summary>The transport's buttons, 8 apart.</summary>
    public const double Button = FamilyShape.IconButton, ButtonGap = ControlsRules.Gap;

    private static double TitleLine => FamilyType.BodyStrong.LineHeight;

    private static double SubLine => FamilyType.Body.LineHeight;

    private static double TimeLine => FamilyType.Caption.LineHeight;

    /// <summary>
    /// [S-16510] A time's template (WR-15, owner 2026-11-20: "the clock right now changes its layout depending on the time
    /// that it shows"): the track's length with every figure an 8; a length past ten minutes or unknown (a stream) "88:88".
    /// The elapsed time and the length both stand in it, so neither moves nor is cut as the seconds go by.
    /// </summary>
    public static string TimeTemplate(TimeSpan duration)
    {
        var shown = duration > TimeSpan.Zero ? MusicRules.Clock(duration) : "88:88";
        return new string([.. shown.Select(c => char.IsDigit(c) ? '8' : c)]);
    }

    /// <summary>[S-16510] A time's box: its template in Caption (its widest case) and a slack of 2 for the font's own rounding.</summary>
    public static double TimeWidth(string template)
        => Math.Ceiling(HeroFit.Ems(string.IsNullOrEmpty(template) ? "8:88" : template) * FamilyType.Caption.FontSize) + TimeSlack;

    /// <summary>[S-16510] What a time's box holds past its template's estimate: the estimate is close, never a measure.</summary>
    public const double TimeSlack = 2;

    /// <summary>The "/" between the times.</summary>
    private static double SlashWidth => Math.Ceiling(HeroFit.Ems("/") * FamilyType.Caption.FontSize);

    /// <summary>[S-16502] What shows of each part, from the stored set and the style.</summary>
    private readonly record struct Shown(MusicStyle Style, bool Cover, bool Title, bool Sub, bool Elapsed, bool Bar, bool Length, bool Previous, bool Play, bool Next)
    {
        public static Shown Of(MusicStyle style, IReadOnlyCollection<MusicElement> set)
        {
            bool Has(MusicElement e) => set.Contains(e);
            return new(style, Has(MusicElement.Cover) && style != MusicStyle.Strip, Has(MusicElement.Title),
                Has(MusicElement.Artist) || Has(MusicElement.Album), Has(MusicElement.Elapsed), Has(MusicElement.Bar),
                Has(MusicElement.Length), Has(MusicElement.Previous), Has(MusicElement.PlayPause), Has(MusicElement.Next));
        }

        public bool Text => Title || Sub;

        public bool Progress => Elapsed || Bar || Length;

        public int Buttons => (Previous ? 1 : 0) + (Play ? 1 : 0) + (Next ? 1 : 0);

        public bool Transport => Buttons > 0;

        public bool Column => Text || Progress || Transport;

        /// <summary>[S-16507] The cover alone: the card's band.</summary>
        public bool BandOnly => !Column && (Cover || Style == MusicStyle.Strip);
    }

    private static double TransportWidth(int buttons) => buttons <= 0 ? 0 : (buttons * Button) + ((buttons - 1) * ButtonGap);

    /// <summary>[S-16502] The column's height: its lines and the gaps between the groups shown.</summary>
    private static double ColumnHeight(Shown s)
    {
        var h = (s.Title ? TitleLine : 0) + (s.Sub ? SubLine : 0);
        if (s.Progress)
        {
            h += (s.Text ? TextGap : 0) + TimeLine;
        }

        if (s.Transport)
        {
            h += (s.Progress ? BarGap : s.Text ? TextGap : 0) + Button;
        }

        return h;
    }

    /// <summary>The progress line's least width: the times and the bar at its least (or the "/" between the times).</summary>
    private static double ProgressLeast(Shown s, double time, double bar)
    {
        if (!s.Progress)
        {
            return 0;
        }

        if (s.Bar)
        {
            return (s.Elapsed ? time + TimeGap : 0) + bar + (s.Length ? TimeGap + time : 0);
        }

        return s.Elapsed && s.Length ? (2 * time) + (2 * SlashGap) + SlashWidth : time;
    }

    /// <summary>[S-16502] The column's least width: its texts', its progress line's and its transport's.</summary>
    private static double ColumnLeastOf(Shown s, double time)
        => Math.Max(Math.Max(s.Text ? ColumnLeast : 0, ProgressLeast(s, time, BarLeast)), TransportWidth(s.Buttons));

    /// <summary>
    /// [S-16502] The column on a new card where texts or a bar take width (else its least): the style's own (the side cover's
    /// <see cref="ColumnNatural"/>, the stacked block's side, Full art's column), so a hidden cover never widens the card.
    /// </summary>
    private static double ColumnNaturalOf(Shown s, double time)
    {
        var natural = s.Style switch
        {
            MusicStyle.Stacked => StackSide,
            MusicStyle.FullArt => ArtColumn,
            _ => ColumnNatural,
        };
        return Math.Max(s.Text || s.Bar ? natural : 0, ColumnLeastOf(s, time));
    }

    /// <summary>[S-16506] The strip's line height: its tallest part's.</summary>
    private static double RowHeight(Shown s)
        => Math.Max(Math.Max(s.Text ? SubLine : 0, s.Progress ? TimeLine : 0), s.Transport ? Button : 0);

    /// <summary>[S-16506] The strip's width, its line and its bar at their least or natural.</summary>
    private static double RowWidth(Shown s, double time, bool natural)
    {
        var groups = new List<double>();
        if (s.Text)
        {
            groups.Add(natural ? LineNatural : LineLeast);
        }

        if (s.Progress)
        {
            groups.Add(ProgressLeast(s, time, s.Text || natural ? StripBar : BarLeast));
        }

        if (s.Transport)
        {
            groups.Add(TransportWidth(s.Buttons));
        }

        return groups.Sum() + (Math.Max(0, groups.Count - 1) * ColumnGap);
    }

    /// <summary>
    /// [S-16502] The least scale the shown parts are drawn at: the smallest text at 9 (a time's Caption, else the
    /// texts' Body), a button's 16 glyph at 12, the bar at the times' scale; 1 where nothing scales (a cover alone).
    /// </summary>
    public static double Floor(MusicStyle style, IReadOnlyCollection<MusicElement> shown)
    {
        ArgumentNullException.ThrowIfNull(shown);
        var s = Shown.Of(style, shown);
        var text = s.Progress ? FamilyType.Caption.FontSize : s.Text ? FamilyType.Body.FontSize : 0;
        return ScaleFloor.Least(text, s.Transport ? FamilyShape.Icon : 0);
    }

    /// <summary>
    /// [S-16502] The sizing ([S-2438]) of a face of <paramref name="style"/> showing <paramref name="shown"/>: its natural box
    /// the block at scale 1 with its column's natural width, its least the block at the floor with its least width, both in the
    /// inset; a cover alone a square of 160 down to the grid's least widget. A hidden element shrinks both.
    /// </summary>
    public static WidgetSizing Sizing(MusicStyle style, IReadOnlyCollection<MusicElement> shown)
    {
        ArgumentNullException.ThrowIfNull(shown);
        var s = Shown.Of(style, shown);
        if (s.BandOnly || !s.Column)
        {
            var corner = new SizeDip(WidgetSizing.LeastScaled, WidgetSizing.LeastScaled);
            var side = new SizeDip(CoverSide, CoverSide);
            return WidgetSizing.Range(corner, side, side) with { Room = StretchRoom.Picture };
        }

        // The natural for a track of minutes; the least for the widest track there is (WR-14: an hour-long one's "1:02:30"
        // drawn at the floor too), a pixel to spare.
        var (natural, _) = Blocks(s, TimeWidth(TimeTemplate(TimeSpan.Zero)));
        var (_, least) = Blocks(s, TimeWidth(WidestTemplate));
        var floor = Floor(style, shown);
        static double Box(double block) => block + (2 * Inset);
        static double Least(double block) => Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(Box(block) + LeastSlack));
        var min = new SizeDip(Least(least.Width * floor), Least(least.Height * floor));
        // A face of one time alone is as wide as the widest time at 1: the least never stands over the natural.
        var nat = new SizeDip(Math.Ceiling(Box(Math.Max(natural.Width, least.Width))), Math.Ceiling(Box(Math.Max(natural.Height, least.Height))));
        return WidgetSizing.Range(min, nat, nat);
    }

    /// <summary>[S-16502] The block's natural and least box at scale 1, by the style (no cover: the column alone).</summary>
    private static (SizeDip Natural, SizeDip Least) Blocks(Shown s, double time)
    {
        var hc = ColumnHeight(s);
        var cl = ColumnLeastOf(s, time);
        var cn = ColumnNaturalOf(s, time);
        if (s.Style == MusicStyle.Strip)
        {
            var hr = RowHeight(s);
            return (new(RowWidth(s, time, natural: true), hr), new(RowWidth(s, time, natural: false), hr));
        }

        if (!s.Cover)
        {
            return (new(cn, hc), new(cl, hc));
        }

        return s.Style switch
        {
            MusicStyle.Stacked => (new(StackSide, StackSide + ColumnGap + hc), new(StackSide, StackSide + ColumnGap + hc)),
            MusicStyle.FullArt => (new(Math.Max(ArtColumn, cl), ArtRoom + hc), new(cl, hc / FootShare)),
            _ => (new(hc + ColumnGap + cn, hc), new(hc + ColumnGap + cl, hc)),
        };
    }

    /// <summary>
    /// [S-16502] The face of <paramref name="style"/> showing <paramref name="shown"/> in a card <paramref name="width"/> ×
    /// <paramref name="height"/> (the view's whole box, its top edge included), its times held to
    /// <paramref name="timeTemplate"/>.
    /// </summary>
    public static MusicLayout Lay(MusicStyle style, IReadOnlyCollection<MusicElement> shown, double width, double height, string timeTemplate)
    {
        ArgumentNullException.ThrowIfNull(shown);
        var s = Shown.Of(style, shown);
        var parts = new Dictionary<MusicPart, MusicBox>();
        if (!(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return new MusicLayout(1, parts, false);
        }

        if (s.BandOnly || !s.Column)
        {
            if (s.Cover || s.Style == MusicStyle.Strip)
            {
                parts[MusicPart.Cover] = new(0, 0, width, height);
            }

            return new MusicLayout(1, parts, true);
        }

        var time = TimeWidth(timeTemplate);
        var (natural, least) = Blocks(s, time);
        var room = new SizeDip(Math.Max(0, width - (2 * Inset)), Math.Max(0, height - (2 * Inset)));
        var hc = ColumnHeight(s);

        // One scale: the height's, held by the width at the block's own proportions (its natural width), so a card of the
        // natural's shape draws the natural face larger, its lines as long; only at the floor does a narrower card take the
        // texts' and the bar's room down to their least. The block then fills the width the scale leaves.
        var floor = Floor(style, shown);
        var byWidth = Math.Max(room.Width / natural.Width, Math.Min(room.Width / least.Width, floor));
        var scale = Math.Min(room.Height / least.Height, byWidth);
        if (!(scale > 0) || !double.IsFinite(scale))
        {
            return new MusicLayout(1, parts, false);
        }

        var wide = room.Width / scale;
        if (s.Style == MusicStyle.Strip)
        {
            var hr = RowHeight(s);
            Row(s, parts, Inset, Inset + ((room.Height - (hr * scale)) / 2), wide, hr, scale, time);
            return new MusicLayout(scale, parts, false);
        }

        if (!s.Cover)
        {
            Column(s, parts, Inset, Inset + ((room.Height - (hc * scale)) / 2), wide, scale, time, centred: false);
            return new MusicLayout(scale, parts, false);
        }

        switch (s.Style)
        {
            case MusicStyle.Stacked:
            {
                // [S-16504] The block keeps its proportions: centred on the axis that does not stop it.
                var block = new SizeDip(StackSide * scale, (StackSide + ColumnGap + hc) * scale);
                var x = Inset + ((room.Width - block.Width) / 2);
                var y = Inset + ((room.Height - block.Height) / 2);
                parts[MusicPart.Cover] = new(x, y, block.Width, StackSide * scale);
                Column(s, parts, x, y + ((StackSide + ColumnGap) * scale), StackSide, scale, time, centred: true);
                return new MusicLayout(scale, parts, false);
            }

            case MusicStyle.FullArt:
            {
                // [S-16505] The art is the card's; the column stands on the foot's inset, the scrim from over it to the foot.
                var top = height - Inset - (hc * scale);
                parts[MusicPart.Cover] = new(0, 0, width, height);
                var scrim = Math.Max(0, top - (ScrimLead * scale));
                parts[MusicPart.Scrim] = new(0, scrim, width, height - scrim);
                Column(s, parts, Inset, top, wide, scale, time, centred: false);
                return new MusicLayout(scale, parts, true);
            }

            default:
            {
                // [S-16503] The cover as tall as the column, at its left; the column takes the width left.
                var y = Inset + ((room.Height - (hc * scale)) / 2);
                parts[MusicPart.Cover] = new(Inset, y, hc * scale, hc * scale);
                Column(s, parts, Inset + ((hc + ColumnGap) * scale), y, wide - hc - ColumnGap, scale, time, centred: false);
                return new MusicLayout(scale, parts, false);
            }
        }
    }

    /// <summary>A part at (<paramref name="x"/>, <paramref name="y"/>) in the card, its own size times the scale.</summary>
    private static MusicBox At(double x, double y, double width, double height, double scale) => new(x, y, width * scale, height * scale);

    /// <summary>
    /// [S-16502] The column at (<paramref name="x"/>, <paramref name="y"/>), <paramref name="column"/> wide in its own units:
    /// its texts, its progress line, its transport (centred in Stacked).
    /// </summary>
    private static void Column(Shown s, Dictionary<MusicPart, MusicBox> parts, double x, double y, double column, double scale, double time, bool centred)
    {
        var at = 0.0;
        if (s.Title)
        {
            parts[MusicPart.Title] = At(x, y, column, TitleLine, scale);
            at += TitleLine;
        }

        if (s.Sub)
        {
            parts[MusicPart.Subtitle] = At(x, y + (at * scale), column, SubLine, scale);
            at += SubLine;
        }

        if (s.Progress)
        {
            at += s.Text ? TextGap : 0;
            Progress(s, parts, x, y + (at * scale), column, scale, time);
            at += TimeLine;
        }

        if (s.Transport)
        {
            at += s.Progress ? BarGap : s.Text ? TextGap : 0;
            var start = centred ? (column - TransportWidth(s.Buttons)) / 2 : 0;
            Transport(s, parts, x + (start * scale), y + (at * scale), scale);
        }
    }

    /// <summary>
    /// [S-16510] The progress line, <paramref name="line"/> wide: the elapsed time at its start, the length at its end, each
    /// in its template's box (WR-15: nothing moves as the seconds go by; the length is never cut), the bar between them 8 from
    /// each; without the bar the times stand together with a "/" between them.
    /// </summary>
    private static void Progress(Shown s, Dictionary<MusicPart, MusicBox> parts, double x, double y, double line, double scale, double time)
    {
        if (s.Bar)
        {
            var from = s.Elapsed ? time + TimeGap : 0;
            var to = line - (s.Length ? time + TimeGap : 0);
            if (s.Elapsed)
            {
                parts[MusicPart.Elapsed] = At(x, y, time, TimeLine, scale);
            }

            parts[MusicPart.Bar] = At(x + (from * scale), y + ((TimeLine - SeekBarFace.Height) / 2 * scale), Math.Max(0, to - from), SeekBarFace.Height, scale);
            if (s.Length)
            {
                parts[MusicPart.Length] = At(x + ((line - time) * scale), y, time, TimeLine, scale);
            }

            return;
        }

        var at = 0.0;
        if (s.Elapsed)
        {
            parts[MusicPart.Elapsed] = At(x, y, time, TimeLine, scale);
            at += time;
        }

        if (s.Elapsed && s.Length)
        {
            at += SlashGap;
            parts[MusicPart.Slash] = At(x + (at * scale), y, SlashWidth, TimeLine, scale);
            at += SlashWidth + SlashGap;
        }

        if (s.Length)
        {
            parts[MusicPart.Length] = At(x + (at * scale), y, time, TimeLine, scale);
        }
    }

    /// <summary>The transport at (<paramref name="x"/>, <paramref name="y"/>): the shown buttons in their order, 8 apart.</summary>
    private static void Transport(Shown s, Dictionary<MusicPart, MusicBox> parts, double x, double y, double scale)
    {
        var at = 0.0;
        foreach (var (part, on) in new[] { (MusicPart.Previous, s.Previous), (MusicPart.PlayPause, s.Play), (MusicPart.Next, s.Next) })
        {
            if (!on)
            {
                continue;
            }

            parts[part] = At(x + (at * scale), y, Button, Button, scale);
            at += Button + ButtonGap;
        }
    }

    /// <summary>
    /// [S-16506] The strip, <paramref name="row"/> wide and <paramref name="height"/> tall in its own units: the line, the
    /// progress, the transport, 12 apart, each centred down the row; the line takes the width left (no line: the bar).
    /// </summary>
    private static void Row(Shown s, Dictionary<MusicPart, MusicBox> parts, double x, double y, double row, double height, double scale, double time)
    {
        var least = RowWidth(s, time, natural: false);
        var spare = Math.Max(0, row - least);
        var at = 0.0;
        if (s.Text)
        {
            var line = LineLeast + spare;
            parts[MusicPart.Line] = At(x, y + ((height - SubLine) / 2 * scale), line, SubLine, scale);
            at += line + ColumnGap;
        }

        if (s.Progress)
        {
            var bar = s.Text ? StripBar : BarLeast + spare;
            var line = ProgressLeast(s, time, bar);
            Progress(s, parts, x + (at * scale), y + ((height - TimeLine) / 2 * scale), line, scale, time);
            at += line + ColumnGap;
        }

        if (s.Transport)
        {
            Transport(s, parts, x + (at * scale), y + ((height - Button) / 2 * scale), scale);
        }
    }
}
