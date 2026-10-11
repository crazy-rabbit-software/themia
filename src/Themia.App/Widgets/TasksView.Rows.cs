using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Windows.System;
using Windows.UI.Core;
using Themia.Core.Shell;

namespace Themia.App.Widgets;

/// <summary>The rows, and everything the user does to one.</summary>
internal sealed partial class TasksView
{
    private readonly AddFooterRow _addRow;

    // One per row, so the handlers come off with the row they belong to. A leaked one keeps the row
    // alive and re-lights it after the list has moved on.
    private readonly List<IDisposable> _hovers = [];
    private readonly List<(Border Row, Border Marker)> _rows = [];

    /// <summary>The rows as drawn, in order: what "move up" and a new task's place are measured against.</summary>
    private IReadOnlyList<TaskItem> _displayed = [];

    /// <summary>The layout the rows on screen were drawn for; a new size that changes it redraws them.</summary>
    private TaskLayoutPlan? _drawnPlan;

    /// <summary>The open editor, if any: renaming a task (its id) or adding one (null id).</summary>
    private (TextBox Box, string? TaskId)? _editor;

    /// <summary>[S-787] A row holds the keyboard focus (the user clicked it): arrows, Enter, Delete, F2 work.</summary>
    private bool _rowFocused;

    /// <summary>The task the focus is on, so a redraw (a tick, a refresh) puts it back on the same task.</summary>
    private string? _focusTaskId;

    /// <summary>Where the focus was, for when its task has gone (deleted): the row now in that place.</summary>
    private int _focusIndex;

    /// <summary>Rows being torn down and rebuilt: the focus leaving them is not the user leaving the list.</summary>
    private bool _rebuilding;

