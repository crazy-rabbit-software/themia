using System.Text.Json.Serialization;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-1346] Which note a Notes widget shows, and how. The note itself lives in the note store
/// (<c>Themia.Core.Notes.NoteStore</c>), not here: a note outlives the widget, the view and the
/// layout it was written in, and two widgets can show one note.
/// </summary>
public sealed class NotesConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Notes;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("notes"),
        Kind = WidgetKind.Notes,
    };

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
    /// A copy shows the same note: copy and paste duplicates the window onto the note, not the note
    /// (original S-12 — two views of one document).
    /// </summary>
    public override WidgetConfig Clone() => new NotesConfig
    {
        NoteId = NoteId,
        FontSizePx = FontSizePx,
    };
}
