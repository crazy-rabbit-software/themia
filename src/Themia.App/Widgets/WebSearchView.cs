using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Core.Shell;

namespace Themia.App.Widgets;

/// <summary>
/// A box: type a query, press Enter, the default browser opens the results.
/// <para>
/// The box is one stock Windows 11 text field, so it has the theme's caret, selection colours and
/// context menu. Around it, the ladder ([S-1529]): the line saying where the query will go, a row of
/// engine chips on a wide tile, and this session's searches under it on a tall one ([S-1530]) —
/// one click puts a search back in the box, a double-click runs it again.
/// </para>
/// <para>
/// <b>Keyboard.</b> The desktop surface carries <c>WS_EX_NOACTIVATE</c>, so the view asks for the
/// keyboard lease ([S-801]) while the box has focus and gives it back when it does not. In the box,
/// Up and Down walk the recent searches like a shell's history.
/// </para>
/// <para>
/// The URL is composed by <see cref="SearchEngines"/>, which percent-encodes the query and refuses
/// a template that is not an http/https address with exactly one placeholder outside its host. It
/// is then opened by <see cref="ShellLauncher"/>, like everything else this app opens.
/// </para>
/// </summary>
internal sealed partial class WebSearchView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions
{
    private const string Scope = "web-search";

    /// <summary>
    /// [S-1530] This session's searches, shared by every Search widget and never written to disk.
    /// </summary>
    private static RecentSearches History { get; } = new();

