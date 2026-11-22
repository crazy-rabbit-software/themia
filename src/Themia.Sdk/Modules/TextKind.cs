using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-14934] One kind of a text family, as data (owner, 2026-11-21: "for text like the rain in x minutes or the song name or
/// album name make a shared class in the background that all of those widgets use. similar to how the metric one works"):
/// its id, its text's key in its family (which text of the family's source it shows: a track's title, its album, the rain's
/// sentence), its glyph (the add page's, and the lead before its line where <see cref="Lead"/>), its name and description on
/// the add page, its search words and its config (a sealed type a kind: a config's type is how it knows its id, [S-6902]).
/// </summary>
public sealed record TextKind(
    string Id,
    string Key,
    string Glyph,
    LocalText Label,
    LocalText Description,
    IReadOnlyList<string> Keywords,
    Func<WidgetConfig> Create)
{
    /// <summary>The config's type.</summary>
    public Type ConfigType { get; } = Create().GetType();

    /// <summary>[S-14930] The lines its text may wrap to (1: one line, cut with its ellipsis); its card is that many tall.</summary>
    public int Lines { get; init; } = 1;

    /// <summary>
    /// [S-14934] Whether its glyph leads the line (what the text is: a title, an artist, an album look alike without it,
    /// [S-12801]); a sentence that says what it is has none.
    /// </summary>
    public bool Lead { get; init; } = true;

    /// <summary>[S-13921] A new card's width: the text face's, or a short text's own (a weekday, a temperature).</summary>
    public double Width { get; init; } = TextRules.NaturalWidth;

    /// <summary>[S-14933] The role its line is drawn in at rest.</summary>
    public TextTone Tone { get; init; } = TextTone.Primary;

    /// <summary>[S-14932] Its words with no text to show ("No album"), drawn unavailable; null: the family always says why itself.</summary>
    public LocalText? None { get; init; }

    /// <summary>
    /// [S-14935] What a click on the card opens and its card menu's line: its family's source (the player, the place's
    /// forecast); null for a kind with nothing to open (it lights nothing under the pointer).
    /// </summary>
    public TextOpen? Opens { get; init; }
}
