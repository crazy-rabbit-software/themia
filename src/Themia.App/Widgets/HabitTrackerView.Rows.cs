using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.System;
using Windows.UI.Core;
using Themia.Core.Shell;

namespace Themia.App.Widgets;

/// <summary>
/// The rows of the Habits widget, and everything done to them in place: the box ([S-1421]), add,
/// rename, move and delete ([S-1425]), the item menu, and the keys ([S-1428]).
/// </summary>
internal sealed partial class HabitTrackerView
{
    private readonly List<HabitRow> _rows = [];
    private readonly AddFooterRow _addRow;

    /// <summary>The open text box: renaming the habit with this id, or (null id) adding one.</summary>
    private (TextBox Box, string? HabitId)? _editor;

    private bool _rowFocused;
    private string? _focusId;
    private int _focusIndex;

    /// <summary>Whether a habit can be added or changed here: a list is chosen and the pool can be written.</summary>
    private bool Writable => _pool is { Writable: true } && _habitList is not null;

    /// <summary>
    /// Keep one row per habit, in list order. Rows are reused by id, so a tick, a rename or a period
    /// rollover re-letters what is there and keeps the row under the pointer; only a change to which
    /// habits there are, or their order, rebuilds.
    /// </summary>
    private void SyncRows(IReadOnlyList<Habit> habits)
    {
        var same = habits.Count == _rows.Count;

        for (var i = 0; same && i < habits.Count; i++)
        {
            same = habits[i].Id == _rows[i].Id;
        }

        if (same)
        {
            return;
        }

        // A rebuild under a lifted row ends the drag: the rows it was shifting are being replaced.
        CancelDrag();

        // A rename open on a habit that has gone has nothing left to commit onto.
        if (_editor is { HabitId: { } editing } && habits.All(h => h.Id != editing))
        {
            CloseEditor();
        }

        var keep = _editor is { HabitId: { } open } ? open : null;
        var reuse = _rows.ToDictionary(r => r.Id, StringComparer.Ordinal);

        _rows.Clear();
        _list.Children.Clear();

        foreach (var habit in habits)
        {
            if (!reuse.Remove(habit.Id, out var row))
            {
                row = new HabitRow(this, habit.Id);
            }

            _rows.Add(row);
            _list.Children.Add(row.Host);
        }

        foreach (var gone in reuse.Values)
        {
            gone.Detach();
        }

        RestoreFocus(keep);
    }

    private HabitRow? RowFor(string id) => _rows.FirstOrDefault(r => r.Id == id);

    private Habit? HabitFor(string id) => Habits.FirstOrDefault(h => h.Id == id);

    // ---- changes: every one goes through the pool ---------------------------------------------------

    /// <summary>
    /// [S-1421] Tick or un-tick the period the box shows. The clock is read once, here, and the
    /// period it gives is both written and drawn — writing one period while drawing another left the
    /// original's box dead for a tick after every rollover.
    /// </summary>
    private void Toggle(string habitId)
    {
        if (!Writable || _habitList is not { } list)
        {
            return;
        }

        var key = Bucket(DateTimeOffset.Now).Key;
        _pool!.Update(list.Id, habits => HabitLists.ToggleHabit(habits, habitId, key));
    }

    /// <summary>[S-1425] Move a habit one place; the first can't go up, the last can't go down (a no-op writes nothing).</summary>
    private void Move(string habitId, int delta)
    {
        if (Writable && _habitList is { } list)
        {
            _focusId = habitId;
            _pool!.Update(list.Id, habits => HabitLists.MoveHabit(habits, habitId, delta));
        }
    }

    /// <summary>
    /// [S-1425] Delete a habit and its log, without a dialog: it sits behind a right-click or a
    /// focused row's Delete key, never a stray click. An editor open on it is cancelled first, so a
    /// pending rename cannot commit onto a habit that no longer exists.
    /// </summary>
    private void Delete(string habitId)
    {
        if (!Writable || _habitList is not { } list)
        {
            return;
        }

        if (_editor is { HabitId: { } editing } && editing == habitId)
        {
            CloseEditor();
        }

        var index = Habits.ToList().FindIndex(h => h.Id == habitId);
        _focusIndex = HabitLayout.AfterDelete(index, Habits.Count);
        _focusId = null;
        _pool!.Update(list.Id, habits => HabitLists.RemoveHabit(habits, habitId));
    }

