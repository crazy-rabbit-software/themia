using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Notes;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.System;
using Themia.Widget.Notes;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1364] [S-14771] Choosing, creating, renaming and deleting notes from the widget itself: the pill's picker (the note's
/// name and its chevron: every note, New note) and its "…" (Rename…, Delete note…). The widget never lists the notes beside
/// the text; it shows the one note. They act on the device-wide pool, so a note renamed or deleted here is renamed or
/// deleted for every widget showing it; only which note this widget shows is its own.
/// </summary>
internal sealed partial class NotesView
{
    private Border _picker = null!;

    /// <summary>[S-14771] The picker's words: the shown note's name, cut to the room the card leaves it.</summary>
    private TextBlock _pickerName = null!;

    /// <summary>The open rename box (the pill's flyout), or null.</summary>
    private TextBox? _renaming;

    /// <summary>[S-14268] A picture's note name (its sample's), with no pool to read one from.</summary>
    private string? _pictureName;

    // -----------------------------------------------------------------------------------------
    // The picker
    // -----------------------------------------------------------------------------------------

    private void BuildPicker()
    {
        _pickerName = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
        _picker = ToolLine.Dropdown(_pickerName, NotesStr.WidgetsC_Notes_ChooseNote.Text(), OnPickerClick);
        _picker.Visibility = Visibility.Collapsed;
        ApplyPickerTexts();
    }

