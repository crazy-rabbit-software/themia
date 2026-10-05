using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// An arithmetic scratchpad: a history of what has been worked out, a line to type the next one on,
/// the answer shown while it is still being typed, and — where the tile has room — a keypad.
/// <para>
/// The arithmetic is <see cref="ExpressionEvaluator"/>, the keypad's editing
/// <see cref="CalculatorKeypad"/> and what shows at which size <see cref="CalculatorLayout"/>, all in
/// Themia.Core where they are tested; this file is the surface only. [S-07]
/// </para>
/// <para>
/// Typing goes through the shell's keyboard lease: the text box asks for the keyboard when it gets
/// focus ([S-801]) and owns every key while it has it. The keypad needs no keyboard at all — its keys
/// edit the line without taking focus, so a sum can be worked with the mouse while the app you were
/// in keeps the keyboard. [S-1510]
/// </para>
/// </summary>
internal sealed partial class CalculatorView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware
{
    /// <summary>The live result's width budget, in characters: worst case, so the size never hops per keystroke.</summary>
    private const double ResultCharacters = 12;

    private readonly StackPanel _rows = new() { Spacing = 0 };
    private readonly ScrollViewer _historyScroll;
    private readonly StackPanel _empty;
    private readonly FontIcon _emptyIcon;
    private readonly TextBlock _emptyText = new() { Text = Str.WidgetsA_Calculator_Empty.Text() };
    private readonly Border _previewBox;
    private readonly TextBlock _preview;
    private readonly TextBox _input = new();
    private readonly Grid _keypad = new();

    /// <summary>The surface this widget is drawn on: where it asks for the keyboard.</summary>
    private IWidgetSurface? _surface;
    private readonly Border _entryArea;
    private readonly CalculatorHistory _history = new();

    /// <summary>One per row. A handler left hooked re-lights a row that is no longer on screen.</summary>
    private readonly List<IDisposable> _hovers = [];

    /// <summary>The keys on the pad now, so their handlers come off when the pad is rebuilt.</summary>
    private readonly List<Button> _keys = [];

    private readonly Brush _previewBrush;
    private readonly Brush _errorBrush;
    private readonly Brush _answerBrush;

    private CalculatorConfig _config = new();
    private WidgetArea _area;

    /// <summary>The pad's row count as built (0 = none yet), so a resize only rebuilds when it changes.</summary>
    private int _keypadBuilt;

    /// <summary>[S-1513] The history row recalled with Up/Down, or -1 for the line being typed.</summary>
    private int _recall = -1;

    /// <summary>What was being typed before the first Up, put back by stepping Down past the newest row.</summary>
    private string _draft = string.Empty;

    private bool _detached;

    public CalculatorView()
    {
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

        _previewBrush = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
        _answerBrush = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);

