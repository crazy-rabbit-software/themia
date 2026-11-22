using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-14931] What one read of a text widget puts on its face: the text (empty: the kind's own words for none, drawn
/// unavailable), whether it can be read now (unavailable: drawn in the disabled role, as "Nothing playing"), why there is
/// none (the tooltip), the problems its status glyph says (a sentence kept through a failed refresh) and the app it came
/// from (the player a click opens, [S-14935]).
/// </summary>
public sealed record TextShown(string Text, bool Available = true)
{
    /// <summary>[S-14932] Why there is no text: the face's tooltip while it is unavailable.</summary>
    public string? Why { get; init; }

    /// <summary>[S-14932] The status glyph's problems (a text kept through a failed refresh).</summary>
    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>[S-14935] The app the text came from (a media session's player); null for none.</summary>
    public string? Owner { get; init; }

    /// <summary>[S-14932] No text to show: the kind's own words, unavailable, <paramref name="why"/> its tooltip.</summary>
    public static TextShown None(string? why = null) => new(string.Empty, false) { Why = why };
}

/// <summary>[S-14933] Which text role a text kind's line is drawn in at rest: the reading's own (primary) or a detail's (secondary).</summary>
public enum TextTone
{
    Primary,
    Secondary,
}

/// <summary>
/// [S-14935] What a click on a text widget opens, declared on its kind (<see cref="TextKind.Opens"/>): the target for the
/// card's config and what it shows now (null: nothing now, a place not set, no player) and the card menu's line that
/// opens it.
/// </summary>
public sealed record TextOpen(LocalText Label, Func<WidgetConfig, TextShown, CardOpen?> Target);

/// <summary>[S-14935] The targets the text families open: each the one their family's own widgets open.</summary>
public static class TextOpens
{
    /// <summary>[S-2006] MSN Weather's forecast for the card's place, as the single metrics of the weather open it (<see cref="MetricOpens.Weather"/>).</summary>
    public static TextOpen Weather { get; } = new(MetricOpens.Weather.Label, (config, _) => MetricOpens.Weather.Target(config));

    /// <summary>[S-2030] The app that owns the media session the text came from; nothing with no player.</summary>
    public static TextOpen Player { get; } = new(Str.Text_Open_Player.Local(), (_, shown) => shown.Owner is { Length: > 0 } app ? CardOpen.App(app) : null);
}

/// <summary>
/// [S-14930]..[S-14934] The text widget's rules, pure, one base for every widget whose face is a line of text (owner,
/// 2026-11-21: "for text like the rain in x minutes or the song name or album name make a shared class in the background
/// that all of those widgets use. similar to how the metric one works"): its size (the line or lines on the face's step in
/// the card's inset, scaling down as one to the legibility floor), what it draws without a text, and the width its line
/// takes in a box so that it grows until one axis stops it and a text longer than that is cut with its ellipsis.
/// </summary>
public static class TextRules
{
    /// <summary>[S-14930] The step a text face is planned on: the Title step (20 on a 28 line).</summary>
    public static TypeStep Step => FamilyType.Title;

    /// <summary>[S-14933] The step of a kind's line in <paramref name="tone"/>: the Title step, a detail's regular.</summary>
    public static TypeStep StepOf(TextTone tone) => tone == TextTone.Secondary ? FamilyType.Regular(Step) : Step;

    /// <summary>[S-14930] The narrowest card the content asks for before it scales down: a short line.</summary>
    public const double MinWidth = 160;

    /// <summary>[S-14930] A new card's width.</summary>
    public const double NaturalWidth = 240;

    /// <summary>[S-13921] A short text's new card (a weekday, a temperature): the narrowest a widget is new at ([S-5601]).</summary>
    public const double ShortWidth = 80;

    /// <summary>[S-14930] The widest the content asks for (relaxed past it, [S-12411]).</summary>
    public const double MaxWidth = 420;

    /// <summary>[S-14930] The most lines a kind may wrap to: a sentence, never a paragraph.</summary>
    public const int MostLines = 3;

    /// <summary>[S-14930] A kind's lines, held to one..<see cref="MostLines"/>.</summary>
    public static int Lines(int lines) => Math.Clamp(lines, 1, MostLines);

    /// <summary>[S-14934] The lead glyph's room before the text: the glyph at the step's size and the gap after it.</summary>
    public static double LeadRoom(bool lead) => lead ? Step.FontSize + FamilySpace.GlyphGap : 0;

    /// <summary>
    /// [S-14930] The face's ink at its own step, <paramref name="lines"/> lines tall: the lines less the leading over the
    /// first's capitals and under the last's baseline.
    /// </summary>
    public static double Ink(int lines)
    {
        var ink = LineInk.Of(Step);
        return Math.Max(1, (Lines(lines) * Step.LineHeight) - ink.Over - ink.Under);
    }

    /// <summary>
    /// [S-12833] The face's parts (<see cref="FaceParts"/>): its line or lines on the step, the first led by the glyph where the
    /// kind has one (it gives way as the text it reads like), each as wide as a new card's room holds.
    /// </summary>
    public static FaceParts Face(int lines, bool lead) => Face(lines, lead, NaturalWidth);

    /// <summary>[S-13921] <see cref="Face(int, bool)"/> for a new card <paramref name="width"/> wide: a short text's room.</summary>
    public static FaceParts Face(int lines, bool lead, double width)
    {
        var step = Step;
        var room = Math.Max(1, width - (2 * FamilySpace.Inset));
        var rows = new List<FaceRow>(Lines(lines));
        for (var i = 0; i < Lines(lines); i++)
        {
            var text = FacePiece.Text(step, room - LeadRoom(lead));
            if (i == 0 && lead)
            {
                var leading = (step.LineHeight - step.FontSize) / 2;
                rows.Add(new FaceRow([new FacePiece(LeadRoom(lead), step.LineHeight, ScaleFloor.AsText(step.FontSize), leading, leading), text]));
            }
            else
            {
                rows.Add(new FaceRow([text]));
            }
        }

        return new FaceParts(step.FontSize, rows);
    }

    /// <summary>
    /// [S-14930] A text kind's card ([S-2438]): its lines on the step in the family's inset on every side, 160..420 wide (a
    /// new one 240), one height; the descriptor relaxes it ([S-12411]) and a larger box scales the face up as one
    /// ([S-12804]). [S-12833] A smaller box scales it down as one, to its line at the legibility floor in the inset (WR-14).
    /// </summary>
    public static WidgetSizing Sizing(int lines, bool lead) => Sizing(lines, lead, NaturalWidth);

    /// <summary>
    /// [S-13921] <see cref="Sizing(int, bool)"/> for a short text (a weekday, a temperature, owner, 2026-11-21: "the widget
    /// atoms" of a forecast's day): a new card <paramref name="width"/> wide, its least no wider, its face scaling down from it.
    /// </summary>
    public static WidgetSizing Sizing(int lines, bool lead, double width)
    {
        var natural = double.IsFinite(width) && width > 0 ? width : NaturalWidth;
        var height = (2 * FamilySpace.Inset) + (Lines(lines) * Step.LineHeight);
        return WidgetSizing.Range(new(Math.Min(MinWidth, natural), height), new(natural, height), new(Math.Max(MaxWidth, natural), height)).ScalesDownTo(Face(lines, lead, natural));
    }

    /// <summary>[S-14930] <see cref="Sizing(int, bool)"/> of <paramref name="kind"/>.</summary>
    public static WidgetSizing Sizing(TextKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        return Sizing(kind.Lines, kind.Lead, kind.Width);
    }

    /// <summary>
    /// [S-14932] What the face draws for <paramref name="shown"/>: its text where it has one, else the kind's words for none
    /// (<paramref name="none"/>), unavailable (WR-10: the face stays, never a blank card). [S-15713] A kind without words for
    /// none draws its reading as <see cref="NoText"/> (WR-10: "its reading as "--""), never its why in the face's place.
    /// </summary>
    public static (string Text, bool Available) Drawn(TextShown shown, string none)
    {
        ArgumentNullException.ThrowIfNull(shown);
        var text = Line(shown.Text);
        if (text.Length > 0)
        {
            return (text, shown.Available);
        }

        var words = Line(none);
        return (words.Length > 0 ? words : NoText, false);
    }

    /// <summary>
    /// [S-15716] The why's width beside the face in a card too short for it under the face (<see cref="SaysWhy"/>): what a
    /// content room <paramref name="width"/> wide (the card's less its inset and the status mark's room) leaves after the
    /// face (<paramref name="face"/> wide as drawn) and a column's gap, at most the sentence's own; none in a card under a
    /// Caption line's height or where that is under <see cref="WhyLine.MinBeside"/>.
    /// </summary>
    public static double? WhyBeside(double width, double cardHeight, double face, double sentence)
    {
        if (!double.IsFinite(width) || !double.IsFinite(cardHeight) || cardHeight < WhyLine.BesideHeight)
        {
            return null;
        }

        var room = width - Math.Max(0, face) - FamilySpace.ColumnGap;
        return room >= WhyLine.MinBeside ? Math.Min(room, double.IsFinite(sentence) ? Math.Max(0, sentence) : room) : null;
    }

    /// <summary>[S-15713] The face's reading where there is none and the kind has no words for it: "--".</summary>
    public const string NoText = "--";

    /// <summary>
    /// [S-15713] The most "--" is drawn at in a content room <paramref name="height"/> tall: the scale a line of the kind's
    /// (<paramref name="lines"/>, its capitals' ink) has there, so the dashes stand as large as the reading they stand for,
    /// never blown up until their thin ink fills the room; never under the legibility floor's scale.
    /// </summary>
    public static double DashScale(double height, int lines)
        => double.IsFinite(height) && height > 0 ? Math.Max(ScaleFloor.Least(Step.FontSize), height / Ink(lines)) : double.PositiveInfinity;

    /// <summary>
    /// [S-15710] Whether a <paramref name="width"/> by <paramref name="height"/> card says why under its face while it has no
    /// text (<see cref="WhyLine"/>): its inset box holds the kind's <paramref name="lines"/> on the step over the why's band
    /// and a sentence's room across. A new card (one line, 52 tall) does not (the why its tooltip).
    /// </summary>
    public static bool SaysWhy(double width, double height, int lines)
        => width > 0 && height > 0 && WhyLine.Fits(width - (2 * FamilySpace.Inset), height - (2 * FamilySpace.Inset), Lines(lines) * Step.LineHeight);

    /// <summary>
    /// [S-14931] [S-14934] The width the text takes at its own step in a content room <paramref name="width"/> by
    /// <paramref name="height"/> (the card's less its inset), led by the glyph or not: the face is scaled as one until its ink
    /// fills the height or the width stops it, so the line takes the room's width at the scale the height gives it (a room
    /// shorter than the face's own ink widens it, never past the legibility floor's scale), never more at a scale over one: a
    /// line that stands whole at its own size or larger is drawn whole, grown until one axis stops it. [S-15720] A one-line
    /// text (<paramref name="natural"/>: its width at the step) is drawn whole at the scale the width gives it, shrunk as far
    /// as the legibility floor (its 9 DIP) and cut only past that, at the floor: the reading stays read at every size (a long
    /// condition cut at the height's scale had read "O." at 240 × 170, [S-15715] retired). A room not laid out yet is unbounded.
    /// </summary>
    public static double Room(double width, double height, int lines, bool lead, double natural = double.NaN)
    {
        if (!(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return double.PositiveInfinity;
        }

        var floor = ScaleFloor.Least(Step.FontSize);
        var byHeight = Math.Max(height / Ink(lines), floor);
        var scale = Lines(lines) == 1 && double.IsFinite(natural) && natural > 0
            ? Math.Max(Math.Min(byHeight, width / (natural + LeadRoom(lead))), floor)
            : Math.Min(byHeight, 1);
        return Math.Max(0, (width / scale) - LeadRoom(lead));
    }

    /// <summary>[S-14931] Somebody else's string as one line: breaks and runs of white space one space, trimmed.</summary>
    public static string Line(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
