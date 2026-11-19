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
/// </summary>
public sealed partial class SearchBox : Grid
{
    private const string Scope = "search";

    /// <summary>The box itself: WinUI's search box (sealed, so held rather than derived from).</summary>
    private readonly AutoSuggestBox _suggest;

    private TextBox? _box;
    private int _caret = -1;

    public SearchBox(string placeholder)
    {
        _suggest = new AutoSuggestBox
        {
            PlaceholderText = placeholder,
            MinHeight = ShellMetrics.Page.Control,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            QueryIcon = new FontIcon { Glyph = Glyphs.Search, FontSize = FamilyType.CaptionSize },
        };
        AutomationProperties.SetName(_suggest, placeholder);
        _suggest.TextChanged += (_, _) => Changed?.Invoke(_suggest.Text);
        _suggest.Loaded += (_, _) => PlaceCaret();
        _suggest.LayoutUpdated += OnLaidOut;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_suggest);
        PreviewKeyDown += OnKey;
    }

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
        _ = _suggest.Focus(FocusState.Programmatic);
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
        if (query.Visibility != Visibility.Visible || query.Content is null)
        {
            query.Content = new FontIcon { Glyph = Glyphs.Search, FontSize = FamilyType.CaptionSize };
            query.Visibility = Visibility.Visible;
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
