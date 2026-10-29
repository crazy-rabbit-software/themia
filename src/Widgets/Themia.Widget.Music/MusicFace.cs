using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Music;

/// <summary>[S-1475] Where now playing draws its art: none, a 48 lead beside the lines, a square over them, or edge to edge over the content (Cover).</summary>
public enum MusicArt
{
    None,
    Lead,
    Over,
    Cover,
}

/// <summary>
/// [S-1475] Now playing at one size: the title's step, whether the artist line, the progress row and
/// the transport show, and the art (where, and its size: the side of a lead or a square, the height
/// of the cover band).
/// </summary>
public readonly record struct MusicPlan(TypeStep Title, bool Subtitle, bool Progress, bool Controls, MusicArt Art, double ArtSize);

/// <summary>
/// [S-1475] Now playing on the family's blocks. The lines stand at the content's top: the track
/// on the Title step (BodyStrong on a compact width) and the artist and album in Body, secondary; the
/// transport is the Controls row at the bottom inset (Previous, Play as the one main action, Next); the
/// progress row (the elapsed time, the seek bar, the total, Caption tertiary at its two ends) stands 8
/// over it. Height is spent in that order of need: the title, the transport, the artist, the progress
/// ([S-666]: the track and what to do with it first). The art is data: a 48 lead beside the two lines
/// from a regular width, a square over the lines once the area is expanded in height (at least 96, as
/// large as the room left allows), or, in the Cover style, a band the card's width over everything
/// else with the room left (at least 48). Text only draws none.
/// </summary>
public static class MusicFace
{
    /// <summary>The art beside the two lines: the family's large icon, as tall as Title and Body together.</summary>
    public const double LeadArt = FamilyShape.IconHuge;

    /// <summary>The least square worth standing over the lines; smaller, the lead it is.</summary>
    public const double OverFloor = 96;

    /// <summary>The least cover band; lower, the Cover style draws no art.</summary>
    public const double CoverFloor = FamilyShape.IconHuge;

    /// <summary>The progress row: one Caption line, the seek bar centred in it.</summary>
    public static double ProgressRow => FamilyType.Caption.LineHeight;

    /// <summary>What the transport takes: the Controls row and the 8 over it.</summary>
    public const double ControlsCost = FamilyShape.Button + FamilySpace.S2;

    /// <summary>[S-1475] The plan for a content box <paramref name="width"/> × <paramref name="height"/> (the view less its insets) of a view given <paramref name="area"/>.</summary>
    public static MusicPlan Plan(WidgetArea area, double width, double height, MusicStyle style, bool transport, bool progress, bool subtitle)
    {
        var title = area.WidthClass >= SizeClass.Regular ? FamilyType.Title : FamilyType.BodyStrong;
        var rest = height - title.LineHeight;

        var controls = transport && rest >= ControlsCost;
        if (controls)
        {
            rest -= ControlsCost;
        }

        var artist = subtitle && rest >= FamilyType.Body.LineHeight;
        if (artist)
        {
            rest -= FamilyType.Body.LineHeight;
        }

        var bar = progress && rest >= ProgressRow + FamilySpace.S2;
        if (bar)
        {
            rest -= ProgressRow + FamilySpace.S2;
        }

        var art = MusicArt.None;
        var size = 0.0;
        if (style == MusicStyle.Cover)
        {
            // The band stands 8 over the lines; under its floor the style draws the lines alone.
            var band = Math.Floor(rest - FamilySpace.S2);
            if (band >= CoverFloor)
            {
                art = MusicArt.Cover;
                size = band;
            }
        }
        else if (style == MusicStyle.Artwork)
        {
            var square = Math.Floor(Math.Min(width, rest - FamilySpace.S2));
            if (area.HeightClass >= SizeClass.Expanded && square >= OverFloor)
            {
                art = MusicArt.Over;
                size = square;
            }
            else if (area.WidthClass >= SizeClass.Regular && artist && title == FamilyType.Title)
            {
                art = MusicArt.Lead;
                size = LeadArt;
            }
        }

        return new MusicPlan(title, artist, bar, controls, art, size);
    }

    /// <summary>
    /// [S-1475] How wide a time at the progress row's ends is: the track's length with every figure an
    /// 8 (its widest case), in Caption, so the bar between them does not move as the seconds go by.
    /// </summary>
    public static double TimeWidth(string total)
    {
        var widest = new string([.. (total ?? string.Empty).Select(c => char.IsDigit(c) ? '8' : c)]);
        return Math.Ceiling(HeroFit.Ems(widest.Length == 0 ? "8:88" : widest) * FamilyType.Caption.FontSize);
    }
}
