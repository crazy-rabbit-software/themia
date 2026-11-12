using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7037] [S-7038] The check look: rows with a check lead in one or two columns, filled column by
/// column, whole lines only, the done group under its heading ([S-781], [S-782], [S-784]); a click on
/// the check toggles it at once ([S-597]); the title renamed in place and new items typed into the add
/// row under the list ([S-599], [S-600], [S-788], [S-3850]); a row dragged to a new place ([S-786]).
/// The widget carries the changes out (<see cref="Renamed"/>, <see cref="Added"/>,
/// <see cref="Reordered"/>); the list never writes.
/// </summary>
public sealed partial class KitList
{
    /// <summary>The pointer's distance from the viewport edge at which a drag scrolls the list.</summary>
    private const double AutoScrollEdge = 16;

    private KitRowsPanel? _checks;
    private KitListHost? _checkHost;
    private AddFooterRow? _addRow;

    /// <summary>The check lead of each drawn row, by place, for the click that toggles it.</summary>
    private readonly Dictionary<int, Border> _checksByRow = [];

    /// <summary>The open editor: renaming an item (its id) or adding one (null).</summary>
    private (TextBox Box, string? Id)? _editor;

    /// <summary>What an open box hid, to show again when it closes.</summary>
    private readonly List<UIElement> _editorHidden = [];

    private Border? _pressRow;
    private uint _pressPointer;
    private Point _pressAt;
    private Point _grab;
    private bool _dragging;
    private bool _dragEnded;

    /// <summary>[S-600] A title renamed in place: the item and the text the user typed (the widget refuses a blank or unchanged one).</summary>
    public Action<KitItem, string>? Renamed { get; init; }

    /// <summary>[S-599] A new item typed into the add row; the list stays open for the next one.</summary>
    public Action<string>? Added { get; init; }

    /// <summary>[S-786] [S-3802] The items' new order (by id) after a drag or Alt+Shift+Up / Down; an unchanged order is never raised.</summary>
    public Action<IReadOnlyList<string>>? Reordered { get; init; }

    /// <summary>[S-808] An editor wants the keyboard (true) or is done with it: the widget asks its surface.</summary>
    public Action<bool>? TextInput { get; init; }

    /// <summary>Whether an editor is open (the widget holds back what would rebuild it).</summary>
    public bool Editing => _editor is not null;

    private bool Checked => _spec.Look == KitRowLook.Check;

    /// <summary>The check look's parts, in the list's rows: the host under the column header, the add row under it.</summary>
    private void BuildCheckParts()
    {
        _checks = new KitRowsPanel();
        _scroll.Content = _checks;
        _scroll.VerticalAlignment = VerticalAlignment.Stretch;
        _checkHost = new KitListHost(_scroll, _checks);
        SetRow(_checkHost, 1);

        _addRow = new AddFooterRow(_spec.AddLabel?.Invoke() ?? string.Empty, OpenAddEditor);
        SetRow(_addRow.Element, 2);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    }

    /// <summary>[S-1110] The add row's words again, after a language change.</summary>
    public void Relabel()
    {
        if (_addRow is not null)
        {
            _addRow.Label = _spec.AddLabel?.Invoke() ?? string.Empty;
        }
    }

    /// <summary>
    /// [S-7038] Put <paramref name="element"/> over the list's own slot (above the add row): a widget's
    /// state panel, so its sentence centres over the rows it stands for.
    /// </summary>
    public void Overlay(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        SetRow(element, 1);
        Children.Add(element);
    }

    /// <summary>
    /// [S-3850] The add row for the plan: shown when the plan has room for it and the list is drawn,
    /// or while it holds an editor; at the rows' density and inset.
    /// </summary>
    private void FitAddRow(bool shown)
        => _addRow?.Fit((shown && _plan.AddRow) || _editor is { Id: null });

    /// <summary>[S-3850] Without a list (another screen) the add row goes, unless it holds the user's words.</summary>
    private void HideAddRow(KitPlan plan)
    {
        if (_addRow is null)
        {
            return;
        }

        _plan = plan;
        FitAddRow(shown: false);
    }

