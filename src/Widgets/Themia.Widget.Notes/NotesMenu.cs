using System.Globalization;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Notes;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1349] The Notes widget's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), which note it shows (picked from the notes kept on this PC, which can be made, renamed
/// and deleted there), apart from it the text size, and apart again clearing the note where one is
/// shown. It has no settings page.
/// <para>
/// The notes are device-wide (the host's pool, read through <see cref="SettingsHost.Notes"/>):
/// renaming or deleting one does it for every widget showing it. Only the choice of note and the look
/// are this widget's. Each line writes straight to the live config or the pool.
/// </para>
/// </summary>
internal static class NotesMenu
{
    /// <summary>The text sizes the config clamps to ([S-1346]), so the menu never offers one it would not draw.</summary>
    private const double MinFont = 8;

    private const double MaxFont = 48;

    /// <param name="pool">The notes on this PC, or null where the host has none to offer.</param>
    /// <param name="now">For each note's last-written age.</param>
    public static void Lines(WidgetMenuLines menu, NotesConfig config, INotes? pool, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(Notes(menu, config, pool, now));

        // The note's base size, how it reads, apart from which note it is; headings are drawn larger.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Number(Glyphs.TextSize, NotesStr.Notes_FontSizeLabel.Text(),
            Math.Round(config.EffectiveFontSizePx), MinFont, MaxFont, 1d,
            value => string.Create(CultureInfo.InvariantCulture, $"{value:0} px"), value => config.FontSizePx = value));

        // [S-130] Empties the note's text for good (there is no undo, no bin, no second copy), so it
        // asks first; greyed while there is nothing to clear. The note itself stays, with its title.
        // Not there while the widget shows no note (none kept on this PC, or the one shown was
        // deleted): there is no note it could ever clear ([S-6070]).
        if (pool is null || pool.Find(config.NoteId) is null)
        {
            return;
        }

        var hasText = pool.Read(config.NoteId).Text.Length > 0;
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Confirm(Glyphs.Clear, NotesStr.WidgetsC_Notes_Settings_Clear_Label.Text(),
            NotesStr.WidgetsC_Notes_Settings_Clear_ConfirmBody.Text(), Str.Common_Clear.Text(),
            () =>
            {
                if (config.NoteId is { } id)
                {
                    pool.Write(id, string.Empty);
                }
            }, danger: true, disabled: !hasText));
    }

    /// <summary>
    /// [S-1349] The notes on this PC as a choice of one (each with when it was last written), a line
    /// when the shown note was deleted, then Rename… and Delete note for the note shown and New note,
    /// which makes one and shows it. Deleting is always asked first: the text cannot be recovered.
    /// </summary>
    private static ItemMenuEntry Notes(WidgetMenuLines menu, NotesConfig config, INotes? pool, DateTimeOffset now)
    {
        var title = NotesStr.WidgetsC_Notes_Settings_Note.Text();
        if (pool is null)
        {
            return WidgetMenuLines.Submenu(Glyphs.Note, title, [WidgetMenuLines.Note(NotesStr.WidgetsC_Notes_Unavailable.Text())]);
        }

        var lines = new List<ItemMenuEntry>();
        var current = pool.Find(config.NoteId);
        if (config.NoteId is not null && current is null)
        {
            lines.Add(WidgetMenuLines.Note(NotesStr.WidgetsC_Notes_Settings_ShownDeleted.Text()));
        }

        foreach (var note in pool.Notes)
        {
            var id = note.Id;
            lines.Add(new ItemMenuEntry(null, Shown(note.Title), () =>
            {
                if (config.NoteId != id)
                {
                    config.NoteId = id;
                    menu.Save();
                }
            }, Checked: id == config.NoteId, Group: title,
            Value: Formatting.Ago(DateTimeOffset.FromUnixTimeSeconds(note.UpdatedAt).ToLocalTime(), now)));
        }

        if (pool.Notes.Count == 0)
        {
            lines.Add(WidgetMenuLines.Note(NotesStr.WidgetsC_Notes_Settings_NoNotes.Text()));
        }

        lines.Add(ItemMenuEntry.Separator);
        if (current is not null)
        {
            var currentId = current.Id;
            lines.Add(new ItemMenuEntry(Glyphs.Rename, Str.Menu_Rename.Text(), () => menu.Ask(new WidgetTextAsk(
                Str.Common_Rename.Text(),
                NotesStr.Notes_NamePlaceholder.Text(),
                Str.Common_Rename.Text(),
                _ => null,
                typed =>
                {
                    if (!string.Equals(typed, current.Title, StringComparison.Ordinal))
                    {
                        pool.Rename(currentId, typed);
                        menu.Save();
                    }
                },
                Initial: current.Title))));
        }

        lines.Add(menu.Command(Glyphs.Add, NotesStr.Notes_NewNote.Text(), () => config.NoteId = pool.Create(NotesStr.Notes_NewName.Text())));

        if (current is not null)
        {
            var currentId = current.Id;
            lines.Add(ItemMenuEntry.Separator);

            // Every widget showing it then says so ([S-1347]) rather than showing another note.
            lines.Add(menu.Confirm(Glyphs.Delete, NotesStr.Notes_DeleteTitle.Text(),
                NotesStr.WidgetsC_Notes_DeleteConfirmTitle.Text(), Str.Common_Delete.Text(),
                () => pool.Delete(currentId), danger: true, detail: NotesStr.WidgetsC_Notes_DeleteConfirmBody.Format(("name", Shown(current.Title)))));
        }

        return WidgetMenuLines.Submenu(Glyphs.Note, title, lines) with { Value = current is null ? null : Shown(current.Title) };
    }

    /// <summary>A note's title, or its placeholder for an untitled one.</summary>
    private static string Shown(string title) => string.IsNullOrWhiteSpace(title) ? NotesStr.Notes_NamePlaceholder.Text() : title;
}