    private void Rename(string habitId, string name)
    {
        if (Writable && _habitList is { } list)
        {
            _pool!.Update(list.Id, habits => HabitLists.RenameHabit(habits, habitId, name));
        }
    }

    private void Add(string name)
    {
        if (Writable && _habitList is { } list)
        {
            _pool!.Update(list.Id, habits => HabitLists.AddHabit(habits, name, HabitLists.NewId("habit")));
        }
    }

    /// <summary>[S-1425] A habit's own menu, ahead of the card's: tick, rename, delete. No moves: a habit is dragged, or moved with Alt+Shift+Up / Down ([S-3802]).</summary>
    private IEnumerable<ItemMenuEntry> MenuFor(string habitId)
    {
        if (!Writable || HabitFor(habitId) is not { } habit)
        {
            return [];
        }

        var done = HabitRules.IsDone(HabitRules.Log(habit.Log), _current);
        var noun = HabitPeriods.CurrentNoun(_config.Cadence.Period);

        return
        [
            new(done ? Glyphs.Undo : Glyphs.Done, done ? Str.WidgetsB_Habits_NotDone.Format(("when", noun)) : Str.WidgetsB_Habits_Done.Format(("when", noun)), () => Toggle(habitId)),
            new(Glyphs.Rename, Str.WidgetsB_Folder_Rename.Text(), () => BeginRename(habitId), Quick: QuickCommand.Rename),
            ItemMenuEntry.Separator,
            new(Glyphs.Delete, Str.Common_Delete.Text(), () => Delete(habitId), Danger: true, Quick: QuickCommand.Delete),
        ];
    }

    // ---- the keyboard ([S-1428]) --------------------------------------------------------------------

    /// <summary>
    /// [S-809] Whether an editor is being typed into. The arrows need nothing asked: a click anywhere
    /// in the widget gives it the shell's keyboard lease (<see cref="IWidgetKeys"/>), and an editor
    /// closing keeps it. This is only the text box's share of the same lease.
    /// </summary>
    private void UpdateKeyboard() => _surface?.RequestTextInput(!_detached && _editor is not null);

    private void FocusRow(HabitRow row)
    {
        _rowFocused = true;
        _focusId = row.Id;
        _focusIndex = Math.Max(0, _rows.IndexOf(row));
        row.Host.Focus(FocusState.Keyboard);
        row.Host.StartBringIntoView();
    }

    /// <summary>After a rebuild, the focus (or the editor) goes back to its habit, or to the row now in its place.</summary>
    private void RestoreFocus(string? editing)
    {
        if (editing is not null && RowFor(editing) is { } edited && _editor is { } editor)
        {
            edited.Adopt(editor.Box);
            return;
        }

        if (!_rowFocused || _editor is not null)
        {
            return;
        }

        if (_rows.Count == 0)
        {
            ReleaseRowFocus();
            return;
        }

        FocusRow((_focusId is null ? null : RowFor(_focusId)) ?? _rows[Math.Clamp(_focusIndex, 0, _rows.Count - 1)]);
    }

    private void ReleaseRowFocus()
    {
        _rowFocused = false;
        _focusId = null;
    }

    /// <summary>
    /// [S-1428]/[S-809] A key from the shell's keyboard lease: the arrows and Home/End move the row
    /// focus, Tab / Shift+Tab wrap, Enter ticks, F2 renames, Delete removes, Escape (also the lease
    /// ending) lets go. With no row focused yet a movement key lands on an end ([S-812]). An open
    /// editor owns its own keys.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        // [S-6409] The heatmap has no rows to move between: its keys stay the shell's.
        if (_detached || IsHeatmap)
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

        var index = _rowFocused ? FocusedIndex() : -1;
        var decision = HabitLayout.Decide(key, index, _rows.Count);
        var row = decision.Index >= 0 && decision.Index < _rows.Count ? _rows[decision.Index] : null;