    private readonly TextBox _input = new();
    private readonly StackPanel _chips = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1 };
    private readonly TextBlock _hint;
    private readonly StackPanel _recentNote;
    private readonly TextBlock _recentNoteText = new();
    private readonly FontIcon _recentNoteIcon;
    private readonly TextBlock _recentTitle;
    private readonly StackPanel _recent = new();
    private readonly StackPanel _top;

    private readonly Brush _hintBrush;
    private readonly Brush _errorBrush;
    private readonly List<IDisposable> _hovers = [];

    private IWidgetSurface? _surface;
    private WebSearchConfig _config = new();
    private WidgetArea _area;
    private int _recentRows;
    private bool _tallRows;
    private bool _renderedTall;
    private int? _historyAt;
    private bool _detached;

    public WebSearchView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _hintBrush = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);

        // The stock "something is wrong" fill, so a bad template reads the way every other error in
        // Windows does rather than as a red this file picked.
        _errorBrush = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.IndianRed);

        _input.PlaceholderText = Str.WidgetsC_WebSearch_Placeholder.Text();
        _input.IsSpellCheckEnabled = false;
        _input.IsTextPredictionEnabled = false;
        _input.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        _input.KeyDown += OnInputKeyDown;
        _input.TextChanged += OnInputChanged;
        _input.GotFocus += OnInputGotFocus;
        _input.LostFocus += OnInputLostFocus;

        _hint = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        _hint.TextTrimming = TextTrimming.CharacterEllipsis;
        _hint.TextWrapping = TextWrapping.NoWrap;

        // [S-1530] The space under the box before the first search: the stock empty state, centred,
        // so it reads as waiting rather than as a list that failed to load.
        _recentNoteText.Text = Str.WidgetsC_WebSearch_RecentNote.Text();
        _recentNote = Fluent.EmptyState(Glyphs.Clock, _recentNoteText, out _recentNoteIcon);
        _recentNote.Visibility = Visibility.Collapsed;

        _top = new StackPanel
        {
            Spacing = Fluent.Space2,
            Padding = new Thickness(Fluent.Space4, Fluent.Space3, Fluent.Space4, 0),
            Children = { _input, _chips, _hint },
        };

        _recentTitle = Fluent.Text(FluentType.Strong(FluentType.Caption), "TextFillColorSecondaryBrush");
        _recentTitle.Text = Str.WidgetsC_WebSearch_Recent.Text();
        _recentTitle.Margin = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, Fluent.Space1);
        _recentTitle.Visibility = Visibility.Collapsed;

        var lower = new Grid { Children = { new StackPanel { Children = { _recentTitle, _recent } }, _recentNote } };
        SetRow(_top, 0);
        SetRow(lower, 1);

        Children.Add(_top);
        Children.Add(lower);

        UpdateHint();
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // No ticker: this widget changes when it is typed in.
        History.Changed += OnHistoryChanged;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1110] The fixed texts again; the placeholder and hint follow on the next lay-out.</summary>
    private void OnLanguage(Locale locale)
    {
        _recentNoteText.Text = Str.WidgetsC_WebSearch_RecentNote.Text();
        _recentTitle.Text = Str.WidgetsC_WebSearch_Recent.Text();
        Lay();
        RenderRecent();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as WebSearchConfig ?? new WebSearchConfig();
        UpdateHint();
        Lay();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    // -----------------------------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------------------------

    private void OnInputChanged(object sender, TextChangedEventArgs args) => UpdateHint();

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs args)
    {
        switch (args.Key)
        {
            case Windows.System.VirtualKey.Enter:
                args.Handled = true;
                Submit(_input.Text);
                break;

            case Windows.System.VirtualKey.Escape when _input.Text.Length > 0:
                args.Handled = true;
                _input.Text = string.Empty;
                _historyAt = null;
                break;

            // [S-1530] Up / Down walk this session's searches, newest first, like a shell history.
            case Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down when History.Items.Count > 0:
                args.Handled = true;
                _historyAt = History.Step(_historyAt, older: args.Key == Windows.System.VirtualKey.Up);
                _input.Text = _historyAt is { } at ? History.Items[at] : string.Empty;
                _input.SelectionStart = _input.Text.Length;
                break;
        }
    }

    /// <summary>
    /// Build the URL and hand it to the shell. A blank box does nothing at all — not an error, just
    /// nothing to search for — and a refused template says so instead of opening the engine bare.
    /// </summary>
    private void Submit(string query)
    {
        if (_detached || _surface?.IsPreview == true)
        {
            return;
        }

        var built = SearchEngines.Build(_config.EffectiveEngine, _config.CustomTemplate, query);

        if (!built.IsValid)
        {
            ShowHint(SearchEngines.ProblemMessage(built.Problem), built.Problem != SearchProblem.NoQuery);
            return;
        }

        if (ShellLauncher.Open(built.Url) == LaunchOutcome.Opened)
        {
            History.Add(query);
            _historyAt = null;

            if (_config.ClearAfterSearch)
            {
                _input.Text = string.Empty;
            }
        }

        UpdateHint();
    }

    /// <summary>
    /// The line under the box: where the query is going, or what is wrong with the template. Worth
    /// showing both — a box whose engine cannot be seen is a box you have to guess at.
    /// </summary>
    private void UpdateHint()
    {
        var engine = _config.EffectiveEngine;

        if (engine == SearchEngine.Custom)
        {
            var problem = SearchEngines.CheckTemplate(_config.CustomTemplate);

            if (problem != SearchProblem.None)
            {
                ShowHint(SearchEngines.ProblemMessage(problem), isError: true);
                return;
            }
        }

        ShowHint(Str.WidgetsC_WebSearch_EnterHint.Format(("engine", SearchEngines.Label(engine))), isError: false);
    }

    private void ShowHint(string text, bool isError)
    {
        _hint.Text = text;
        _hint.Foreground = isError ? _errorBrush : _hintBrush;
        ToolTipService.SetToolTip(_hint, text);
    }

    private void OnInputGotFocus(object sender, RoutedEventArgs args) => _surface?.RequestTextInput(true);

    private void OnInputLostFocus(object sender, RoutedEventArgs args) => _surface?.RequestTextInput(false);

    // -----------------------------------------------------------------------------------------
    // The ladder
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1529] The box always, at its stock height (one step larger at XL); the engine chips from
    /// an Expanded width; the hint when a line is left; then this session's searches as whole rows
    /// of what remains — or, with none yet, a line saying where they will appear. With nothing under
    /// it, the box sits in the middle of the tile.
    /// </summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var area = _area.IsMeasured ? _area : new WidgetArea(320, 120);
        var large = WebSearchLayout.LargeBox(area);

        _input.FontSize = large ? FluentType.BodyLarge.FontSize : FluentType.Body.FontSize;
        _input.MinHeight = large ? 40 : Fluent.RowHeightCompact;

        // A narrow box keeps a placeholder it can show whole.
        _input.PlaceholderText = (area.WidthClass == SizeClass.Compact ? Str.Common_Search : Str.WidgetsC_WebSearch_Placeholder).Text();

        var room = new Disclosure(area.Deflate(Fluent.Space4, Fluent.Space2), gap: Fluent.Space2);
        room.Reserve(_input.MinHeight);

        var chips = WebSearchLayout.Chips(area) && room.Take(WebSearchLayout.ChipHeight);
        _chips.Visibility = chips ? Visibility.Visible : Visibility.Collapsed;

        if (chips)
        {
            BuildChips();
        }

        var hint = room.Take(FluentType.Caption.LineHeight);
        _hint.Visibility = hint ? Visibility.Visible : Visibility.Collapsed;

        // A large tile titles the list and gives each search a full-height row.
        var wanted = Math.Min(History.Items.Count, WebSearchLayout.RecentWanted(area));
        var title = wanted > 0 && area.HeightClass >= SizeClass.Expanded && room.Take(FluentType.Caption.LineHeight + Fluent.Space1);
        _recentTitle.Visibility = title ? Visibility.Visible : Visibility.Collapsed;
        _tallRows = large;

        var rows = area.HeightClass >= SizeClass.Regular && wanted > 0
            ? room.TakeRows(large ? Fluent.RowHeight : WebSearchLayout.RecentRow, wanted)
            : 0;

        // No searches yet: on a tile with the room, say where they will go, so the space under the
        // box reads as waiting rather than as broken.
        var note = rows == 0 && History.Items.Count == 0 && area.HeightClass >= SizeClass.Regular
            && room.Take(FluentType.Body.LineHeight * 2);
        _recentNote.Visibility = note ? Visibility.Visible : Visibility.Collapsed;

        var stacked = rows > 0 || note;
        _top.VerticalAlignment = stacked ? VerticalAlignment.Top : VerticalAlignment.Center;
        _top.Padding = new Thickness(Fluent.Space4, stacked ? Fluent.Space3 : 0, Fluent.Space4, 0);
        SetRowSpan(_top, stacked ? 1 : 2);

        if (rows != _recentRows || _recent.Children.Count != rows || large != _renderedTall)
        {
            _recentRows = rows;
            RenderRecent();
        }
    }

    /// <summary>[S-1529] One chip per engine (the custom one only when its template is usable); the one in use is checked.</summary>
    private void BuildChips()
    {
        var engines = SearchEngines.All
            .Where(engine => engine != SearchEngine.Custom || SearchEngines.CheckTemplate(_config.CustomTemplate) == SearchProblem.None)
            .ToList();

        if (_chips.Children.Count != engines.Count)
        {
            ClearChips();

            foreach (var engine in engines)
            {
                var chip = new ToggleButton
                {
                    Content = SearchEngines.Label(engine),
                    Tag = engine,
                    Height = WebSearchLayout.ChipHeight,
                    MinWidth = 0,
                    Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0),
                    FontSize = FluentType.Caption.FontSize,
                    CornerRadius = new CornerRadius(WebSearchLayout.ChipHeight / 2),
                };

                chip.Click += OnChipClick;
                _chips.Children.Add(chip);
            }
        }

        foreach (var child in _chips.Children)
        {
            if (child is ToggleButton { Tag: SearchEngine engine } chip)
            {
                chip.IsChecked = engine == _config.EffectiveEngine;
            }
        }
    }

    private void ClearChips()
    {
        foreach (var child in _chips.Children)
        {
            if (child is ToggleButton chip)
            {
                chip.Click -= OnChipClick;
            }
        }

        _chips.Children.Clear();
    }

    /// <summary>[S-1529] A chip switches this widget's engine and saves it, as the settings page would.</summary>
    private void OnChipClick(object sender, RoutedEventArgs args)
    {
        if (sender is ToggleButton { Tag: SearchEngine engine })
        {
            _config.Engine = engine;
            _surface?.RequestSave();
            UpdateHint();
            BuildChips();
        }
    }

    // -----------------------------------------------------------------------------------------
    // Recent searches
    // -----------------------------------------------------------------------------------------

    private void OnHistoryChanged()
    {
        if (!_detached)
        {
            _recentRows = -1;
            Lay();
        }
    }

    /// <summary>[S-1530] The newest searches, one row each, edge to edge like every list here.</summary>
    private void RenderRecent()
    {
        ClearRecent();
        _renderedTall = _tallRows;

        for (var i = 0; i < _recentRows && i < History.Items.Count; i++)
        {
            var query = History.Items[i];

            var icon = Fluent.Icon(Glyphs.Clock);
            icon.Margin = new Thickness(0, 0, Fluent.Space3, 0);

            var text = Fluent.Text(FluentType.Body);
            text.Text = query;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.TextWrapping = TextWrapping.NoWrap;
            SetColumn(text, 1);

            var line = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
                Children = { icon, text },
            };

            var row = Fluent.Row(line, compact: !_tallRows);
            row.Height = _tallRows ? Fluent.RowHeight : WebSearchLayout.RecentRow;
            row.Tag = query;
            row.Tapped += OnRecentTapped;
            row.DoubleTapped += OnRecentDoubleTapped;
            row.RightTapped += OnRecentRightTapped;
            ToolTipService.SetToolTip(row, Str.WidgetsC_WebSearch_RowTip.Text());

            _hovers.Add(Fluent.Hover(row));
            _recent.Children.Add(row);
        }
    }

    private void ClearRecent()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();

        foreach (var child in _recent.Children)
        {
            if (child is Border row)
            {
                row.Tapped -= OnRecentTapped;
                row.DoubleTapped -= OnRecentDoubleTapped;
                row.RightTapped -= OnRecentRightTapped;
            }
        }

        _recent.Children.Clear();
    }

    /// <summary>One click puts the search back in the box to change it; nothing opens.</summary>
    private void OnRecentTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: string query })
        {
            args.Handled = true;
            _input.Text = query;
            _input.Focus(FocusState.Programmatic);
            _input.SelectionStart = query.Length;
        }
    }

    /// <summary>A double-click runs it again, as the widget's other opens do.</summary>
    private void OnRecentDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: string query })
        {
            args.Handled = true;
            Submit(query);
        }
    }

    private void OnRecentRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (sender is Border { Tag: string query } row)
        {
            ItemMenu.Show(row, args,
            [
                new ItemMenuEntry(Glyphs.Search, Str.WidgetsC_WebSearch_SearchAgain.Text(), () => Submit(query)),
                new ItemMenuEntry(Glyphs.Copy, Str.Common_Copy.Text(), () => Copy(query), Quick: QuickCommand.Copy),
                ItemMenuEntry.Separator,
                new ItemMenuEntry(Glyphs.Delete, Str.WidgetsC_WebSearch_RemoveRecent.Text(), () => History.Remove(query), Quick: QuickCommand.Delete),
            ]);
        }
    }

    private static void Copy(string text)
    {
        if (!ClipboardWatcher.SetText(text))
        {
            Log.Warn(Scope, "could not copy a search to the clipboard");
        }
    }

    /// <summary>[S-1530] "Clear recent searches" on the card's menu, while there are any.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => History.Items.Count > 0
        ? [new WidgetAction(Glyphs.Clear, Str.WidgetsC_WebSearch_ClearRecent.Text(), () => History.Clear())]
        : [];

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;

        History.Changed -= OnHistoryChanged;
        _input.KeyDown -= OnInputKeyDown;
        _input.TextChanged -= OnInputChanged;
        _input.GotFocus -= OnInputGotFocus;
        _input.LostFocus -= OnInputLostFocus;

        ClearChips();
        ClearRecent();

        // Give the keyboard back on the way out: a widget removed while its box had focus would
        // otherwise leave the surface activatable for the rest of the session.
        _surface?.RequestTextInput(false);
        _surface = null;
    }
}