        // The stock "something is wrong" fill, so the error tracks the theme rather than being a
        // red this file decided on.
        _errorBrush = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.IndianRed);

        // Rows reach both edges of the card, so the list scrolls with no inset of its own.
        _rows.Margin = WidgetLayout.ListInset;

        _historyScroll = new ScrollViewer
        {
            Content = _rows,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        // [S-03]/[S-04] The running answer, in the secondary colour. Body at its smallest; a tile with
        // height to spare gives it to this line, as Windows Calculator's display is its largest text.
        _preview = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        _preview.HorizontalAlignment = HorizontalAlignment.Right;
        _preview.VerticalAlignment = VerticalAlignment.Bottom;
        _preview.TextTrimming = TextTrimming.CharacterEllipsis;
        _preview.Visibility = Visibility.Collapsed; // [S-04] nothing typed, nothing to show

        // The box keeps the line's height whether or not there is an answer to show, so typing the
        // first digit never shifts the history above it.
        _previewBox = new Border { Child = _preview };

        // A stock Windows 11 text box, styled by the theme rather than by this file.
        _input.PlaceholderText = "2 + 3 * 4";
        _input.IsSpellCheckEnabled = false;
        _input.IsTextPredictionEnabled = false;
        _input.TextAlignment = TextAlignment.Right;
        _input.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        _input.Height = CalculatorLayout.InputHeight;
        _input.TextChanged += OnInputChanged;
        _input.KeyDown += OnInputKeyDown;
        _input.PreviewKeyDown += OnInputPreviewKeyDown;
        _input.GotFocus += OnInputGotFocus;
        _input.LostFocus += OnInputLostFocus;

        // The line being typed is separated from the ones already worked out. This is the widget's
        // one rule, and it earns it the way a dialog's command area does: it divides a scrolling
        // region from a fixed one. The history rows themselves have no dividers at all.
        _entryArea = new Border
        {
            BorderBrush = FluentLayout.Themed("DividerStrokeColorDefaultBrush", Colors.Gray),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Spacing = CalculatorLayout.EntryGap, Children = { _previewBox, _input } },
        };

        // A tall tile with nothing worked out yet was a large blank area above the input, which
        // reads as something failing to load. Windows Calculator says so in words instead.
        _empty = Fluent.EmptyState(Glyphs.Calculator, _emptyText, out _emptyIcon);
        _empty.Visibility = Visibility.Collapsed;

        _keypad.ColumnSpacing = CalculatorLayout.KeyGap;
        _keypad.RowSpacing = CalculatorLayout.KeyGap;
        _keypad.Visibility = Visibility.Collapsed;

        for (var column = 0; column < CalculatorKeypad.Columns; column++)
        {
            _keypad.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        Children.Add(_historyScroll);
        Children.Add(_empty);
        Children.Add(_entryArea);
        Children.Add(_keypad);

        Lay();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // No ticker: this widget changes when it is typed in.

        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as CalculatorConfig ?? new CalculatorConfig();

        // The decimal places changed under the rows that are already on screen, so they are redrawn
        // from the history rather than left showing the old precision. [S-05]
        UpdatePreview();
        RedrawHistory();
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    // -----------------------------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------------------------

    private void OnInputChanged(object sender, TextChangedEventArgs args) => UpdatePreview();

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        args.Handled = true;
        Commit();
    }

    /// <summary>
    /// [S-1513] Up / Down recall history rows into the line; Escape clears a line that has something
    /// on it (and keeps the keyboard), else goes on and ends the lease. In the tunnelling pass: a
    /// single-line text box takes Up and Down itself (caret to the start / end) and marks them handled.
    /// </summary>
    private void OnInputPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        switch (args.Key)
        {
            case Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down:
                args.Handled = Recall(older: args.Key == Windows.System.VirtualKey.Up);
                break;

            case Windows.System.VirtualKey.Escape when _input.Text.Length > 0:
                args.Handled = true;
                _input.Text = string.Empty;
                break;
        }
    }

    private bool Recall(bool older)
    {
        if (_history.Count == 0)
        {
            return false;
        }

        if (_recall < 0)
        {
            _draft = _input.Text;
        }

        var next = CalculatorRecall.Step(_recall, older, _history.Count);

        if (next == _recall)
        {
            return true;
        }

        _recall = next;
        var text = next < 0 ? _draft : _history.Entries[next].Expression;

        _input.Text = text;
        _input.SelectionStart = text.Length;
        MarkRecalled();
        return true;
    }

    private void MarkRecalled()
    {
        for (var i = 0; i < _rows.Children.Count; i++)
        {
            if (_rows.Children[i] is Border row)
            {
                RowFocus.Mark(row, i == _recall);
            }
        }
    }

    /// <summary>[S-03]/[S-04] The answer, live, above the line being typed.</summary>
    private void UpdatePreview()
    {
        var result = ExpressionEvaluator.Evaluate(_input.Text);

        switch (result.Status)
        {
            // [S-1517] Nothing typed: the display shows the last answer, quieter, as Windows
            // Calculator's does after = (it is what an operator or Ans continues from). With no
            // answer yet there is nothing to show. [S-04]
            case ExpressionStatus.Empty when _history.Count > 0:
                _preview.Visibility = Visibility.Visible;
                _preview.Foreground = _answerBrush;
                _preview.Text = "= " + _history.Entries[0].Result;
                break;

            case ExpressionStatus.Empty:
                _preview.Visibility = Visibility.Collapsed;
                _preview.Text = string.Empty;
                break;

            case ExpressionStatus.Error:
                _preview.Visibility = Visibility.Visible;
                _preview.Foreground = _errorBrush;
                _preview.Text = Str.WidgetsA_Calculator_Error.Text();
                break;

            default:
                _preview.Visibility = Visibility.Visible;
                _preview.Foreground = _previewBrush;
                _preview.Text = "= " + result.Format(_config.EffectiveDecimalPlaces);
                break;
        }
    }

    /// <summary>[S-10] Enter (or =) files the calculation and clears the line, unless there is nothing to file.</summary>
    private void Commit()
    {
        var expression = _input.Text;
        var result = ExpressionEvaluator.Evaluate(expression);

        if (!result.IsNumber)
        {
            return;
        }

        if (!_history.Add(expression.Trim(), result.Format(_config.EffectiveDecimalPlaces)))
        {
            return;
        }

        _recall = -1;
        _input.Text = string.Empty;
        RedrawHistory();
        UpdatePreview();
    }

    // -----------------------------------------------------------------------------------------
    // Keypad
    // -----------------------------------------------------------------------------------------

    /// <summary>Build the pad for this many rows, once per change of row count.</summary>
    private void BuildKeypad(int rows)
    {
        if (rows == _keypadBuilt)
        {
            return;
        }

        ClearKeypad();
        _keypadBuilt = rows;

        var layout = CalculatorKeypad.Rows(extended: rows > CalculatorKeypad.Standard.Count);

        for (var r = 0; r < layout.Count; r++)
        {
            _keypad.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CalculatorLayout.KeyMin) });

            for (var c = 0; c < layout[r].Count; c++)
            {
                var key = layout[r][c];
                var button = BuildKey(key);
                SetRow(button, r);
                SetColumn(button, c);
                _keypad.Children.Add(button);
                _keys.Add(button);
            }
        }
    }

    /// <summary>
    /// One key: a stock Windows button, Windows Calculator's way round — digits on the standard
    /// control fill, operators and functions on the quieter secondary fill, equals in the accent.
    /// Never focusable, so pressing one leaves the keyboard (and the caret) where they were.
    /// </summary>
    private Button BuildKey(KeypadKey key)
    {
        var label = new TextBlock
        {
            Text = CalculatorKeypad.Label(key),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var button = new Button
        {
            Content = label,
            Tag = key,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0),
            IsTabStop = false,
            AllowFocusOnInteraction = false,
        };

        if (key == KeypadKey.Equals)
        {
            FluentLayout.ApplyStyle(button, "AccentButtonStyle");
        }
        else if (CalculatorKeypad.IsOperator(key) || key is KeypadKey.Clear or KeypadKey.Backspace or KeypadKey.OpenParen
                     or KeypadKey.CloseParen or KeypadKey.Answer)
        {
            button.Background = FluentLayout.Themed("ControlFillColorSecondaryBrush", Colors.Gray);
        }

        NameKey(button, key);
        button.Click += OnKeyClick;
        return button;
    }

    private static void NameKey(Button button, KeypadKey key)
    {
        var name = CalculatorKeypad.Name(key, Strings.Current);
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
    }

    /// <summary>[S-1109] The empty text, the error and the keys' names follow the language.</summary>
    private void OnLanguage(Locale locale)
    {
        _emptyText.Text = Str.WidgetsA_Calculator_Empty.Text();
        foreach (var child in _keypad.Children)
        {
            if (child is Button { Tag: KeypadKey key } button)
            {
                NameKey(button, key);
            }
        }

        UpdatePreview();
    }

    private void OnKeyClick(object sender, RoutedEventArgs args)
    {
        if (_detached || sender is not Button { Tag: KeypadKey key })
        {
            return;
        }

        var last = _history.Count > 0 ? _history.Entries[0].Result : null;
        var edit = CalculatorKeypad.Apply(key, _input.Text, _input.SelectionStart, _input.SelectionLength, last);

        if (edit.Commit)
        {
            Commit();
            return;
        }

        _input.Text = edit.Text;
        _input.SelectionStart = Math.Clamp(edit.Caret, 0, _input.Text.Length);
    }

    /// <param name="padWidth">The pad's width, from the plan (the pad itself may not be measured yet).</param>
    private void SizeKeys(double keyHeight, double padWidth)
    {
        foreach (var row in _keypad.RowDefinitions)
        {
            row.Height = new GridLength(keyHeight);
        }

        // Key labels grow with the key, as Windows Calculator's do; three characters ("Ans") must fit.
        var width = (padWidth - ((CalculatorKeypad.Columns - 1) * CalculatorLayout.KeyGap)) / CalculatorKeypad.Columns;
        var step = FluentType.Fit(Math.Max(width, CalculatorLayout.KeyMin) - Fluent.Space2, keyHeight - Fluent.Space2, 3, FluentType.Body, FluentType.Subtitle);

        foreach (var key in _keys)
        {
            if (key.Content is TextBlock label)
            {
                label.Apply(step);
            }
        }
    }

    private void ClearKeypad()
    {
        foreach (var key in _keys)
        {
            key.Click -= OnKeyClick;
        }

        _keys.Clear();
        _keypad.Children.Clear();
        _keypad.RowDefinitions.Clear();
        _keypadBuilt = 0;
    }

    // -----------------------------------------------------------------------------------------
    // History
    // -----------------------------------------------------------------------------------------

    private void RedrawHistory()
    {
        ClearRows();

        // At most eight rows ([S-09]), so redrawing the lot is cheaper than tracking which changed.
        foreach (var entry in _history.Entries)
        {
            _rows.Children.Add(BuildRow(entry));
        }

        MarkRecalled();

        // Lay owns what is shown at this size, so there is one place that decides how the widget
        // looks at every size.
        Lay();
    }

    /// <summary>Drop the rows and the pointer handlers that belong to them. Safe to call twice.</summary>
    private void ClearRows()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();

        foreach (var child in _rows.Children)
        {
            if (child is Border row)
            {
                row.Tapped -= OnRowTapped;
            }
        }

        _rows.Children.Clear();
    }

    /// <summary>
    /// One history row, in the standard Windows 11 list shape: a fixed 40px height, the content
    /// inset carried as the row's own padding so the row — and the fill it takes under the pointer —
    /// reaches both edges of the card, and no divider.
    /// </summary>
    private FrameworkElement BuildRow(Calculation entry)
    {
        var expression = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        expression.Text = entry.Expression;
        expression.TextTrimming = TextTrimming.CharacterEllipsis;

        var result = Fluent.Text(FluentType.BodyStrong);
        result.Text = "= " + entry.Result;
        result.Margin = new Thickness(Fluent.Space2, 0, 0, 0);

        Grid.SetColumn(expression, 0);
        Grid.SetColumn(result, 1);

        var layout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children = { expression, result },
        };

        var row = Fluent.Row(layout);
        row.Height = CalculatorLayout.Row;
        row.Tag = entry.Result;
        RowFocus.Prepare(row);

        _hovers.Add(Fluent.Hover(row));

        // [S-11] A past answer is usually the next thing you want to work from.
        row.Tapped += OnRowTapped;

        ToolTipService.SetToolTip(row, $"{entry.Expression} = {entry.Result}");
        return row;
    }

    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_detached || sender is not Border { Tag: string result })
        {
            return;
        }

        _input.Text = result;
        _input.SelectionStart = result.Length;
        _input.Focus(FocusState.Programmatic);
        args.Handled = true;
    }

    // -----------------------------------------------------------------------------------------
    // Layout
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1514] Place what <see cref="CalculatorLayout.Plan"/> says this size shows: the line always,
    /// then history rows, the keypad under or beside, and the leftover to the result.
    /// </summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var plan = CalculatorLayout.Plan(_area, _history.Count, _config.ShowKeypad, FluentType.Body.LineHeight);
        var inset = CalculatorLayout.Inset;
        var showHistory = plan.HistoryRows > 0;
        var showEmpty = plan.HistoryRegion && _history.Count == 0;

        _historyScroll.Visibility = showHistory ? Visibility.Visible : Visibility.Collapsed;
        _empty.Visibility = showEmpty ? Visibility.Visible : Visibility.Collapsed;
        _emptyIcon.Visibility = _area.HeightClass >= SizeClass.Regular
            && (plan.KeypadRows == 0 || plan.Arrangement == CalculatorArrangement.SideKeypad)
            ? Visibility.Visible
            : Visibility.Collapsed;

        // The result line: exactly its budget, so the history over it gets exactly its whole rows.
        _previewBox.Height = plan.ResultLine;
        var width = (plan.LeftWidth > 0 ? plan.LeftWidth : _area.Width) - (2 * inset);
        _preview.Apply(FluentType.Fit(Math.Max(width, 1), plan.ResultLine, ResultCharacters, FluentType.Body, FluentType.Display));

        var keypad = plan.KeypadRows > 0;
        _keypad.Visibility = keypad ? Visibility.Visible : Visibility.Collapsed;

        if (keypad)
        {
            BuildKeypad(plan.KeypadRows);
        }

        var regionAbove = showHistory || showEmpty;

        switch (plan.Arrangement)
        {
            case CalculatorArrangement.Strip:
                ColumnDefinitions[0].Width = new GridLength(plan.LeftWidth);
                ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

                Place(_historyScroll, row: 0, column: 1, rowSpan: 3);
                Place(_empty, row: 0, column: 1, rowSpan: 3);
                Place(_entryArea, row: 0, column: 0, rowSpan: 3);

                // Beside the history, not under it: no rule, and the line sits at the bottom.
                _entryArea.VerticalAlignment = VerticalAlignment.Bottom;
                _entryArea.BorderThickness = new Thickness(0);
                _entryArea.Padding = new Thickness(inset);
                break;

            case CalculatorArrangement.SideKeypad:
                ColumnDefinitions[0].Width = new GridLength(plan.LeftWidth);
                ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

                Place(_historyScroll, row: 0, column: 0, rowSpan: 1);
                Place(_empty, row: 0, column: 0, rowSpan: 1);
                Place(_entryArea, row: 1, column: 0, rowSpan: 1);
                Place(_keypad, row: 0, column: 1, rowSpan: 3);

                _entryArea.VerticalAlignment = VerticalAlignment.Stretch;
                _entryArea.BorderThickness = new Thickness(0, regionAbove ? Fluent.Hairline : 0, 0, 0);
                _entryArea.Padding = new Thickness(inset, regionAbove ? Fluent.Space3 : inset, inset, inset);
                _keypad.Margin = new Thickness(0, inset, inset, inset);
                _keypad.VerticalAlignment = VerticalAlignment.Top;
                break;

            default:
                ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                ColumnDefinitions[1].Width = new GridLength(0);

                Place(_historyScroll, row: 0, column: 0, rowSpan: 1);
                Place(_empty, row: 0, column: 0, rowSpan: 1);
                Place(_entryArea, row: 1, column: 0, rowSpan: 1);
                Place(_keypad, row: 2, column: 0, rowSpan: 1);

                // With nothing above it the rule would be a line across the top of the widget.
                _entryArea.VerticalAlignment = VerticalAlignment.Stretch;
                _entryArea.BorderThickness = new Thickness(0, regionAbove ? Fluent.Hairline : 0, 0, 0);
                _entryArea.Padding = new Thickness(inset, regionAbove ? Fluent.Space3 : inset, inset, inset);
                _keypad.Margin = new Thickness(inset, 0, inset, inset);
                _keypad.VerticalAlignment = VerticalAlignment.Top;
                break;
        }

        // Without a region above, the line takes the whole height and sits at its bottom.
        RowDefinitions[0].Height = regionAbove || plan.Arrangement == CalculatorArrangement.Strip
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);

        if (!regionAbove && plan.Arrangement != CalculatorArrangement.Strip)
        {
            Place(_entryArea, row: 0, column: 0, rowSpan: 2);
            _entryArea.VerticalAlignment = VerticalAlignment.Bottom;
            RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
        }

        if (keypad)
        {
            var padWidth = plan.Arrangement == CalculatorArrangement.SideKeypad
                ? _area.Width - plan.LeftWidth - inset
                : _area.Width - (2 * inset);
            SizeKeys(plan.KeyHeight, padWidth);
        }
    }

    private static void Place(FrameworkElement element, int row, int column, int rowSpan)
    {
        SetRow(element, row);
        SetColumn(element, column);
        SetRowSpan(element, rowSpan);
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    /// <summary>
    /// Ask the surface for the keyboard. Outside edit mode it is unfocusable, so without this the
    /// caret blinks in the box while the digits go to whatever window actually has focus.
    /// </summary>
    private void OnInputGotFocus(object sender, RoutedEventArgs args) => _surface?.RequestTextInput(true);

    private void OnInputLostFocus(object sender, RoutedEventArgs args) => _surface?.RequestTextInput(false);

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;

        _input.TextChanged -= OnInputChanged;
        _input.KeyDown -= OnInputKeyDown;
        _input.PreviewKeyDown -= OnInputPreviewKeyDown;
        _input.GotFocus -= OnInputGotFocus;
        _input.LostFocus -= OnInputLostFocus;
        _surface?.RequestTextInput(false);

        ClearKeypad();

        // [S-09] In-memory only: there is nothing to write out, and nothing is left behind.
        _history.Clear();
        ClearRows();
    }
}
