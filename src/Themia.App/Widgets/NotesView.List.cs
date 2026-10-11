using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Notes;
using Themia.Core.Widgets;
using Windows.System;
using Themia.Core.Shell;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1367] Choosing, creating, renaming and deleting notes from the widget itself: a picker in the
/// header at every size, and at XL a note list beside the text. Both act on the device-wide pool,
/// so a note renamed or deleted here is renamed or deleted for every widget showing it; only which
/// note this widget shows is its own.
/// </summary>
internal sealed partial class NotesView
{
    private readonly List<(string Id, Border Row, IDisposable Hover)> _rows = [];

    /// <summary>[S-1994] The title typed so far.</summary>
    private readonly TypeAhead _typeAhead = new();
    private Button _picker = null!;
    private Grid _listHost = null!;
    private StackPanel _listPanel = null!;
    private ScrollViewer _listScroll = null!;
    private Border _newRow = null!;
    private TextBlock? _newRowText;
    private IDisposable? _newRowHover;

    /// <summary>The open rename box (in the list or the header's flyout), or null.</summary>
    private TextBox? _renaming;

    /// <summary>[S-1563] The list row under the keyboard cursor, or negative for none.</summary>
    private int _focusIndex = -1;

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
            Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),
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
    /// The picker shows wherever there is a pool to pick from — except at XL, where the list does its
    /// job, and on the narrowest tile, where it would push the note's title out of the header.
    /// </summary>
    private void UpdatePicker()
        => _picker.Visibility = _pool is not null && !_plan.List && _area.WidthClass > SizeClass.Compact ? Visibility.Visible : Visibility.Collapsed;

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
        var flyout = new Flyout { Content = box, Placement = FlyoutPlacementMode.Bottom };
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
    /// [S-1367] Delete after asking: the text cannot be recovered. Every widget showing the note then
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

            var confirmed = await FluentLayout.ConfirmAsync(
                this,
                Str.WidgetsC_Notes_DeleteConfirmTitle.Text(),
                Str.WidgetsC_Notes_DeleteConfirmBody.Format(("name", note.Title)),
                Str.Common_Delete.Text()).ConfigureAwait(true);

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

    // -----------------------------------------------------------------------------------------
    // XL note list
    // -----------------------------------------------------------------------------------------

    private void BuildList()
    {
        _listPanel = new StackPanel();

        _listScroll = new ScrollViewer
        {
            Content = _listPanel,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        var add = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
        add.Children.Add(Fluent.Icon(Glyphs.Add, "TextFillColorSecondaryBrush", 14));
        var addText = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        addText.Text = Str.Notes_NewNote.Text();
        _newRowText = addText;
        add.Children.Add(addText);

        _newRow = Fluent.Row(add, compact: true);
        _newRowHover = Fluent.Hover(_newRow);
        _newRow.Tapped += OnNewRowTapped;

        _listHost = new Grid
        {
            // The rows reach the card's left edge, as every list's rows do; the text keeps the inset.
            Width = NotesLayout.ListWidth + WidgetLayout.ContentInset,
            Margin = new Thickness(-WidgetLayout.ContentInset, -WidgetLayout.ContentInset, Fluent.Space2, -WidgetLayout.ContentInset),
            Padding = new Thickness(0, Fluent.Space1, 0, Fluent.Space2),
            BorderThickness = new Thickness(0, 0, Fluent.Hairline, 0),
            BorderBrush = FluentLayout.Themed("DividerStrokeColorDefaultBrush", Colors.Gray),
            Visibility = Visibility.Collapsed,

            // [S-1563] Where a click on a row parks the focus: off the editor, so the lease's keys
            // reach the list, and outside the scroller, which would take the arrows for itself.
            IsTabStop = true,
            UseSystemFocusVisuals = false,
        };

        _listHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _listHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SetRow(_newRow, 1);
        _listHost.Children.Add(_listScroll);
        _listHost.Children.Add(_newRow);

        SetColumn(_listHost, 0);
        Children.Add(_listHost);
    }

    private void OnNewRowTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        NewNote();
    }

    private void LayList()
    {
        var show = _plan.List && _pool is not null;
        var was = _listHost.Visibility == Visibility.Visible;
        _listHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        UpdatePicker();
        UpdateMessage();

        if (show && !was)
        {
            RebuildList();
        }
    }

    /// <summary>One row per note: its title and when it was last written, the shown note lit.</summary>
    private void RebuildList()
    {
        if (_detached || _listHost.Visibility != Visibility.Visible || _pool is null)
        {
            return;
        }

        // A rename box open in a row survives only its own rebuild-free life; a rebuild ends it.
        foreach (var (_, _, hover) in _rows)
        {
            hover.Dispose();
        }

        _rows.Clear();
        _listPanel.Children.Clear();

        var now = DateTimeOffset.Now;

        foreach (var note in _pool.Notes)
        {
            var id = note.Id;
            var title = Fluent.Text(FluentType.Body);
            title.Text = note.Title;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;

            var when = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            when.Text = Formatting.Ago(DateTimeOffset.FromUnixTimeSeconds(note.UpdatedAt).ToLocalTime(), now);
            when.TextTrimming = TextTrimming.CharacterEllipsis;

            var lines = new StackPanel();
            lines.Children.Add(title);
            lines.Children.Add(when);

            var row = Fluent.Row(lines);
            RowFocus.Prepare(row);

            var current = id == _shownId;
            if (current)
            {
                row.Background = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray);
                title.Foreground = FluentLayout.Themed("AccentTextFillColorPrimaryBrush", Colors.DodgerBlue);
            }

            // The shown row keeps its fill; hover would put it back to clear on the way out.
            var hover = current ? NoHover.Instance : Fluent.Hover(row);

            row.Tapped += (_, e) =>
            {
                e.Handled = true;
                FocusList(_rows.FindIndex(r => r.Id == id));
                Pick(id);
            };

            row.DoubleTapped += (_, e) =>
            {
                e.Handled = true;
                BeginRename(id);
            };

            row.RightTapped += (_, e) => ItemMenu.Show(row, e, RowMenu(id));

            _rows.Add((id, row, hover));
            _listPanel.Children.Add(row);
        }

        if (_rows.Count == 0)
        {
            var none = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            none.Text = Str.WidgetsC_Notes_NoNotes.Text();
            _listPanel.Children.Add(Fluent.Row(none, compact: true));
        }

        if (_focusIndex >= 0)
        {
            _focusIndex = ListKeys.AfterRemoval(_focusIndex, _rows.Count);
            if (_focusIndex >= 0)
            {
                RowFocus.Mark(_rows[_focusIndex].Row, true);
            }
        }
    }

    private IEnumerable<ItemMenuEntry> RowMenu(string id) =>
    [
        new ItemMenuEntry(Glyphs.Open, Str.Common_Open.Text(), () => Pick(id), Disabled: id == _shownId),
        new ItemMenuEntry(Glyphs.Rename, Str.Common_Rename.Text(), () => BeginRename(id), Quick: QuickCommand.Rename),
        ItemMenuEntry.Separator,
        new ItemMenuEntry(Glyphs.Delete, Str.Common_DeleteEllipsis.Text(), () => _ = DeleteAsync(id), Danger: true, Quick: QuickCommand.Delete),
    ];

    /// <summary>[S-1367] Rename in the row: Enter or leaving the box commits, Escape does not.</summary>
    private void BeginRename(string id)
    {
        var index = _rows.FindIndex(r => r.Id == id);
        if (index < 0 || _pool?.Find(id) is not { } note || _rows[index].Row.Child is not StackPanel lines)
        {
            return;
        }

        var box = new TextBox { Text = note.Title, MinWidth = 0, PlaceholderText = Str.Notes_NamePlaceholder.Text() };
        var done = false;

        void Finish(bool commit)
        {
            if (done)
            {
                return;
            }

            done = true;
            _renaming = null;

            if (commit)
            {
                // Renaming re-sorts the pool, which rebuilds the list and drops this box with it.
                _pool?.Rename(id, box.Text);
            }
            else
            {
                RebuildList();
            }

            UpdateKeyboard();
        }

        box.KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Enter or VirtualKey.Escape)
            {
                e.Handled = true;
                Finish(commit: e.Key == VirtualKey.Enter);
                _listHost.Focus(FocusState.Programmatic);
            }
        };

        box.LostFocus += (_, _) => Finish(commit: true);

        lines.Children.Clear();
        lines.Children.Add(box);

        _renaming = box;
        UpdateKeyboard();
        box.Focus(FocusState.Programmatic);
        box.SelectAll();
    }

    private void FocusList(int index)
    {
        if (_focusIndex >= 0 && _focusIndex < _rows.Count)
        {
            RowFocus.Mark(_rows[_focusIndex].Row, false);
        }

        _focusIndex = index;
        _listHost.Focus(FocusState.Programmatic);

        if (index >= 0 && index < _rows.Count)
        {
            RowFocus.Mark(_rows[index].Row, true);
        }
    }

    /// <summary>
    /// [S-1367] [S-1563] A key from the shell's keyboard lease, for the note list (shown at XL),
    /// decided by <see cref="ListKeys"/> like every list widget's: Up/Down, Page Up/Down, Home/End
    /// move, Enter opens, F2 renames, Delete deletes (after asking), the Menu key opens the row's
    /// menu, Escape — also sent whenever the lease ends — lets go. The editor and a rename box get
    /// no keys here: while one has focus the keys are its own.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || _renaming is not null)
        {
            return false;
        }

        if (key == WidgetKey.Escape)
        {
            _typeAhead.Reset();
            return ReleaseList();
        }

        if (_listHost.Visibility != Visibility.Visible)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _focusIndex, _rows.Count);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                FocusList(decision.Index);
                return true;

            case ListCommand.Open:
                Pick(_rows[decision.Index].Id);
                return true;

            case ListCommand.Rename:
                BeginRename(_rows[decision.Index].Id);
                return true;

            case ListCommand.Delete:
                _ = DeleteAsync(_rows[decision.Index].Id);
                return true;

            case ListCommand.Menu:
                ItemMenu.Open(_rows[decision.Index].Row, RowMenu(_rows[decision.Index].Id));
                return true;

            default:
                return false;
        }
    }

    /// <summary>[S-1994] Type-ahead ([S-1980]) in the XL note list: a title's first letters focus its row.</summary>
    public bool HandleText(char character)
    {
        if (_detached || _renaming is not null || _listHost.Visibility != Visibility.Visible || _rows.Count == 0)
        {
            return false;
        }

        var titles = _rows.Select(r => _pool?.Find(r.Id)?.Title ?? string.Empty).ToList();
        if (_typeAhead.Type(character, Environment.TickCount64, titles, _focusIndex) is not { } index)
        {
            return _typeAhead.Prefix.Length > 0;
        }

        FocusList(index);
        return true;
    }

    private bool ReleaseList()
    {
        if (_focusIndex < 0)
        {
            return false;
        }

        if (_focusIndex < _rows.Count)
        {
            RowFocus.Mark(_rows[_focusIndex].Row, false);
        }

        _focusIndex = -1;
        return true;
    }

    private void DetachList()
    {
        foreach (var (_, _, hover) in _rows)
        {
            hover.Dispose();
        }

        _rows.Clear();
        _newRowHover?.Dispose();
        _newRow.Tapped -= OnNewRowTapped;
        _picker.Click -= OnPickerClick;
    }

    /// <summary>The hover of the shown row: none, so its fill stays.</summary>
    private sealed class NoHover : IDisposable
    {
        public static readonly NoHover Instance = new();

        public void Dispose()
        {
            // ignore: nothing was hooked.
        }
    }
}