        switch (decision.Command)
        {
            case HabitKeyCommand.Focus when row is not null:
                FocusRow(row);
                return true;

            case HabitKeyCommand.Toggle when row is not null:
                Toggle(row.Id);
                return true;

            case HabitKeyCommand.Delete when row is not null:
                Delete(row.Id);
                return true;

            case HabitKeyCommand.MoveUp or HabitKeyCommand.MoveDown when row is not null:
                Move(row.Id, decision.Command == HabitKeyCommand.MoveUp ? -1 : +1);
                return true;

            case HabitKeyCommand.Rename when row is not null:
                BeginRename(row.Id);
                return true;

            case HabitKeyCommand.Release when _rowFocused:
                ReleaseRowFocus();
                return true;

            default:
                return false;
        }
    }

    /// <summary>The focused habit's row now, or the row in its old place when the habit has gone.</summary>
    private int FocusedIndex()
    {
        var at = _focusId is null ? -1 : _rows.FindIndex(r => r.Id == _focusId);
        return at >= 0 ? at : Math.Clamp(_focusIndex, 0, Math.Max(0, _rows.Count - 1));
    }

    private static bool ShiftDown()
        => InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

    /// <summary>Leaving the list gives the keyboard back; moving between rows or into an editor is not leaving it.</summary>
    private void OnRowLostFocus()
    {
        if (!_rowFocused || XamlRoot is null)
        {
            return;
        }

        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        if (_rows.Any(r => ReferenceEquals(r.Host, focused)) || (_editor is { } editor && ReferenceEquals(focused, editor.Box)))
        {
            return;
        }

        ReleaseRowFocus();
    }

    // ---- the editors ([S-1425]) -----------------------------------------------------------------------

    /// <summary>
    /// Rename in place: the name becomes a text box holding it. Enter or leaving it commits, Escape
    /// cancels; a blank or unchanged name commits nothing.
    /// </summary>
    private void BeginRename(string habitId)
    {
        if (!Writable || RowFor(habitId) is not { } row || HabitFor(habitId) is not { } habit)
        {
            return;
        }

        CloseEditor();

        var box = NewEditor(habit.Name);
        row.Adopt(box);
        _editor = (box, habitId);
        StartTyping(box);
    }

    /// <summary>[S-3850] The shared add footer, under the list.</summary>
    private AddFooterRow BuildAddRow() => new(Str.WidgetsB_Habits_AddHabit.Text(), OpenAddEditor);

    private void OpenAddEditor()
    {
        if (!Writable || _editor is { HabitId: null })
        {
            return;
        }

        CloseEditor();

        var box = NewEditor(string.Empty);
        box.PlaceholderText = Str.WidgetsB_Habits_NamePlaceholder.Text();
        _addRow.Open(box);
        _editor = (box, null);
        StartTyping(box);
    }

    /// <summary>
    /// [S-1427]/[S-3850] The add footer shows where the plan has room for it; while it holds an open
    /// editor it stays whatever the plan says.
    /// </summary>
    private void FitAddRow(HabitLayoutPlan plan)
        => _addRow.Fit((plan.AddRow && Writable) || _editor is { HabitId: null }, plan.CompactRows, plan.Inset);

    private TextBox NewEditor(string text)
    {
        var box = new TextBox
        {
            Text = text,
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
                CloseEditor();
                break;
        }
    }

    /// <summary>
    /// [S-1428] Tab, caught before focus navigation takes it: in a rename box it commits and opens the
    /// next habit's box, falling into the add row past the last; in the add box it commits and stays
    /// open for the next habit. Shift+Tab goes back, and from the add row commits and closes.
    /// </summary>
    private void OnEditorPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Tab || _editor is not { } editor)
        {
            return;
        }

        args.Handled = true;
        var backwards = ShiftDown();

        if (editor.HabitId is not { } editing)
        {
            CommitEdit(keepAdding: !backwards);
            return;
        }

        var index = _rows.FindIndex(r => r.Id == editing);
        var next = backwards ? index - 1 : index + 1;
        CommitEdit(keepAdding: false);

        if (_detached)
        {
            return;
        }

        if (next >= 0 && next < _rows.Count)
        {
            BeginRename(_rows[next].Id);
        }
        else if (!backwards)
        {
            OpenAddEditor();
        }
    }

    private void OnEditorLostFocus(object sender, RoutedEventArgs args) => CommitEdit(keepAdding: false);

    /// <summary>
    /// Commit what the editor holds. Enter in the add row leaves it open for the next habit, so a list
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

        if (editor.HabitId is { } habitId)
        {
            Rename(habitId, text);
            return;
        }

        Add(text);

        if (keepAdding && text.Trim().Length > 0 && !_detached)
        {
            OpenAddEditor();
        }
    }

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

        if (editor.HabitId is { } habitId && RowFor(habitId) is { } row && _plan is { } plan)
        {
            // The row's optional parts come back as the ladder has them, not all at once.
            row.SetLayout(plan);
        }

        if (_plan is { } current)
        {
            FitAddRow(current);
        }

        UpdateKeyboard();
    }

    private void DetachAddRow() => _addRow.Detach();

    /// <summary>
    /// One habit's row: a box, the name, the run, the history dots, and on a wide tile its rate and
    /// best run.
    /// <para>
    /// The box is the <b>only</b> pointer target that completes a habit ([S-1421]): a click on the
    /// name or the run never ticks anything, because a widget sits under the pointer's path across
    /// the whole desktop and has no undo. The box is drawn rather than a stock <c>CheckBox</c>, as
    /// Tasks draws its marker: the stock control's template, stripped of its 120px minimum to fit a
    /// row, clipped its own glyph to a dash in both states.
    /// </para>
    /// </summary>
    private sealed class HabitRow
    {
        private const double BoxSize = 20;

        private readonly HabitTrackerView _owner;
        private readonly Grid _grid;
        private readonly Border _box;
        private readonly FontIcon _tick;
        private readonly TextBlock _name = Fluent.Text(FluentType.Body);
        private readonly TextBlock _streak = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        private readonly StackPanel _history = new() { Orientation = Orientation.Horizontal, Spacing = HabitLayout.DotPitch - HabitLayout.DotSize };
        private readonly TextBlock _rate = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        private readonly TextBlock _best = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        private readonly List<Border> _dots = [];
        private readonly IDisposable _hover;
        private readonly IDisposable _menu;

        private readonly Brush _filled = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        // [S-837] The empty dot must read as a slot on both themes (the alt fill is all but invisible on
        // dark): a control's strong rest fill (the item layer's track, [S-6513]), not the disabled
        // one, which read as a switched-off habit.
        private readonly Brush _stroke = ThemeBrush.Of("ControlStrongStrokeColorDefaultBrush", Colors.Gray);

        private IReadOnlyList<HabitDot> _recent = [];
        private HabitPeriod _period = HabitPeriod.Daily;
        private int _streakCount;
        private bool _showStreak = true;
        private bool _done;

        public HabitRow(HabitTrackerView owner, string id)
        {
            _owner = owner;
            Id = id;

            _tick = new FontIcon
            {
                Glyph = Glyphs.Done,
                FontSize = 12,
                Foreground = ThemeBrush.Of("TextOnAccentFillColorPrimaryBrush", Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _box = new Border
            {
                Width = BoxSize,
                Height = BoxSize,
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, Fluent.Space3, 0),
                Child = _tick,
            };

            _box.Tapped += OnBoxTapped;
            _box.DoubleTapped += OnBoxDoubleTapped;

            _name.VerticalAlignment = VerticalAlignment.Center;
            _name.TextTrimming = TextTrimming.CharacterEllipsis;
            _name.TextWrapping = TextWrapping.NoWrap;
            _name.DoubleTapped += OnNameDoubleTapped;

            foreach (var text in new[] { _streak, _rate, _best })
            {
                text.VerticalAlignment = VerticalAlignment.Center;
                text.Margin = new Thickness(HabitLayout.ColumnGap, 0, 0, 0);
                text.TextAlignment = TextAlignment.Right;
                Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);
            }

            _rate.MinWidth = HabitLayout.RateWidth;
            _best.MinWidth = HabitLayout.BestWidth;

            _history.VerticalAlignment = VerticalAlignment.Center;
            _history.Margin = new Thickness(HabitLayout.ColumnGap, 0, 0, 0);

            _grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
            };

            var column = 0;
            foreach (var part in new FrameworkElement[] { _box, _name, _streak, _history, _rate, _best })
            {
                SetColumn(part, column++);
                _grid.Children.Add(part);
            }

            Host = Fluent.Row(_grid);

            // [S-1428] Focusable, so a clicked row can take the keys; the system focus rect shows which.
            Host.IsTabStop = true;
            Host.UseSystemFocusVisuals = true;
            Host.Tapped += OnRowTapped;
            WidgetItems.Mark(Host); // [S-814]
            Host.LostFocus += OnLostFocus;

            _hover = Fluent.Hover(Host);
            _menu = ItemMenu.Attach(Host, () => _owner.MenuFor(Id));

            // [S-1621] A press anywhere but the box can become a drag.
            _owner.HookDrag(Host);
        }

        public string Id { get; }

        public Border Host { get; }

        public void Update(Habit habit, IReadOnlySet<string> log, HabitBucket current, HabitCadence cadence, int historyDots, bool showStreak)
        {
            var zone = TimeZoneInfo.Local;

            _name.Text = string.IsNullOrWhiteSpace(habit.Name) ? Str.WidgetsB_Habits_Untitled.Text() : habit.Name;
            ToolTipService.SetToolTip(_name, _name.Text);

            _done = HabitRules.IsDone(log, current);
            _box.Background = _done ? _filled : Fluent.Clear();
            _box.BorderBrush = _stroke;
            _box.BorderThickness = new Thickness(_done ? 0 : 1);
            _tick.Visibility = _done ? Visibility.Visible : Visibility.Collapsed;

            var noun = HabitPeriods.CurrentNoun(cadence.Period);
            var state = (_done ? Str.WidgetsB_Habits_StateDone : Str.WidgetsB_Habits_StateNotDone).Format(("name", _name.Text), ("when", noun));
            AutomationProperties.SetName(_box, state);
            AutomationProperties.SetName(Host, state);
            ToolTipService.SetToolTip(_box, (_done ? Str.WidgetsB_Habits_MarkNotDone : Str.WidgetsB_Habits_MarkDone).Format(("when", noun)));

            _period = cadence.Period;
            _showStreak = showStreak;
            _streakCount = HabitRules.Streak(log, current, cadence, zone);

            var runWords = _streakCount == 0 ? Str.WidgetsB_Habits_NoRun.Text() : Str.WidgetsB_Habits_InARow.Format(("units", HabitPeriods.Units(_period, _streakCount)));
            AutomationProperties.SetName(_streak, runWords);
            ToolTipService.SetToolTip(_streak, runWords);

            _recent = HabitRules.Recent(log, current, cadence, zone, historyDots);
            DrawDots(cadence);

            var doneIn = HabitRules.DoneIn(log, current, cadence, zone, HabitRules.RateWindow);
            _rate.Text = LocaleFormat.Percent(Strings.Current, Math.Round(100.0 * doneIn / HabitRules.RateWindow));
            var rateWords = Str.WidgetsB_Habits_DoneOfLast.Format(("done", doneIn), ("units", HabitPeriods.Units(_period, HabitRules.RateWindow)));
            ToolTipService.SetToolTip(_rate, rateWords);
            AutomationProperties.SetName(_rate, rateWords);

            var best = HabitRules.BestStreak(log, current, cadence, zone);
            _best.Text = Str.WidgetsB_Habits_BestShort.Format(("count", best));
            var bestWords = best == 0 ? Str.WidgetsB_Habits_NoBest.Text() : Str.WidgetsB_Habits_BestRun.Format(("units", HabitPeriods.Units(_period, best)));
            ToolTipService.SetToolTip(_best, bestWords);
            AutomationProperties.SetName(_best, bestWords);
        }

        /// <summary>
        /// [S-1426] All the configured dots exist; <see cref="SetLayout"/> shows the newest as many as
        /// fit. Each carries its period in the cadence's own words, and its state, because a filled dot
        /// and an empty one are otherwise the same shape.
        /// </summary>
        private void DrawDots(HabitCadence cadence)
        {
            if (_dots.Count != _recent.Count)
            {
                _dots.Clear();
                _history.Children.Clear();

                for (var i = 0; i < _recent.Count; i++)
                {
                    var dot = new Border
                    {
                        Width = HabitLayout.DotSize,
                        Height = HabitLayout.DotSize,
                        CornerRadius = new CornerRadius(HabitLayout.DotSize / 2),
                        VerticalAlignment = VerticalAlignment.Center,
                    };

                    _dots.Add(dot);
                    _history.Children.Add(dot);
                }
            }

            for (var i = 0; i < _recent.Count; i++)
            {
                var dot = _recent[i];
                if (dot.Done)
                {
                    _dots[i].Background = _filled;
                }
                else
                {
                    ItemFill.Paint(_dots[i], ItemRole.Track);
                }


                var label = (dot.Done ? Str.WidgetsB_Habits_DotDone : Str.WidgetsB_Habits_DotNotDone).Format(("period", HabitPeriods.Describe(dot.Bucket, cadence)));
                ToolTipService.SetToolTip(_dots[i], label);
                AutomationProperties.SetName(_dots[i], label);
            }
        }

        /// <summary>[S-1427] The parts this tile has room for. The oldest dots go first; the name never yields to them.</summary>
        public void SetLayout(HabitLayoutPlan plan)
        {
            Fluent.SetDensity(Host, plan.CompactRows);
            Host.Padding = new Thickness(plan.Inset, 0, plan.Inset, 0);

            if (_grid.Children.OfType<TextBox>().Any())
            {
                // Renaming: the box spans the trailing columns until it closes.
                return;
            }

            var count = LocaleFormat.Integer(_streakCount);
            _streak.Text = _streakCount == 0 ? string.Empty : plan.StreakWords ? HabitPeriods.Units(_period, _streakCount) : count;
            _streak.Visibility = _showStreak && plan.Streak && _streakCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            var shown = Math.Min(plan.Dots, _dots.Count);
            for (var i = 0; i < _dots.Count; i++)
            {
                _dots[i].Visibility = i >= _dots.Count - shown ? Visibility.Visible : Visibility.Collapsed;
            }

            _history.Visibility = shown > 0 ? Visibility.Visible : Visibility.Collapsed;
            _rate.Visibility = plan.Rate ? Visibility.Visible : Visibility.Collapsed;
            _best.Visibility = plan.Best ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Put an editor where the name is; the trailing parts make room for it until it closes.</summary>
        public void Adopt(TextBox box)
        {
            if (box.Parent is Panel old && !ReferenceEquals(old, _grid))
            {
                old.Children.Remove(box);
            }

            SetColumn(box, 1);
            SetColumnSpan(box, 5);

            foreach (var child in _grid.Children)
            {
                if (GetColumn((FrameworkElement)child) >= 1)
                {
                    child.Visibility = Visibility.Collapsed;
                }
            }

            if (!_grid.Children.Contains(box))
            {
                _grid.Children.Add(box);
            }

            box.Visibility = Visibility.Visible;
        }

        /// <summary>Whether a press landed on the tick box, which ticks and never starts a drag.</summary>
        public bool IsInBox(DependencyObject? source)
        {
            for (var node = source; node is not null && !ReferenceEquals(node, Host); node = VisualTreeHelper.GetParent(node))
            {
                if (ReferenceEquals(node, _box))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnBoxTapped(object sender, TappedRoutedEventArgs args)
        {
            args.Handled = true;
            _owner.Toggle(Id);
        }

        /// <summary>Two quick ticks are two ticks, not a rename.</summary>
        private static void OnBoxDoubleTapped(object sender, DoubleTappedRoutedEventArgs args) => args.Handled = true;

        private void OnNameDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
        {
            args.Handled = true;
            _owner.BeginRename(Id);
        }

        /// <summary>[S-1428] A click on the row (not its box) gives the list the keyboard, focused here.</summary>
        private void OnRowTapped(object sender, TappedRoutedEventArgs args)
        {
            if (_owner._editor is null)
            {
                _owner.FocusRow(this);
            }
        }

        private void OnLostFocus(object sender, RoutedEventArgs args) => _owner.OnRowLostFocus();

        /// <summary>Safe to call twice: every handle is idempotent.</summary>
        public void Detach()
        {
            _hover.Dispose();
            _menu.Dispose();
            _box.Tapped -= OnBoxTapped;
            _box.DoubleTapped -= OnBoxDoubleTapped;
            _name.DoubleTapped -= OnNameDoubleTapped;
            Host.Tapped -= OnRowTapped;
            Host.LostFocus -= OnLostFocus;
            _owner.UnhookDrag(Host);
        }
    }
}
