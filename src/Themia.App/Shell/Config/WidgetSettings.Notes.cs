using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Notes;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Notes;
using Themia.Core.Widgets;
using Windows.System;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1349] The Notes settings: which note the widget shows — picked from the notes kept on this PC,
/// which can be created, renamed and deleted here — and how it is drawn.
/// <para>
/// The notes are device-wide: renaming or deleting one here does it for every widget showing it.
/// Only the choice of note, and the look, are this widget's. Each row writes straight to the live
/// config or the pool; nothing is mirrored into a local field first.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    private const string NotesScope = "notes-settings";

    public static void Notes(List<FrameworkElement> rows, NotesConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_Notes_Settings_Note.Text()));

        var pool = AppServices.Notes;

        if (pool is null)
        {
            rows.Add(FluentLayout.Note(Str.WidgetsC_Notes_Unavailable.Text()));
        }
        else
        {
            // The rows change as notes come and go, here or from a widget, so they live in one host
            // rebuilt in place — and listen to the pool only while the page is on screen.
            var list = new StackPanel { Spacing = Fluent.Space1 };
            rows.Add(list);

            void Rebuild()
            {
                list.Children.Clear();

                foreach (var row in NoteRows(pool, config, save, Rebuild))
                {
                    list.Children.Add(row);
                }
            }

            void OnChanged(string? id)
            {
                // A body change does not change a row; a list change (created, renamed, deleted) does.
                if (id is null)
                {
                    Rebuild();
                }
            }

            list.Loaded += (_, _) => pool.Changed += OnChanged;
            list.Unloaded += (_, _) => pool.Changed -= OnChanged;
            Rebuild();
        }

        rows.Add(FluentLayout.Header(Str.ConfigTabs_Display.Text()));

        // [S-05] The note's base size; headings are drawn larger than this.
        rows.Add(FluentLayout.Row(Glyphs.TextSize, Str.Notes_FontSizeLabel.Text(), Str.Notes_FontSizeDesc.Text(),
            // The full range the config clamps to, so the slider can never sit at 32 while the
            // stored size is 40 and the note is drawn at neither of the two.
            FluentLayout.Slider(config.EffectiveFontSizePx, 8, 48, 1, "px",
                value => { config.FontSizePx = value; save(); })));

        // [S-130] Empties the note's text for good — there is no undo, no bin, no second copy — so
        // it asks first. The note itself stays, with its title.
        rows.Add(FluentLayout.ConfirmRow(
            Glyphs.Clear,
            Str.WidgetsC_Notes_Settings_Clear_Label.Text(),
            Str.WidgetsC_Notes_Settings_Clear_Desc.Text(),
            Str.Common_Clear.Text(),
            Str.WidgetsC_Notes_Settings_Clear_ConfirmTitle.Text(),
            () => pool?.Find(config.NoteId) is not null && pool.Read(config.NoteId).Text.Length > 0
                ? Str.WidgetsC_Notes_Settings_Clear_ConfirmBody.Text()
                : null,
            () =>
            {
                if (config.NoteId is { } id)
                {
                    pool?.Write(id, string.Empty);
                }
            }));
    }

    /// <summary>
    /// [S-1349] One row per note: a pick dot, the title (committed on Enter or on leaving the box, not
    /// per keystroke — a title is a file write), when it was last written, and delete after asking.
    /// "New note" adds one and picks it.
    /// </summary>
    private static IEnumerable<FrameworkElement> NoteRows(NotePool pool, NotesConfig config, Action save, Action rebuild)
    {
        if (config.NoteId is { } shown && pool.Find(shown) is null)
        {
            yield return FluentLayout.Note(Str.WidgetsC_Notes_Settings_ShownDeleted.Text());
        }

        var now = DateTimeOffset.Now;

        foreach (var note in pool.Notes)
        {
            var noteId = note.Id;

            var pick = new RadioButton
            {
                GroupName = "notes",
                IsChecked = noteId == config.NoteId,
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };

            pick.Checked += (_, _) =>
            {
                if (config.NoteId != noteId)
                {
                    config.NoteId = noteId;
                    save();
                }
            };

            var title = new TextBox
            {
                Text = note.Title,
                PlaceholderText = Str.Notes_NamePlaceholder.Text(),
                MinWidth = 160,
                VerticalAlignment = VerticalAlignment.Center,
            };

            void Commit()
            {
                if (!string.Equals(title.Text, pool.Find(noteId)?.Title, StringComparison.Ordinal))
                {
                    pool.Rename(noteId, title.Text);
                }
            }

            title.LostFocus += (_, _) => Commit();
            title.KeyDown += (_, args) =>
            {
                if (args.Key == VirtualKey.Enter)
                {
                    args.Handled = true;
                    Commit();
                }
            };

            var edited = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            edited.Text = Formatting.Ago(DateTimeOffset.FromUnixTimeSeconds(note.UpdatedAt).ToLocalTime(), now);
            edited.VerticalAlignment = VerticalAlignment.Center;

            var remove = FluentLayout.IconCommand(Glyphs.Delete, Str.Notes_DeleteTitle.Text());
            remove.Click += async (_, _) =>
            {
                try
                {
                    // Always asked: the text cannot be recovered, and the button sits one row from
                    // the title being typed in.
                    if (!await FluentLayout.ConfirmAsync(
                            remove,
                            Str.WidgetsC_Notes_DeleteConfirmTitle.Text(),
                            Str.WidgetsC_Notes_DeleteConfirmBody.Format(("name", note.Title)),
                            Str.Common_Delete.Text()).ConfigureAwait(true))
                    {
                        return;
                    }

                    // Every widget showing it then says so ([S-1347]) rather than showing another note.
                    pool.Delete(noteId);
                    rebuild();
                }
                catch (Exception ex)
                {
                    Log.Error(NotesScope, "could not delete the note", ex);
                }
            };

            var line = new Grid
            {
                ColumnSpacing = Fluent.Space2,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
            };

            Grid.SetColumn(title, 1);
            Grid.SetColumn(edited, 2);
            Grid.SetColumn(remove, 3);
            line.Children.Add(pick);
            line.Children.Add(title);
            line.Children.Add(edited);
            line.Children.Add(remove);

            yield return FluentLayout.Card(line);
        }

        if (pool.Notes.Count == 0)
        {
            yield return FluentLayout.Note(Str.WidgetsC_Notes_Settings_NoNotes.Text());
        }

        var add = new Button { Content = Str.Notes_NewNote.Text() };
        add.Click += (_, _) =>
        {
            config.NoteId = pool.Create(Str.Notes_NewName.Text());
            save();
            rebuild();
        };

        yield return add;
    }
}
