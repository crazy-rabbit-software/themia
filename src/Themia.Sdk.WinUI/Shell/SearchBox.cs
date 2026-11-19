using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.System;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-13606] The one search box: every place Themia searches as it is typed into (a settings lookup, the add
/// page, the icon and font pickers, a widget's own search, the layout editor's apps) is this. Windows 11's search
/// box, WinUI's <see cref="AutoSuggestBox"/>, as Karpo's search field is drawn (<c>MenuTextField</c>,
/// docs/widgets.md §641): 32 tall, the accent line at its foot while it has the keys, the clear button and the
/// magnifier at its end, the placeholder greyed; no spelling squiggles under a place's or a symbol's name. Its
/// keys are <see cref="SearchKeys"/>' (where it is told how many results it has, <see cref="Count"/>), its
/// results' keys <see cref="Results"/>'; its status line <see cref="Status"/>.
/// <para>A box with no <see cref="Count"/> keeps WinUI's own keys (a suggestion list, the layout editor's).</para>
/// <para>
/// [S-13606] [S-13950] It comes in two sizes (<see cref="SearchSize"/>), one component: the <b>field</b> (32, above) where
/// a search is always there, and the <b>line</b> where a widget opens a search on demand: the box stands in exactly the
/// box of the line it opens on (a tool line's label, <see cref="StandIn"/>; or a list's first row), as tall as the
/// line, the magnifier in the line's lead slot and the words in the line's type at the line's text edge, no frame,
/// fill, clear button or magnifier button round them (the accent line at its foot, inside the line, while it has the
/// keys), so opening it, typing in it and closing it move nothing (owner, 2026-11-19: "layout shifts for these kind
/// of inputs should never happen"). Its keys are the field's.
/// </para>
/// </summary>
public sealed partial class SearchBox : Grid
{
    private const string Scope = "search";

    /// <summary>The box itself: WinUI's search box (sealed, so held rather than derived from).</summary>
    private readonly AutoSuggestBox _suggest;

    /// <summary>The line's magnifier, in the line's lead slot; null for the field.</summary>
    private readonly Border? _lead;

    /// <summary>Where the line stands in for a tool line's words, once it has.</summary>
    private WordsPlace? _place;

    private TextBox? _box;
    private int _caret = -1;

    public SearchBox(string placeholder, SearchSize size = SearchSize.Field)
    {
        Size = size;
        _suggest = new AutoSuggestBox
        {
            PlaceholderText = placeholder,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (size == SearchSize.Field)
        {
            _suggest.MinHeight = ShellMetrics.Page.Control;
            _suggest.QueryIcon = new FontIcon { Glyph = Glyphs.Search, FontSize = FamilyType.CaptionSize };
        }
        else
        {
            _lead = BuildLine();
        }

        AutomationProperties.SetName(_suggest, placeholder);
        _suggest.TextChanged += (_, _) => Changed?.Invoke(_suggest.Text);
        _suggest.Loaded += OnSuggestLoaded;
        _suggest.LayoutUpdated += OnLaidOut;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_suggest);
        PreviewKeyDown += OnKey;
    }

    /// <summary>[S-13606] The field's 32 or the line's (<see cref="SearchSize"/>).</summary>
    public SearchSize Size { get; }

    private double _fill = double.NaN;

    /// <summary>
    /// [S-12834] A widget's field over its rows, in a band of whole grid cells (WR-1, WR-5: the rows start right under it,
    /// on a grid line): the box fills <paramref name="height"/> exactly (<c>LineRules.Under</c>: the family's control of 28
    /// grown to end on a grid line), its words centred in it. A page's field keeps its 32.
    /// </summary>
    public void Fill(double height)
    {
        if (Size != SearchSize.Field || !double.IsFinite(height) || height <= 0 || height.Equals(_fill))
        {
            return;
        }

        _fill = height;
        Height = height;
        VerticalAlignment = VerticalAlignment.Top;
        _suggest.MinHeight = 0;
        _suggest.VerticalAlignment = VerticalAlignment.Stretch;
        if (Inner() is { } box)
        {
            FitBox(box);
        }
    }

    /// <summary>[S-12834] The field's text box in its band: no 32 minimum, its words centred in the band's height.</summary>
    private void FitBox(TextBox box)
    {
        if (!double.IsFinite(_fill))
        {
            return;
        }

        box.MinHeight = 0;
        var spare = Math.Max(0, _fill - FamilyType.Body.LineHeight - (2 * FamilyShape.Hairline));
        var over = Math.Floor(spare / 2);
        box.Padding = box.Padding with { Top = over, Bottom = spare - over };
    }

    /// <summary>[S-13950] The line's lead slot: the magnifier is centred in it (a list row's lead, 32 with an avatar; 16 by default).</summary>
    public double LeadWidth
    {
        get => _lead?.Width ?? 0;
        set
        {
            if (_lead is not null)
            {
                _lead.Width = value;
            }
        }
    }

    /// <summary>Whether the line stands in for a tool line's words now.</summary>
    public bool Standing => _place?.Standing == true;

    /// <summary>
    /// [S-13950] The line stands in for <paramref name="words"/> (a tool line's label): their panel, place and cell, their
    /// line's height, their type; the words collapsed until <see cref="StandOut"/>.
    /// </summary>
    public void StandIn(TextBlock words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (_place is null || !ReferenceEquals(_place.Words, words))
        {
            _place?.Out();
            _place = new WordsPlace(this, words);
        }

        TypeOf(words);
        Visibility = Visibility.Visible;
        _place.In();
    }

    /// <summary>The line steps out: the words come back where they stood.</summary>
    public void StandOut() => _place?.Out();

    /// <summary>[S-13950] The line in the type and line of <paramref name="words"/>: its height theirs, its words in their size and family.</summary>
    public void TypeOf(TextBlock words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (Size != SearchSize.Line)
        {
            return;
        }

        // [S-12834] The words' whole box: their line and the band their padding gives it (a tool line's label fills its band).
        Height = InlineFit.Of(words.LineHeight > 0 ? words.LineHeight : FamilyType.LineOf(words.FontSize)).Height + words.Padding.Top + words.Padding.Bottom;
        _suggest.FontSize = words.FontSize;
        _suggest.FontFamily = words.FontFamily;
        _suggest.CharacterSpacing = words.CharacterSpacing;
    }

    /// <summary>
    /// [S-13950] The line: [the magnifier in the lead slot] 8 [the words], as tall as a Body line until it takes the line it
    /// stands in for. The platform's search box keeps its keys and its caret; what it would add round the words goes, in
    /// every state, through its own resource keys (its states set fills and the focused frame straight on its border).
    /// </summary>
    private Border BuildLine()
    {
        var lead = new Border
        {
            Width = FamilyShape.Icon,
            Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Family.Glyph(Glyphs.Search),
        };

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        SetColumn(_suggest, 1);
        Children.Add(lead);

        Height = FamilyType.Body.LineHeight;
        _suggest.MinHeight = 0;
        _suggest.MinWidth = 0;
        _suggest.VerticalAlignment = VerticalAlignment.Stretch;
        _suggest.BorderThickness = new Thickness(0);
        _suggest.CornerRadius = new CornerRadius(0);
        _suggest.FontSize = FamilyType.Body.FontSize;
        _suggest.Background = Family.Brush(ColourRole.Clear);
        _suggest.Foreground = Family.Brush(ColourRole.TextPrimary);

        var clear = Family.Brush(ColourRole.Clear);
        foreach (var key in LineFills)
        {
            _suggest.Resources[key] = clear;
        }

        var placeholder = Family.Brush(ColourRole.TextSecondary);
        foreach (var key in LinePlaceholders)
        {
            _suggest.Resources[key] = placeholder;
        }

        // The accent line at its foot while it has the keys, inside the line's height (a hairline under the words).
        _suggest.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0, 0, 0, FamilyShape.Hairline);
        _suggest.Resources["TextControlBorderBrushFocused"] = Family.Brush(ColourRole.Accent);

        // Its caret scrolls the words inside it, never what holds the line.
        BringIntoViewRequested += (_, args) => args.Handled = true;
        return lead;
    }

    /// <summary>The fills the line never draws: at rest, under the pointer, focused, disabled.</summary>
    private static readonly string[] LineFills =
    [
        "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled",
    ];

    /// <summary>The placeholder in every state: the line's secondary words.</summary>
    private static readonly string[] LinePlaceholders =
    [
        "TextControlPlaceholderForeground", "TextControlPlaceholderForegroundPointerOver", "TextControlPlaceholderForegroundFocused",
    ];

    /// <summary>Every change of the words, typed or set.</summary>
    public event Action<string>? Changed;

    /// <summary>The words in the box.</summary>
    public string Text
    {
        get => _suggest.Text;
        set => _suggest.Text = value;
    }

    /// <summary>What the box says while it is empty (and what a screen reader calls it); a language changed says it anew.</summary>
    public string Placeholder
    {
        get => _suggest.PlaceholderText;
        set
        {
            _suggest.PlaceholderText = value;
            AutomationProperties.SetName(_suggest, value);
        }
    }

    /// <summary>What a screen reader calls the box (its placeholder unless told otherwise).</summary>
    public string Said
    {
        set => AutomationProperties.SetName(_suggest, value);
    }

    /// <summary>WinUI's box, for a search that lists suggestions of its own (the layout editor's apps).</summary>
    public AutoSuggestBox Suggest => _suggest;

    /// <summary>How many results stand under the box now; null: WinUI's own keys (a suggestion list).</summary>
    public Func<int>? Count { get; set; }

    /// <summary>Enter: take the best result (the first).</summary>
    public Action? PickBest { get; set; }

    /// <summary>Down: the keyboard to the first result.</summary>
    public Action? ToFirst { get; set; }

    /// <summary>Escape with the box empty: let go (a widget's box folds away); null leaves it to what holds the box (a page's picker closes).</summary>
    public Action? LetGo { get; set; }

    /// <summary>The key the box (and the text box in it, which has the keyboard) is found by on a page drawn again, so the caret comes back.</summary>
    public string? Key
    {
        get => Tag as string;
        set
        {
            Tag = value;
            if (_box is not null)
            {
                _box.Tag = value;
            }
        }
    }

    /// <summary>The keyboard in the box, the caret at <paramref name="caret"/> (its end where it is past it).</summary>
    public void FocusAt(int caret)
    {
        _caret = caret;

        // A line just opened in its words' place is in the tree but not loaded yet: it takes the keys once it is.
        _keysWanted = !_suggest.Focus(FocusState.Programmatic);
        PlaceCaret();
    }

    /// <summary>The keys were asked for before the box could take them.</summary>
    private bool _keysWanted;

    private void OnSuggestLoaded(object sender, RoutedEventArgs args)
    {
        if (_keysWanted)
        {
            _keysWanted = false;
            _ = _suggest.Focus(FocusState.Programmatic);
        }

        PlaceCaret();
    }

    /// <summary>Whether the keyboard is in the box (the text box inside it has it).</summary>
    public bool HasKeys => (Inner()?.FocusState ?? _suggest.FocusState) != FocusState.Unfocused;

    /// <summary>The keyboard back in the box, at the end of its words.</summary>
    public void FocusEnd() => FocusAt(int.MaxValue);

    /// <summary>Escape with words: the box emptied, the keyboard in it.</summary>
    public void Clear()
    {
        Text = string.Empty;
        FocusEnd();
    }

    /// <summary>A letter typed on a result goes into the box, and so does the keyboard.</summary>
    public void Type(char typed)
    {
        Text += typed;
        FocusEnd();
    }

    /// <summary>Backspace on a result takes the last letter, and the keyboard goes back to the box.</summary>
    public void Backspace()
    {
        if (Text.Length > 0)
        {
            Text = Text[..^1];
        }

        FocusEnd();
    }

    /// <summary>The text box inside WinUI's box, once its template stands: the keys' tag, no spelling marks.</summary>
    private TextBox? Inner()
    {
        if (_box is not null)
        {
            return _box;
        }

        if (!_suggest.ApplyTemplate() && VisualTreeHelper.GetChildrenCount(_suggest) == 0)
        {
            return null;
        }

        _box = Find(_suggest);
        if (_box is null)
        {
            Log.Warn(Scope, "the search box's template has no text box");
            return null;
        }

        _box.Tag = Tag;
        _box.IsSpellCheckEnabled = false;
        _box.IsTextPredictionEnabled = false;
        return _box;
    }

    private static TextBox? Find(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if ((child as TextBox ?? Find(child)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// The magnifier, once the box's template stands. WinUI finds its query button when the text box inside loads; a box
    /// that loads collapsed (a widget's, shown on demand) has no template yet then, and its magnifier never comes. So
    /// the first layout that has the button shows it, with the magnifier, itself.
    /// </summary>
    private void OnLaidOut(object? sender, object e)
    {
        if (Inner() is not { } box || Named(box, "QueryButton") is not Button query)
        {
            return;
        }

        _suggest.LayoutUpdated -= OnLaidOut;
        if (Size == SearchSize.Line)
        {
            Compact(box, query);
            return;
        }

        if (query.Visibility != Visibility.Visible || query.Content is null)
        {
            query.Content = new FontIcon { Glyph = Glyphs.Search, FontSize = FamilyType.CaptionSize };
            query.Visibility = Visibility.Visible;
        }

        FitBox(box);
    }

    /// <summary>
    /// [S-13950] The line's text box: no 32 minimum, no padding (the words at the line's text edge), and neither the clear
    /// button nor the magnifier button (each 32 wide and 28 or more tall; the magnifier is the lead's, Escape empties it).
    /// </summary>
    private static void Compact(TextBox box, Button query)
    {
        box.MinHeight = 0;
        box.MinWidth = 0;
        box.Padding = new Thickness(0);
        foreach (var button in new[] { query, Named(box, "DeleteButton") })
        {
            if (button is not null)
            {
                button.MaxWidth = 0;
                button.MaxHeight = 0;
                button.IsHitTestVisible = false;
            }
        }
    }

    private static FrameworkElement? Named(DependencyObject root, string name)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if ((child is FrameworkElement { Name: var found } element && found == name ? element : Named(child, name)) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }

    private void PlaceCaret()
    {
        if (Inner() is { } box && _caret >= 0)
        {
            box.SelectionStart = Math.Min(_caret, box.Text.Length);
            _caret = -1;
        }
    }
    private void OnKey(object sender, KeyRoutedEventArgs e)
    {
        if (Count is not { } count || KeyOf(e.Key) is not { } key)
        {
            return;
        }

        try
        {
            var move = SearchKeys.InBox(key, Text.Length > 0, count());
            e.Handled = Do(move);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the search box's key {e.Key} failed", ex);
        }
    }

    /// <summary>What a move does here; true where the key was the search's.</summary>
    private bool Do(SearchMove move)
    {
        Action? act = move switch
        {
            SearchMove.Clear => Clear,
            SearchMove.PickBest => PickBest,
            SearchMove.ToFirst => ToFirst,
            SearchMove.ToBox => FocusEnd,
            SearchMove.LetGo => LetGo,
            _ => null,
        };

        act?.Invoke();
        return act is not null;
    }

    /// <summary>
    /// [S-13607] The keys on a box's results: Up on the first goes back to the box, Escape empties it (an empty box's
    /// Escape lets go), Backspace and a letter typed go into the box. <paramref name="indexOf"/> says which result (0
    /// the first; for a grid, the row) a key came from, null for none.
    /// </summary>
    public void Results(UIElement list, Func<object, int?> indexOf)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(indexOf);
        list.PreviewKeyDown += (_, e) =>
        {
            try
            {
                if (e.Key == VirtualKey.Back)
                {
                    Backspace();
                    e.Handled = true;
                    return;
                }

                if (KeyOf(e.Key) is not { } key || indexOf(e.OriginalSource) is not { } index)
                {
                    return;
                }

                e.Handled = Do(SearchKeys.OnResult(key, index, Text.Length > 0));
            }
            catch (Exception ex)
            {
                Log.Error(Scope, $"a result's key {e.Key} failed", ex);
            }
        };
        list.CharacterReceived += (_, e) =>
        {
            // Space takes a result, as Enter does; every other printable letter is the search's.
            if (char.IsControl(e.Character) || e.Character == ' ')
            {
                return;
            }

            Type(e.Character);
            e.Handled = true;
        };
    }

    /// <summary>
    /// [S-13608] The one status line under a search's results (<see cref="LookupStatus"/>: searching, the answer's
    /// word, how many were found, none): Caption in the secondary text colour, wrapped.
    /// </summary>
    public static TextBlock Status(string text)
    {
        var words = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
        words.Text = text;
        words.TextWrapping = TextWrapping.Wrap;
        words.TextTrimming = TextTrimming.None;
        return words;
    }

    /// <summary>The search's keys among the platform's (the arrows, Enter, Escape).</summary>
    private static WidgetKey? KeyOf(VirtualKey key) => key switch
    {
        VirtualKey.Enter => WidgetKey.Enter,
        VirtualKey.Escape => WidgetKey.Escape,
        VirtualKey.Up => WidgetKey.Up,
        VirtualKey.Down => WidgetKey.Down,
        _ => null,
    };
}

/// <summary>[S-13606] The one search box's two sizes.</summary>
public enum SearchSize
{
    /// <summary>The 32 field, where a search is always there (a page's lookup, a widget's Search box setting).</summary>
    Field,

    /// <summary>[S-13950] The line a widget opens a search on: as tall as the line it stands in, nothing round its words.</summary>
    Line,
}