    private void ApplyPickerTexts()
    {
        ToolTipService.SetToolTip(_picker, NotesStr.WidgetsC_Notes_ChooseNote.Text());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_picker, NotesStr.WidgetsC_Notes_ChooseNote.Text());
        UpdatePicker();
    }

    /// <summary>
    /// [S-11206] The picker stands wherever there is a pool to pick from, at every size; [S-14268] and in a picture of the
    /// note (its sample, a render: unbound), which shows the widget as it stands on the desktop; a picture's picker opens
    /// nothing. [S-14771] It says the shown note's name (<see cref="NoteMenus.PickerName"/>), cut with an ellipsis at the
    /// room the card's width leaves it (<see cref="NotesLayout.NameWidth"/>; none on a card too narrow: the chevron alone).
    /// </summary>
    private void UpdatePicker()
    {
        _picker.Visibility = _pool is not null || _state == NoteState.Unbound ? Visibility.Visible : Visibility.Collapsed;

        var title = _state switch
        {
            NoteState.Ready or NoteState.Unreadable => _pool?.Find(_shownId)?.Title,
            NoteState.Unbound => _pictureName,
            _ => null,
        };

        var name = NoteMenus.PickerName(title, title is not null, NotesStr.Notes_NamePlaceholder.Text(), NotesStr.WidgetsC_Notes_ChooseNote.Text());
        var room = NotesLayout.NameWidth(_area.IsMeasured ? _area.Width : NotesLayout.Sizing.Natural.Width, _notSavedGlyph.Visibility == Visibility.Visible);

        _pickerName.Text = name;
        _pickerName.MaxWidth = room;
        _pickerName.Visibility = room > 0 ? Visibility.Visible : Visibility.Collapsed;

        // With no name the picker is its chevron's square alone, as a line button is.
        _picker.Padding = room > 0 ? new Thickness(FamilySpace.S2, 0, 0, 0) : default;
        ToolTipService.SetToolTip(_picker, room > 0 ? NotesStr.WidgetsC_Notes_ChooseNote.Text() : name);
    }

    private void OnPickerClick()
    {
        if (_pool is null || _detached)
        {
            // A picture's picker opens nothing: there is no pool behind it.
            return;
        }

        OpenPicker(_pool.Notes, _shownId);
    }

    /// <summary>[S-1364] [S-14771] The picker's menu under it: every note (the shown one checked), then New note.</summary>
    private void OpenPicker(IReadOnlyList<NoteMeta> notes, string? shownId)
    {
        var entries = new List<ItemMenuEntry>();
        foreach (var pick in NoteMenus.Picker(notes, shownId, NotesStr.Notes_NamePlaceholder.Text(), NotesStr.Notes_NewNote.Text()))
        {
            if (pick.Id is { } id)
            {
                entries.Add(new ItemMenuEntry(null, pick.Title, () => Pick(id), Checked: pick.Shown));
            }
            else
            {
                entries.Add(ItemMenuEntry.Separator);
                entries.Add(new ItemMenuEntry(Glyphs.Add, pick.Title, NewNote));
            }
        }

        PillMenu(true);
        if (!ItemMenu.Open(_picker, entries, () => PillMenu(false)))
        {
            PillMenu(false);
        }
    }

    /// <summary>Rename the shown note in a small box under the pill's "…": Enter or leaving it commits, Escape does not.</summary>
    private void RenameInFlyout(string id)
    {
        if (_pool?.Find(id) is not { } note)
        {
            return;
        }

        var box = new TextBox { Text = note.Title, MinWidth = NotesLayout.RenameWidth, PlaceholderText = NotesStr.Notes_NamePlaceholder.Text() };
        // [S-1500] Its own popup, kept in the display's work area by the framework wherever the card sits.
        var flyout = new Flyout { Content = box, Placement = FlyoutPlacementMode.Bottom, ShouldConstrainToRootBounds = false };
        var cancelled = false;

        box.KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Enter or VirtualKey.Escape)
            {
                e.Handled = true;
                cancelled = e.Key == VirtualKey.Escape;
                flyout.Hide();
            }
        };

        flyout.Opened += (_, _) =>
        {
            _renaming = box;
            UpdateKeyboard();
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };

        flyout.Closed += (_, _) =>
        {
            if (!cancelled)
            {
                _pool?.Rename(id, box.Text);
            }

            _renaming = null;
            UpdateKeyboard();
            PillMenu(false);
        };

        PillMenu(true);
        flyout.ShowAt(_more);
    }

    // -----------------------------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------------------------

    /// <summary>Show another note in this widget. What was typed goes to the note it was typed in first.</summary>
    private void Pick(string id)
    {
        if (_detached || string.Equals(id, _config.NoteId, StringComparison.Ordinal))
        {
            return;
        }

        Flush();
        _config.NoteId = id;
        _surface?.RequestSave();
        Resolve();
    }

    private void NewNote()
    {
        if (_pool is null || _detached)
        {
            return;
        }

        Pick(_pool.Create(NotesStr.Notes_NewName.Text()));

        if (_state == NoteState.Ready)
        {
            _editor.Box.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>
    /// [S-1364] Delete after asking: the text cannot be recovered. Every widget showing the note then
    /// says it was deleted ([S-1347]) — this one too; it is not handed another note.
    /// </summary>
    private async Task DeleteAsync(string id)
    {
        try
        {
            if (_pool?.Find(id) is not { } note)
            {
                return;
            }

            PillMenu(true);
            bool confirmed;
            try
            {
                confirmed = await AskToDeleteAsync(note.Title).ConfigureAwait(true);
            }
            finally
            {
                PillMenu(false);
            }

            if (confirmed && !_detached)
            {
                if (id == _shownId)
                {
                    // Nothing typed into it may land after the delete.
                    _dirty = false;
                    _saveTimer?.Stop();
                }

                _pool?.Delete(id);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not delete the note", ex);
        }
    }

    /// <summary>
    /// [S-1500] The question beside the pill's "…", which is where the delete was chosen; under the widget when the pill
    /// has no "…" standing.
    /// </summary>
    private Task<bool> AskToDeleteAsync(string title) => ConfirmFlyout.AskAsync(
        _more.XamlRoot is not null && _more.Visibility == Visibility.Visible && _more.ActualWidth > 0 ? _more : this,
        NotesStr.WidgetsC_Notes_DeleteConfirmTitle.Text(),
        Str.Common_Delete.Text(),
        NotesStr.WidgetsC_Notes_DeleteConfirmBody.Format(("name", title)));
}