    /// <summary>
    /// [S-7037] Whether a show must wait: never under a lifted row (it would vanish mid-drag; the drop
    /// draws), never under the user's typing in a row's editor while that item is still listed (the
    /// editor's commit or cancel draws). An editor whose item has gone goes with it.
    /// </summary>
    private bool HoldBack(IReadOnlyList<KitItem> items)
    {
        if (_dragging)
        {
            return true;
        }

        if (_editor is { Id: { } editing })
        {
            if (items.Any(i => i.Id == editing))
            {
                return true;
            }

            CloseEditor();
        }

        return false;
    }

    private void BuildChecks()
    {
        var panel = _checks!;
        panel.Columns = _plan.Columns;

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];

            // [S-784] A group's heading over its first row, where a heading earns its line.
            if (_plan.Headings && item.Group is { Heading: { } heading } group && (i == 0 || _items[i - 1].Group?.Key != group.Key))
            {
                panel.Children.Add(CheckHeading(heading));
            }

            var row = CheckRow(item, i);
            panel.Children.Add(row);
            _rowElements.Add(row);
        }

        panel.ResetOrder();
        FitAddRow(shown: true);
        CapToWholeLines();
    }

    /// <summary>
    /// [S-781] [S-6712] The scroll shows only the lines that fit whole in the list's room (the view's
    /// area less the bottom inset and the add row), never ending on a heading: the rows' heights are
    /// the plan's, so the cap is decided here rather than from a measure that may not have run yet.
    /// </summary>
    private void CapToWholeLines()
    {
        if (!_area.IsMeasured || _checks is not { } panel)
        {
            _scroll.MaxHeight = double.PositiveInfinity;
            return;
        }

        var lines = KitChecks.LinesFor(panel.Children.Count, panel.Columns);
        var bottoms = new List<double>(lines);
        var headings = new List<bool>(lines);
        var total = 0.0;
        for (var line = 0; line < lines; line++)
        {
            var heading = panel.Children[line] is not Border;
            total += heading ? FamilyShape.Row1 : _plan.RowHeight;
            bottoms.Add(total);
            headings.Add(heading);
        }

        var room = KitLadder.ListHeight(_area) - (_addRow?.Element.Visibility == Visibility.Visible ? FamilyShape.Row1 : 0);
        _scroll.MaxHeight = total <= room + Disclosure.Tolerance ? double.PositiveInfinity : KitChecks.WholeLineCap(bottoms, room, headings);
    }

    /// <summary>[S-784] A group's heading (the done ones): one row of 32, BodyStrong, secondary.</summary>
    private static Grid CheckHeading(string text)
    {
        var heading = Family.Text(FamilyType.BodyStrong, ColourRole.TextSecondary, text);
        heading.VerticalAlignment = VerticalAlignment.Center;
        return new Grid { Children = { heading }, Height = FamilyShape.Row1, Padding = Family.SideInset };
    }

    /// <summary>
    /// [S-7037] One row, a Row block: the check as its lead, the title as the name (BodyStrong when
    /// emphasised and open; struck through and tertiary when done), the due as its value in the item's
    /// tone, the subtitle (a list's name) on its second line where the plan gives two.
    /// </summary>
    private Border CheckRow(KitItem item, int index)
    {
        var done = item.Lead.IsDone;
        var marker = CheckMarker(done, item.Lead.Tone, index);
        var block = new RowBlock(RowKind.List) { Lines = _plan.Lines, Strong = item.Emphasis && !done };
        block.Show(TitleText(item), _plan.When ? DueText(item) : null, _plan.Subtitle ? item.Subtitle : null);
        block.Lead(marker);

        // [S-186] Done is struck through, not merely dimmed: the one mark that says "done" without a
        // legend and without relying on colour. Late says so in words as well as in its tone, and
        // only for open work.
        if (done)
        {
            block.NameText.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
            block.NameText.Foreground = Family.Brush(ColourRole.TextTertiary);
        }
        else
        {
            block.ValueText.Foreground = Family.Brush(ToneRole(item.Tone, ColourRole.TextSecondary));
        }

        var row = new Border { Child = block, Tag = index };
        ItemFill.Paint(row, ItemRole.Row);
        _checksByRow[index] = marker;

        _rowHooks.Add(Fluent.Hover(row));
        _rowHooks.Add(ItemMenu.Attach(row, () => Entries(item)));
        WidgetItems.Mark(row); // [S-814]

        if (item.Tooltip is { } tip)
        {
            ToolTipService.SetToolTip(row, tip);
        }

        row.Tapped += OnCheckRowTapped;
        row.DoubleTapped += OnRowDoubleTapped;
        HookDrag(row);
        return row;
    }

    private string? DueText(KitItem item)
    {
        if (item.Due is not { } due)
        {
            return null;
        }

        if (_plan.UrgentOnly)
        {
            return due.Urgent ? due.Short : null;
        }

        return _plan.LongWhen ? due.Long : due.Short;
    }

    /// <summary>
    /// [S-597] The check, which is also the one control that toggles the item: an empty ring for open
    /// work, a filled tick for done. Drawn rather than taken from the icon font (there is no hollow
    /// circle in it), sized from the row height, never from the tile.
    /// </summary>
    /// <summary>
    /// [S-780] The check: a 16 ring (stroke 2, in the item's tone when it is late, else secondary), the
    /// accent's filled circle with the tick on it when done (a checked box is the accent's to say).
    /// </summary>
    private Border CheckMarker(bool done, KitTone tone, int index)
    {
        var marker = new Border
        {
            Width = FamilyShape.Icon,
            Height = FamilyShape.Icon,
            CornerRadius = Family.Round(FamilyShape.Icon, FamilyShape.Icon),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = index,
        };

        PaintCheck(marker, done, tone);
        ToolTipService.SetToolTip(marker, CheckTooltip(index));
        marker.Tapped += OnCheckTapped;
        marker.DoubleTapped += OnCheckDoubleTapped;
        _rowHooks.Add(new Unhook(() =>
        {
            marker.Tapped -= OnCheckTapped;
            marker.DoubleTapped -= OnCheckDoubleTapped;
        }));
        return marker;
    }

    private static void PaintCheck(Border marker, bool done, KitTone tone)
    {
        marker.BorderBrush = Family.Brush(ToneRole(tone, ColourRole.TextSecondary));
        marker.BorderThickness = new Thickness(done ? 0 : FamilyShape.Stroke);
        marker.Background = Family.Brush(done ? ColourRole.Accent : ColourRole.Clear);
        marker.Child = done ? Family.Glyph(Glyphs.Done, FamilyShape.Icon, ColourRole.TextOnAccent) : null;
    }

    private string? CheckTooltip(int index)
        => index < _items.Count ? _actions(_items[index]).FirstOrDefault(a => a.Kind == KitActionKind.Toggle)?.Label : null;

    /// <summary>[S-597] A click on the check toggles the item, drawn at once; the widget's write follows.</summary>
    private void OnCheckTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        if (sender is Border { Tag: int index } marker && index < _items.Count)
        {
            Toggle(index, marker);
        }
    }

    /// <summary>Two quick ticks are two ticks, not a rename: the row must not see them as a double-click.</summary>
    private static void OnCheckDoubleTapped(object sender, DoubleTappedRoutedEventArgs args) => args.Handled = true;

    /// <summary>[S-597] Toggle an item: its check flips on screen first (optimistic), then its action runs.</summary>
    private bool Toggle(int index, Border? marker = null)
    {
        var item = _items[index];
        if (_actions(item).FirstOrDefault(a => a.Kind == KitActionKind.Toggle && a.Runnable) is not { } toggle)
        {
            return false;
        }

        if ((marker ?? _checksByRow.GetValueOrDefault(index)) is { } check)
        {
            PaintCheck(check, !item.Lead.IsDone, item.Lead.Tone);
        }

        Run(item, toggle);
        return true;
    }

    /// <summary>[S-787] A click on a row (not its check) focuses it, and is the row's ([S-2004]).</summary>
    private void OnCheckRowTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;

        if (ConsumeDragEnd() || _editor is not null || sender is not Border { Tag: int index })
        {
            return;
        }

        Focus(index);
    }

    // ---- the editors ([S-599], [S-600], [S-788]) -----------------------------------------------

    /// <summary>
    /// [S-600] Rename in place: the title becomes a box holding the whole title. Enter or leaving it
    /// commits, Escape cancels, Tab commits and opens the next row's box.
    /// </summary>
    public void BeginRename(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var index = IndexOf(item.Id);
        if (!Checked || index < 0 || index >= _rowElements.Count || _rowElements[index].Child is not RowBlock body)
        {
            return;
        }

        CancelEdit();

        var box = NewEditor(item.Title);
        SetColumn(box, 1);
        SetColumnSpan(box, 3);

        // The title's column takes the box; the check stays as it was.
        _editorHidden.Clear();
        foreach (var child in body.Children.OfType<FrameworkElement>())
        {
            if (GetColumn(child) >= 1 && child.Visibility == Visibility.Visible)
            {
                child.Visibility = Visibility.Collapsed;
                _editorHidden.Add(child);
            }
        }

        body.Children.Add(box);
        _editor = (box, item.Id);
        StartTyping(box);
    }

    private void OpenAddEditor()
    {
        if (_editor is { Id: null } || _addRow is null)
        {
            return;
        }

        CancelEdit();

        var box = NewEditor(string.Empty);
        box.PlaceholderText = _spec.AddPlaceholder?.Invoke() ?? string.Empty;
        _editorHidden.Clear();
        _editorHidden.AddRange(_addRow.Open(box));
        _editor = (box, null);
        StartTyping(box);
    }

    private TextBox NewEditor(string text)
    {
        var box = new TextBox
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };

        box.KeyDown += OnEditorKeyDown;
        box.PreviewKeyDown += OnEditorPreviewKeyDown;
        box.LostFocus += OnEditorLostFocus;
        box.GotFocus += OnEditorGotFocus;
        return box;
    }

    /// <summary>
    /// Ask for the keyboard first: outside edit mode the surface is unfocusable, so without this the
    /// caret would blink here while the letters went to whatever window had focus.
    /// </summary>
    private void StartTyping(TextBox box)
    {
        TextInput?.Invoke(true);
        box.Focus(FocusState.Programmatic);
        box.SelectionStart = box.Text.Length;
    }

    private void OnEditorGotFocus(object sender, RoutedEventArgs args) => TextInput?.Invoke(_editor is not null);

    private void OnEditorKeyDown(object sender, KeyRoutedEventArgs args)
    {
        switch (args.Key)
        {
            case VirtualKey.Enter:
                args.Handled = true;
                CommitEdit(keepAdding: true);
                break;

            case VirtualKey.Escape:
                args.Handled = true;
                CancelEdit();
                Redraw();
                break;
        }
    }

    /// <summary>
    /// [S-788] Tab, caught before focus navigation takes it: in a rename box it commits and opens the
    /// next row's box (none past the last); in the add box it commits and stays open for the next item.
    /// Shift+Tab commits and closes.
    /// </summary>
    private void OnEditorPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Tab || _editor is not { } editor)
        {
            return;
        }

        args.Handled = true;
        var backwards = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

        if (editor.Id is not { } editing)
        {
            CommitEdit(keepAdding: !backwards);
            return;
        }

        var next = KitChecks.NextEditor([.. _items.Select(i => i.Id)], editing, backwards);
        CommitEdit(keepAdding: false);

        if (next is not null && !_detached && IndexOf(next) is var at and >= 0)
        {
            BeginRename(_items[at]);
        }
    }

    private void OnEditorLostFocus(object sender, RoutedEventArgs args) => CommitEdit(keepAdding: false);

    /// <summary>
    /// Commit what the editor holds. Enter in the add row leaves it open for the next item, so a list
    /// can be typed in one go; leaving the box commits and closes it.
    /// </summary>
    private void CommitEdit(bool keepAdding)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        var text = editor.Box.Text;
        CloseEditor();

        if (editor.Id is { } id)
        {
            if (IndexOf(id) is var at and >= 0)
            {
                Renamed?.Invoke(_items[at], text);
            }

            Redraw();
            return;
        }

        Added?.Invoke(text);
        Redraw();

        if (keepAdding && text.Trim().Length > 0 && !_detached)
        {
            OpenAddEditor();
        }
    }

    /// <summary>Take an open editor down without committing it.</summary>
    public void CancelEdit() => CloseEditor();

    /// <summary>Take the editor down without committing it, and give the keyboard back.</summary>
    private void CloseEditor()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        _editor = null;
        editor.Box.KeyDown -= OnEditorKeyDown;
        editor.Box.PreviewKeyDown -= OnEditorPreviewKeyDown;
        editor.Box.LostFocus -= OnEditorLostFocus;
        editor.Box.GotFocus -= OnEditorGotFocus;

        if (editor.Box.Parent is Panel parent)
        {
            parent.Children.Remove(editor.Box);
        }

        // Only what the box hid comes back (the add row's line, a row's texts).
        foreach (var child in _editorHidden)
        {
            child.Visibility = Visibility.Visible;
        }

        _editorHidden.Clear();

        TextInput?.Invoke(false);
    }

    /// <summary>Draw again what the list last showed, or the show held back meanwhile (an editor closed, a drag let go).</summary>
    private void Redraw()
    {
        if (_detached)
        {
            return;
        }

        if (_held is { } held)
        {
            Show(held.Items, held.Plan, held.Area, held.Now);
            return;
        }

        Show(_items, _plan, _area, Clock());
    }

    // ---- the drag ([S-786]) ---------------------------------------------------------------------

    private void HookDrag(Border row)
    {
        if (Reordered is null)
        {
            return;
        }

        row.PointerPressed += OnRowPointerPressed;
        row.PointerMoved += OnRowPointerMoved;
        row.PointerReleased += OnRowPointerReleased;
        row.PointerCaptureLost += OnRowPointerCaptureLost;
        _rowHooks.Add(new Unhook(() =>
        {
            row.PointerPressed -= OnRowPointerPressed;
            row.PointerMoved -= OnRowPointerMoved;
            row.PointerReleased -= OnRowPointerReleased;
            row.PointerCaptureLost -= OnRowPointerCaptureLost;
        }));
    }

    private bool ConsumeDragEnd()
    {
        var ended = _dragEnded;
        _dragEnded = false;
        return ended;
    }

    private void OnRowPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        _dragEnded = false;

        if (sender is not Border { Tag: int index } row || _editor is not null || _dragging || _checks is null)
        {
            return;
        }

        var point = args.GetCurrentPoint(row);

        // The check is a button of its own: a press there toggles the item and arms nothing.
        if (!point.Properties.IsLeftButtonPressed || IsInCheck(args.OriginalSource as DependencyObject, row, index))
        {
            return;
        }

        _pressRow = row;
        _pressPointer = args.Pointer.PointerId;
        _pressAt = args.GetCurrentPoint(_checks).Position;
        _grab = point.Position;
    }

    private void OnRowPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_pressRow is not { Tag: int index } row || !ReferenceEquals(sender, row) || args.Pointer.PointerId != _pressPointer || _checks is not { } panel || index >= _items.Count)
        {
            return;
        }

        if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
        {
            // Released somewhere this row did not hear about.
            _pressRow = null;
            return;
        }

        var at = args.GetCurrentPoint(panel).Position;

        if (!_dragging)
        {
            if (!KitChecks.IsDrag(at.X - _pressAt.X, at.Y - _pressAt.Y) || !row.CapturePointer(args.Pointer))
            {
                return;
            }

            _dragging = true;
            ToolTipService.SetToolTip(row, null);
            panel.Lift(row);
            ItemFill.Paint(row, ItemRole.Row, ItemState.Hover);
        }

        // One column: the row moves up and down only. Two: it follows the pointer between them.
        var x = panel.Columns > 1 ? at.X - _grab.X : 0;
        panel.MoveLifted(new Point(x, at.Y - _grab.Y));

        var group = _items[index].Group?.Key;
        var current = panel.SlotOf(row);
        var target = KitChecks.Retarget(panel.Slots, current, at.X, at.Y,
            slot => panel.ChildAt(slot) is Border { Tag: int other } && other < _items.Count && _items[other].Group?.Key == group);

        if (target != current)
        {
            panel.MoveTo(row, target);
        }

        AutoScroll(args);
        args.Handled = true;
    }

    /// <summary>Near an edge of the viewport the list scrolls a little with each move, so a drag can reach a row out of sight.</summary>
    private void AutoScroll(PointerRoutedEventArgs args)
    {
        var y = args.GetCurrentPoint(_scroll).Position.Y;
        var step = y < AutoScrollEdge ? -FamilySpace.S2 : y > _scroll.ActualHeight - AutoScrollEdge ? FamilySpace.S2 : 0;

        if (step != 0)
        {
            _scroll.ChangeView(null, Math.Clamp(_scroll.VerticalOffset + step, 0, _scroll.ScrollableHeight), null, disableAnimation: true);
        }
    }

    private void OnRowPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging && ReferenceEquals(sender, _pressRow))
        {
            args.Handled = true;
            FinishDrag(commit: true);
        }

        _pressRow = null;
    }

    /// <summary>Capture taken away mid-drag (edit mode switched on, the window lost): the rows go back.</summary>
    private void OnRowPointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        if (_dragging && ReferenceEquals(sender, _pressRow))
        {
            FinishDrag(commit: false);
            _pressRow = null;
        }
    }

    private void FinishDrag(bool commit)
    {
        if (_pressRow is not { } row || _checks is not { } panel)
        {
            return;
        }

        _dragging = false;
        _dragEnded = true;
        row.ReleasePointerCaptures();
        ItemFill.Paint(row, ItemRole.Row);

        var ids = panel.InSlotOrder().OfType<Border>().Select(b => b.Tag).OfType<int>().Where(i => i < _items.Count).Select(i => _items[i].Id).ToList();
        panel.Settle();

        if (commit && !ids.SequenceEqual(_items.Select(i => i.Id)))
        {
            Reordered?.Invoke(ids);
            return;
        }

        // Let go without a move: the rows go back, and a show held back while the row was lifted (a
        // refresh, a tick) happens now.
        panel.ResetOrder();
        Redraw();
    }

    private bool IsInCheck(DependencyObject? source, Border row, int index)
    {
        var check = _checksByRow.GetValueOrDefault(index);

        for (var node = source; node is not null && !ReferenceEquals(node, row); node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, check))
            {
                return true;
            }
        }

        return false;
    }

    private void CancelDrag()
    {
        if (_dragging)
        {
            FinishDrag(commit: false);
        }

        _pressRow = null;
        _dragEnded = false;
    }

    /// <summary>[S-3802] Move the item at <paramref name="index"/> one place; the order goes to the widget.</summary>
    private bool Move(int index, int step)
    {
        if (Reordered is null || index < 0 || index >= _items.Count)
        {
            return false;
        }

        var ids = _items.Select(i => i.Id).ToList();
        var to = index + step;
        if (to < 0 || to >= ids.Count || _items[to].Group?.Key != _items[index].Group?.Key)
        {
            return false;
        }

        (ids[index], ids[to]) = (ids[to], ids[index]);
        Reordered(ids);
        return true;
    }
}
