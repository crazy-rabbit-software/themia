using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.System;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1364] Choosing, creating, renaming and deleting notes from the widget itself: the picker in
/// the header. The widget never lists the notes beside the text; it shows the one note. The picker
/// acts on the device-wide pool, so a note renamed or deleted here is renamed or deleted for every
/// widget showing it; only which note this widget shows is its own.
/// </summary>
internal sealed partial class NotesView
{
    private Button _picker = null!;

    /// <summary>The open rename box (the header's flyout), or null.</summary>
    private TextBox? _renaming;

    // -----------------------------------------------------------------------------------------
    // Header picker
    // -----------------------------------------------------------------------------------------

    private void BuildPicker()
    {
        _picker = new Button
        {
            Content = Fluent.Icon(Glyphs.ChevronDown, "TextFillColorSecondaryBrush", 12),
            Padding = new Thickness(Fluent.Space1),
            MinWidth = 0,
            MinHeight = 0,
            BorderThickness = new Thickness(0),
            Background = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        ApplyPickerTexts();
        _picker.Click += OnPickerClick;
    }

    private void ApplyPickerTexts()
    {
        ToolTipService.SetToolTip(_picker, Str.Notes_NotesLabel.Text());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_picker, Str.WidgetsC_Notes_ChooseNote.Text());
    }

    /// <summary>
    /// The picker shows wherever there is a pool to pick from — except on the narrowest tile, where
    /// it would push the note's title out of the header.
    /// </summary>
    private void UpdatePicker()
        => _picker.Visibility = _pool is not null && _area.WidthClass > SizeClass.Compact ? Visibility.Visible : Visibility.Collapsed;

    private void OnPickerClick(object sender, RoutedEventArgs args)
    {
        if (_pool is null || _detached)
        {
            return;
        }

        var entries = new List<ItemMenuEntry>();
        foreach (var note in _pool.Notes)
        {
            var id = note.Id;
            entries.Add(new ItemMenuEntry(null, note.Title, () => Pick(id), Checked: id == _shownId));
        }

        entries.Add(ItemMenuEntry.Separator);
        entries.Add(new ItemMenuEntry(Glyphs.Add, Str.Notes_NewNote.Text(), NewNote));

        if (_state == NoteState.Ready && _shownId is { } shown)
        {
            entries.Add(new ItemMenuEntry(Glyphs.Rename, Str.WidgetsC_Notes_RenameEllipsis.Text(), () => RenameInFlyout(shown)));
            entries.Add(new ItemMenuEntry(Glyphs.Delete, Str.WidgetsC_Notes_DeleteNoteEllipsis.Text(), () => _ = DeleteAsync(shown), Danger: true));
        }

        ItemMenu.Open(_picker, entries);
    }

    /// <summary>Rename the shown note in a small box under the picker: Enter or leaving it commits, Escape does not.</summary>
    private void RenameInFlyout(string id)
    {
        if (_pool?.Find(id) is not { } note)
        {
            return;
        }

        var box = new TextBox { Text = note.Title, MinWidth = 200, PlaceholderText = Str.Notes_NamePlaceholder.Text() };
        // [S-1500] Its own popup: inside the surface's window the region clips it to the card.
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
        };

        flyout.ShowAt(_picker);
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

        Pick(_pool.Create(Str.Notes_NewName.Text()));

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

            var confirmed = await AskToDeleteAsync(note.Title).ConfigureAwait(true);

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
    /// [S-1500] The question beside the picker, which is where the delete was chosen; under the
    /// widget when the header has no picker.
    /// </summary>
    private Task<bool> AskToDeleteAsync(string title) => ConfirmFlyout.AskAsync(
        _picker.XamlRoot is not null && _picker.Visibility == Visibility.Visible && _picker.ActualWidth > 0 ? _picker : this,
        Str.WidgetsC_Notes_DeleteConfirmTitle.Text(),
        Str.Common_Delete.Text(),
        Str.WidgetsC_Notes_DeleteConfirmBody.Format(("name", title)));

    private void DetachPicker() => _picker.Click -= OnPickerClick;
}