    private void BuildRows(IReadOnlyList<TaskItem> rows, DateOnly today, TaskLayoutPlan plan)
    {
        // A rebuild while the user types would throw their words away; the editor's own commit or
        // cancel redraws when it is done.
        // Nor while a row is lifted: the row under the pointer would vanish mid-drag. The drop redraws.
        if (_dragging)
        {
            return;
        }

        if (_editor is { TaskId: { } editing })
        {
            if (rows.Any(r => r.Id == editing))
            {
                return;
            }

            // The task being renamed has gone (deleted elsewhere): its editor goes with it.
            CancelEdit();
        }

        _rebuilding = true;
        try
        {
            ClearRows();
            _drawnPlan = plan;
            _rowsPanel.Columns = plan.Columns;

            // A tile with no add row and no height to spare spends the list's foot padding on a row.
            _scroll.Padding = plan.AddRow ? Fluent.ListPadding : new Thickness(0);

            var doneCount = rows.Count(t => t.Completed);

            foreach (var task in rows)
            {
                // [S-784] Completed tasks follow the open ones under their own heading, where a
                // heading earns its line.
                if (task.Completed && plan.CompletedHeading && ReferenceEquals(task, rows.First(t => t.Completed)))
                {
                    _rowsPanel.Children.Add(Heading(Str.WidgetsC_Tasks_CompletedHeading.Format(("count", doneCount))));
                }

                _rowsPanel.Children.Add(BuildRow(task, today, plan));
            }

            _rowsPanel.ResetOrder();
            RestoreFocus();
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private static TextBlock Heading(string text)
    {
        var heading = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        heading.Text = text;
        heading.Margin = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, Fluent.Space1);
        return heading;
    }

    /// <summary>
    /// One task: the state marker, the title wrapping to the plan's lines ([S-601]) with the "!" of
    /// an important task after it ([S-785]), the list it is from under Today on a panel ([S-789]), and
    /// as much of the due date as the width earns, under the title or beside it ([S-780]).
    /// </summary>
    private Border BuildRow(TaskItem task, DateOnly today, TaskLayoutPlan plan)
    {
        var markerSize = plan.CompactRows ? Fluent.IconSize : Fluent.Space5;
        var due = TaskDue.Resolve(task.DueRaw, today, TimeZoneInfo.Local);

        // [S-780] An open task that is late rings in the critical colour: the cue that survives on
        // a tile with no room for the word.
        var marker = BuildMarker(task, markerSize, due is { IsPast: true } && !task.Completed && _config.ShowDueDate);
        var inset = plan.NarrowInset ? Fluent.Space2 : Fluent.Space4;

        if (plan.NarrowInset)
        {
            marker.Margin = new Thickness(0, 0, Fluent.Space2, 0);
        }
        var important = task.Important && !task.Completed;

        // Weight marks an important task the way an unread message is marked, and the glyph says it
        // to a reader who does not notice weight.
        var title = Fluent.Text(
            important ? FluentType.BodyStrong : FluentType.Body,
            task.Completed ? "TextFillColorTertiaryBrush" : "TextFillColorPrimaryBrush");

        title.Text = task.DisplayTitle;
        title.TextWrapping = TextWrapping.WrapWholeWords;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.MaxLines = plan.TitleLines;

        // [S-186] Done is struck through, not merely dimmed: the one mark that says "done" without
        // a legend and without relying on colour.
        title.TextDecorations = task.Completed ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None;

        var titleLine = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        Grid.SetColumn(title, 1);
        titleLine.Children.Add(title);

        if (important && plan.PriorityGlyph)
        {
            // [S-785] The classic "!" badge, in the critical colour, leading the title: never cut
            // off by the ellipsis, and read before the words it qualifies.
            var bang = Fluent.Text(FluentType.BodyStrong, "SystemFillColorCriticalBrush");
            bang.Text = "!";
            bang.Margin = new Thickness(0, 0, Fluent.Space1 + (Fluent.Space1 / 2), 0);
            bang.VerticalAlignment = VerticalAlignment.Top;
            titleLine.Children.Add(bang);
        }

        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(titleLine);

        if (plan.ListNames && _listNames.TryGetValue(task.ListId, out var listName))
        {
            var list = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            list.Text = listName;
            content.Children.Add(list);
        }

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        Grid.SetColumn(marker, 0);
        Grid.SetColumn(content, 1);
        body.Children.Add(marker);
        body.Children.Add(content);

        if (DueText(task, due, plan.Due) is { } text)
        {
            // [S-186] Overdue says so in words as well as in the critical colour, and only for open
            // work: a finished task is not late.
            var overdue = due!.Value.IsPast && !task.Completed;
            var when = Fluent.Text(FluentType.Caption, overdue ? "SystemFillColorCriticalBrush" : "TextFillColorTertiaryBrush");
            when.Text = text;

            if (plan.DueBeside)
            {
                when.VerticalAlignment = VerticalAlignment.Center;
                when.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
                Grid.SetColumn(when, 2);
                body.Children.Add(when);
            }
            else
            {
                content.Children.Add(when);
            }
        }

        var row = Fluent.Row(body, plan.CompactRows);

        // The title may wrap; a little air above and below keeps two wrapped rows apart without a rule.
        row.Padding = new Thickness(inset, Fluent.Space1, inset, Fluent.Space1);
        row.Tag = task;

        // [S-787] Focusable, so a clicked row can take the arrows; the system focus rect shows which.
        row.IsTabStop = true;
        row.UseSystemFocusVisuals = true;

        _hovers.Add(Fluent.Hover(row));
        _rows.Add((row, marker));

        row.Tapped += OnRowTapped;
        row.DoubleTapped += OnRowDoubleTapped;
        row.RightTapped += OnRowRightTapped;
        row.LostFocus += OnRowLostFocus;
        HookDrag(row);

        ToolTipService.SetToolTip(row, Tooltip(task, due));
        return row;
    }

    /// <summary>[S-780] The due text a row shows at this width, or null for none.</summary>
    private static string? DueText(TaskItem task, Due? due, DueDetail detail)
    {
        if (due is not { } resolved || detail == DueDetail.None)
        {
            return null;
        }

        // A finished task that was due in the past shows its date, not "Overdue".
        if (resolved.IsPast && task.Completed)
        {
            return detail == DueDetail.Urgent ? null : TaskDue.ShortDate(resolved.Day);
        }

        return detail switch
        {
            DueDetail.Urgent => TaskDue.IsUrgent(resolved) ? TaskDue.Text(resolved) : null,
            DueDetail.Long => TaskDue.WideText(resolved),
            _ => TaskDue.Text(resolved),
        };
    }

    /// <summary>
    /// [S-597] The state marker, which is also the one control that completes a task: an empty ring
    /// for open work, a filled tick for finished. Drawn rather than taken from the icon font, because
    /// there is no hollow circle in it, and sized from the row height, never from the tile.
    /// </summary>
    private Border BuildMarker(TaskItem task, double size, bool overdue)
    {
        var marker = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Fluent.Space3, 0),
            BorderBrush = overdue
                ? FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.IndianRed)
                : FluentLayout.Themed("ControlStrongStrokeColorDefaultBrush", Colors.Gray),
            BorderThickness = new Thickness(task.Completed ? 0 : 1),
            Background = task.Completed
                ? FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue)
                : Fluent.RowRest(),
            Tag = task,
        };

        if (task.Completed)
        {
            marker.Child = new FontIcon
            {
                Glyph = Glyphs.Done,
                FontSize = Math.Round(size * 0.6),
                Foreground = FluentLayout.Themed("TextOnAccentFillColorPrimaryBrush", Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        ToolTipService.SetToolTip(marker, (task.Completed ? Str.WidgetsC_Tasks_MarkNotDone : Str.WidgetsC_Tasks_MarkDone).Text());
        marker.Tapped += OnMarkerTapped;
        marker.DoubleTapped += OnMarkerDoubleTapped;
        return marker;
    }

    private static string Tooltip(TaskItem task, Due? due)
    {
        var lines = new List<string> { task.DisplayTitle, (task.Completed ? Str.WidgetsC_Tasks_StateCompleted : Str.WidgetsC_Tasks_StateOpen).Text() };

        if (due is { } resolved)
        {
            lines.Add((resolved.IsPast && !task.Completed ? Str.WidgetsC_Tasks_DueOnOverdue : Str.WidgetsC_Tasks_DueOn).Format(("date", TaskDue.LongText(resolved))));
        }

        if (task.Important)
        {
            lines.Add(Str.WidgetsC_Tasks_HighPriority.Text());
        }

        lines.Add(Str.WidgetsC_Tasks_RowHint.Text());
        return string.Join(Environment.NewLine, lines);
    }

    private void ClearRows()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        foreach (var (row, marker) in _rows)
        {
            row.Tapped -= OnRowTapped;
            row.DoubleTapped -= OnRowDoubleTapped;
            row.RightTapped -= OnRowRightTapped;
            row.LostFocus -= OnRowLostFocus;
            UnhookDrag(row);
            marker.Tapped -= OnMarkerTapped;
            marker.DoubleTapped -= OnMarkerDoubleTapped;
        }

        _hovers.Clear();
        _rows.Clear();
        _rowsPanel.Children.Clear();
    }

    /// <summary>The task rows in list order, without the section heading.</summary>
    private List<Border> TaskRows() => [.. _rowsPanel.InSlotOrder().OfType<Border>().Where(b => b.Tag is TaskItem)];

    // ---- what a click does ---------------------------------------------------------------------------

    private void OnMarkerTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;

        if (sender is FrameworkElement { Tag: TaskItem task })
        {
            SetCompleted(task, !task.Completed);
        }
    }

    /// <summary>Two quick ticks are two ticks, not a rename: the row must not see them as a double-click.</summary>
    private static void OnMarkerDoubleTapped(object sender, DoubleTappedRoutedEventArgs args) => args.Handled = true;

    /// <summary>[S-787] A click on a row (not its marker) gives the list the keyboard, focused on that row.</summary>
    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        if (ConsumeDragEnd() || _editor is not null || sender is not Border row)
        {
            return;
        }

        FocusRow(row);
    }

    private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: TaskItem task } row)
        {
            args.Handled = true;
            BeginRename(row, task);
        }
    }

    /// <summary>[S-597]/[S-598]/[S-600] A task's own menu, ahead of the card's.</summary>
    private void OnRowRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (sender is not Border { Tag: TaskItem task } row)
        {
            return;
        }

        // [S-3802] No moves: a task is dragged, or moved with Alt+Shift+Up / Down.
        ItemMenu.Show(row, args,
        [
            new(task.Completed ? Glyphs.Undo : Glyphs.Done, (task.Completed ? Str.WidgetsC_Tasks_MarkNotDone : Str.WidgetsC_Tasks_MarkDone).Text(),
                () => SetCompleted(task, !task.Completed)),
            new(Glyphs.Rename, Str.Common_Rename.Text(), () => BeginRename(row, task), Quick: QuickCommand.Rename),
            ItemMenuEntry.Separator,
            new(Glyphs.Delete, Str.Common_Delete.Text(), () => Delete(task), Danger: true, Quick: QuickCommand.Delete),
        ]);
    }

    // ---- the keyboard ([S-787]) ----------------------------------------------------------------------

    /// <summary>
    /// [S-808] Whether an editor is being typed into. The arrows need nothing asked: a click anywhere
    /// in the widget gives it the shell's keyboard lease (<see cref="IWidgetKeys"/>), and an editor
    /// closing keeps it. This is only the text box's share of the same lease.
    /// </summary>
    private void UpdateKeyboard() => _surface?.RequestTextInput(!_detached && _editor is not null);

    private void FocusRow(Border row)
    {
        if (row.Tag is not TaskItem task)
        {
            return;
        }

        _rowFocused = true;
        _focusTaskId = task.Id;
        _focusIndex = Math.Max(0, TaskRows().IndexOf(row));
        row.Focus(FocusState.Keyboard);
        row.StartBringIntoView();
    }

    /// <summary>After a redraw, the focus goes back to its task, or to the row now in its place.</summary>
    private void RestoreFocus()
    {
        if (!_rowFocused || _editor is not null)
        {
            return;
        }

        var rows = TaskRows();
        if (rows.Count == 0)
        {
            ReleaseRowFocus();
            return;
        }

        var row = rows.FirstOrDefault(r => r.Tag is TaskItem t && t.Id == _focusTaskId) ?? rows[Math.Clamp(_focusIndex, 0, rows.Count - 1)];
        FocusRow(row);
    }

    private void ReleaseRowFocus()
    {
        _rowFocused = false;
        _focusTaskId = null;
    }

    /// <summary>
    /// [S-787]/[S-808] A key from the shell's keyboard lease: Up/Down/Home/End move the row focus,
    /// Enter ticks, Delete deletes, F2 renames, Escape (also the lease ending) lets go. With no row
    /// focused yet a movement key lands on an end ([S-812]). An open editor owns its own keys; the
    /// lease ending while one is open only forgets the row, and the box's blur commits it.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        if (_editor is not null)
        {
            if (key == WidgetKey.Escape)
            {
                ReleaseRowFocus();
            }

            return false;
        }

        var rows = TaskRows();
        var index = _rowFocused ? FocusedIndex(rows) : -1;
        var decision = TaskLayout.Decide(key, index, rows.Count);
        var task = decision.Index >= 0 && decision.Index < rows.Count ? rows[decision.Index].Tag as TaskItem : null;

        switch (decision.Command)
        {
            case TaskKeyCommand.Focus:
                FocusRow(rows[decision.Index]);
                return true;

            case TaskKeyCommand.Toggle when task is not null:
                SetCompleted(task, !task.Completed);
                return true;

            case TaskKeyCommand.Delete when task is not null:
                _focusIndex = decision.Index;
                Delete(task);
                return true;

            case TaskKeyCommand.MoveUp or TaskKeyCommand.MoveDown when task is not null:
                Move(task, decision.Command == TaskKeyCommand.MoveUp ? -1 : +1);
                return true;

            case TaskKeyCommand.Rename when task is not null:
                BeginRename(rows[decision.Index], task);
                return true;

            case TaskKeyCommand.Release when _rowFocused:
                ReleaseRowFocus();

                // Rebuilt rows drop the focus rect with the focus.
                _drawnPlan = null;
                Render();
                return true;

            default:
                return false;
        }
    }

    /// <summary>The focused task's row now, or the row in its old place when the task has gone.</summary>
    private int FocusedIndex(List<Border> rows)
    {
        var at = rows.FindIndex(r => r.Tag is TaskItem t && t.Id == _focusTaskId);
        return at >= 0 ? at : Math.Clamp(_focusIndex, 0, Math.Max(0, rows.Count - 1));
    }

    /// <summary>
    /// [S-787] Leaving the list (focus moving to the widget's own buttons, or away with the lease)
    /// drops the row focus. Moving between rows, into an editor, or a redraw replacing the focused
    /// row is not leaving it.
    /// </summary>
    private void OnRowLostFocus(object sender, RoutedEventArgs args)
    {
        if (_rebuilding || !_rowFocused || XamlRoot is null)
        {
            return;
        }

        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        if (focused is Border { Tag: TaskItem } || (_editor is { } editor && ReferenceEquals(focused, editor.Box)))
        {
            return;
        }

        ReleaseRowFocus();
    }

    // ---- changes ---------------------------------------------------------------------------------------

    private void SetCompleted(TaskItem task, bool completed)
    {
        if (_service is null)
        {
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.SetDone(tasks, task.Id, completed));
            }

            return;
        }

        if (_feed is { } feed)
        {
            _ = _service.SetCompletedAsync(feed, task, completed);
        }
    }

    private void Rename(TaskItem task, string title)
    {
        if (_service is null)
        {
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.RenameTask(tasks, task.Id, title));
            }

            return;
        }

        if (_feed is { } feed)
        {
            _ = _service.RenameAsync(feed, task, title);
        }
    }

    private void Delete(TaskItem task)
    {
        if (_service is null)
        {
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.RemoveTask(tasks, task.Id));
            }

            return;
        }

        if (_feed is { } feed)
        {
            _ = _service.DeleteAsync(feed, task);
        }
    }

    /// <summary>[S-598] Move a task one place among the rows shown.</summary>
    private void Move(TaskItem task, int step)
    {
        var shown = _displayed.Select(t => t.Id).ToList();
        CommitOrder(TaskRules.Nudge(shown, task.Id, step));
    }

    /// <summary>
    /// [S-598]/[S-786] Keep a new arrangement of the rows shown, from the menu or a drag. A list on
    /// this PC stores its order in the list; an account list's order lives in this widget's config,
    /// because no provider keeps one. An unchanged order writes nothing.
    /// </summary>
    private void CommitOrder(IReadOnlyList<string> ids)
    {
        var shown = _displayed.Select(t => t.Id).ToList();

        if (TaskRules.SameOrder(ids, shown) || _service is null)
        {
            _rowsPanel.ResetOrder();
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.Reorder(tasks, ids));
            }

            return;
        }

        _config.TaskOrder = [.. ids];
        _surface?.RequestSave();
        Render();
    }

    private async Task AddAsync(string title)
    {
        if (_service is null || title.Trim().Length == 0)
        {
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.AddTask(tasks, title, $"local-{Guid.NewGuid():n}"));
            }

            return;
        }

        if (_feed is not { } feed)
        {
            return;
        }

        var created = await _service.CreateAsync(feed, title).ConfigureAwait(true);

        if (created is null || _detached || !ReferenceEquals(feed, _feed))
        {
            return;
        }

        // [S-599] Pin the arrangement on screen with the new task last. Without this the next
        // refresh re-imposes the provider's own sort — Graph lists most recently changed first —
        // which is how a task just added kept jumping to the top. Rebuilt from the rows shown, so
        // ids of deleted tasks prune themselves rather than accumulate.
        var order = _displayed.Select(t => t.Id).Where(id => id != created.Id).ToList();
        order.Add(created.Id);
        _config.TaskOrder = order;
        _surface?.RequestSave();
        Render();
    }

    // ---- the editors -----------------------------------------------------------------------------------

    /// <summary>
    /// [S-600] Rename in place: the title becomes a text box holding the whole title. Enter or
    /// leaving it commits, Escape cancels; a blank or unchanged title commits nothing.
    /// </summary>
    private void BeginRename(Border row, TaskItem task)
    {
        if (_feed?.Edits.IsUnconfirmedCreate(task.Id) == true || row.Child is not Grid body)
        {
            return;
        }

        CancelEdit();

        var box = NewEditor(task.Title);
        Grid.SetColumn(box, 1);
        Grid.SetColumnSpan(box, 2);

        // The title's column takes the box; the rest of the row stays as it was.
        foreach (var child in body.Children)
        {
            if (Grid.GetColumn((FrameworkElement)child) >= 1)
            {
                child.Visibility = Visibility.Collapsed;
            }
        }

        body.Children.Add(box);
        _editor = (box, task.Id);
        StartTyping(box);
    }

    /// <summary>[S-599]/[S-3850] The shared add footer, under the list.</summary>
    private AddFooterRow BuildAddRow() => new(Str.WidgetsC_Tasks_AddTask.Text(), OpenAddEditor);

    private void OpenAddEditor()
    {
        if (_editor is { TaskId: null })
        {
            return;
        }

        CancelEdit();

        var box = NewEditor(string.Empty);
        box.PlaceholderText = Str.WidgetsC_Tasks_TaskName.Text();
        _addRow.Open(box);
        _editor = (box, null);
        StartTyping(box);
    }

    /// <summary>
    /// [S-3850] The add footer shows when the plan has room for it and there is a list to add to: on a
    /// small tile every row is worth more as a task ([S-780]).
    /// </summary>
    private void FitAddRow(TaskLayoutPlan plan)
        => _addRow.Fit(
            (plan.AddRow && _screen == Screen.Ready && Writable) || _editor is { TaskId: null },
            plan.CompactRows,
            plan.NarrowInset ? Fluent.Space2 : Fluent.Space4);

    private bool Writable => IsLocal ? _local?.Writable == true && _localListId is not null : _feed is { Loaded: true };

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
    /// Ask the surface for the keyboard first: outside edit mode it is unfocusable, so without this
    /// the caret would blink here while the letters went to whatever window had focus.
    /// </summary>
    private void StartTyping(TextBox box)
    {
        UpdateKeyboard();
        box.Focus(FocusState.Programmatic);
        box.SelectionStart = box.Text.Length;
    }

    private void OnEditorGotFocus(object sender, RoutedEventArgs args) => UpdateKeyboard();

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
                Render();
                break;
        }
    }

    /// <summary>
    /// [S-788] Tab, caught before focus navigation takes it: in a rename box it commits and opens the
    /// next task's box (none past the last); in the add box it commits and stays open for the next
    /// task. Shift+Tab commits and closes.
    /// </summary>
    private void OnEditorPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Tab || _editor is not { } editor)
        {
            return;
        }

        args.Handled = true;
        var backwards = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

        if (editor.TaskId is not { } editing)
        {
            CommitEdit(keepAdding: !backwards);
            return;
        }

        var next = TaskLayout.NextEditor([.. _displayed.Select(t => t.Id)], editing, backwards);
        CommitEdit(keepAdding: false);

        if (next is not null && !_detached
            && TaskRows().FirstOrDefault(r => r.Tag is TaskItem t && t.Id == next) is { Tag: TaskItem task } row)
        {
            BeginRename(row, task);
        }
    }

    private void OnEditorLostFocus(object sender, RoutedEventArgs args) => CommitEdit(keepAdding: false);

    /// <summary>
    /// Commit what the editor holds. Enter in the add row leaves it open for the next task, so a list
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

        if (editor.TaskId is { } taskId)
        {
            if (_displayed.FirstOrDefault(t => t.Id == taskId) is { } task)
            {
                Rename(task, text);
            }

            Render();
            return;
        }

        _ = AddAsync(text);
        Render();

        if (keepAdding && text.Trim().Length > 0 && !_detached)
        {
            OpenAddEditor();
        }
    }

    private void CancelEdit() => CloseEditor();

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

            foreach (var child in parent.Children)
            {
                child.Visibility = Visibility.Visible;
            }
        }

        UpdateKeyboard();
    }

    private void DetachAddRow()
    {
        _addRow.Detach();
    }
}
