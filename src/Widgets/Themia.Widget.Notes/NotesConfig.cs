using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-1346] Which note a Notes widget shows, and how. The note itself lives in the note store
/// (<c>Themia.Core.Notes.NoteStore</c>), not here: a note outlives the widget, the view and the
/// layout it was written in, and two widgets can show one note.
/// </summary>
public sealed class NotesConfig : WidgetConfig
{
    /// <summary>
    /// The note shown, or null for none yet — a new widget gets a new note of its own on first show.
    /// Validated by the store before it ever reaches a path ([S-1342]).
    /// </summary>
    public string? NoteId { get; set; }

    /// <summary>[S-03] Base text size in device-independent pixels, before the global text scale.</summary>
    public double FontSizePx { get; set; } = 13;

    /// <summary>Clamped on read: a persisted 0 or a NaN would render nothing at all. [S-05]</summary>
    [JsonIgnore]
    public double EffectiveFontSizePx => double.IsFinite(FontSizePx) ? Math.Clamp(FontSizePx, 8, 48) : 13;

    /// <summary>
    /// [S-1349] The size the note is drawn at: its own size times the text size every widget follows
    /// (⚙ › Appearance › Text size, <paramref name="textScale"/>), so the two compose and neither is
    /// lost on a restart; a text size that is not a positive number counts as 100 %.
    /// </summary>
    public double DrawnFontSizePx(double textScale)
        => EffectiveFontSizePx * (double.IsFinite(textScale) && textScale > 0 ? textScale : 1);

    /// <summary>
    /// A copy shows the same note: copy and paste duplicates the window onto the note, not the note
    /// (original S-12 — two views of one document).
    /// </summary>
    public override WidgetConfig Clone() => new NotesConfig
    {
        NoteId = NoteId,
        FontSizePx = FontSizePx,
    };
}
