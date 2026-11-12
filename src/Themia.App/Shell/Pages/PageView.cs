using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>
/// [S-2305] A page of the edit bar drawn as Windows' settings page, after Karpo's (karpo-app,
/// docs/widgets.md §575, §594, §670): its sections, a BodyStrong heading 24 after the last group and 8
/// over its cards; its cards 4 apart; a row 48 tall (60 with a caption), its icon at 16, its name at 48,
/// its control 16 from the card's end. A card opens one level inside it (an expander, an inline picker:
/// one at a time, scrolled into view); a page of its own (an account, a line of text asked, an "are you
/// sure?") stands in the page's place behind a back head.
/// <para>
/// The page is drawn again from its model on every change (<see cref="Show"/>): what is open, the
/// sub-page, the scroll and the keyboard's place are kept by the rows' keys, so a change made while one
/// is typing, dragging or picking leaves it where it was.
/// </para>
/// </summary>
internal sealed partial class PageView : StackPanel
{
    private const string Scope = "pages";

    private readonly Func<PageModel> _build;

    /// <summary>The sub-pages, asks and questions opened, deepest last.</summary>
    private readonly List<Step> _steps = [];

    /// <summary>The one card opened inside (an expander, a picker), by its row's key.</summary>
    private string? _open;

    /// <summary>[S-3736] Why a text typed into a box was not kept, by the box's key.</summary>
    private readonly Dictionary<string, string> _refused = new(StringComparer.Ordinal);

    /// <summary>[S-2308] What is typed in a lookup's box, by its row's key.</summary>
    private readonly Dictionary<string, string> _typed = new(StringComparer.Ordinal);

    /// <summary>[S-2308] A lookup's last answer, by its row's key, with what it was asked for.</summary>
    private readonly Dictionary<string, (string Typed, MenuSearchAnswer? Answer)> _found = new(StringComparer.Ordinal);

    private CancellationTokenSource? _finding;

    private PageModel? _model;

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _lit;

    public PageView(Func<PageModel> build)
    {
        _build = build ?? throw new ArgumentNullException(nameof(build));
        Padding = new Thickness(ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, P.Bottom);
        Unloaded += (_, _) => Cancel();
    }

    /// <summary>The page's key (its id, or the selection's).</summary>
    public string Key => _model?.Key ?? string.Empty;

    /// <summary>Asked to scroll an element into view (the opened card, a lit row).</summary>
    public event Action<FrameworkElement>? BringIntoView;

    /// <summary>Whether a sub-page, a line of text asked or a question stands in the page's place.</summary>
    public bool Deep => _steps.Count > 0;

    /// <summary>
    /// [S-3531] Draw the page as the state holds it now: the open card, the sub-page, the keyboard's
    /// place kept by the rows' keys.
    /// </summary>
    public void Show()
    {
        var focused = FocusedTag();
        try
        {
            _model = _build();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a page could not be built", ex);
            return;
        }

        Children.Clear();
        while (_steps.Count > 0 && _steps[^1] is SubStep { Key: var key } && Find(_model, key) is null)
        {
            _steps.RemoveAt(_steps.Count - 1);
        }

        try
        {
            switch (_steps.LastOrDefault())
            {
                case SubStep sub when Find(_model, sub.Key) is { } row:
                    Children.Add(BackHead(row.Text));
                    if (row.Page is { } whole)
                    {
                        // [S-11755] A member's page as a lone selection of it shows it: its header card, its sections.
                        DrawRoot(whole);
                    }
                    else
                    {
                        AddCards(row.Rows ?? [], false);
                    }

                    break;
                case AskStep ask:
                    Children.Add(BackHead(ask.Edit.Title ?? string.Empty));
                    Children.Add(AskCard(ask.Edit));
                    break;
                case QuestionStep question:
                    Children.Add(BackHead(question.Question.Question));
                    Children.Add(QuestionCard(question.Question));
                    break;
                default:
                    DrawRoot(_model);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the page '{_model.Key}' could not be drawn", ex);
        }

        if (focused is not null)
        {
            Refocus(focused.Value);
        }
    }

    /// <summary>
    /// [S-3534] Open the page at the row along <paramref name="path"/> (the rows' keys, a section's first):
    /// its sub-pages and its card opened, the row scrolled into view and lit for a moment.
    /// </summary>
    public bool Open(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _steps.Clear();
        _open = null;
        _model ??= _build();

        var rows = _model.Sections.FirstOrDefault(s => path.Count > 0 && s.Key == path[0])?.Rows;
        string? target = path.Count > 0 ? path[0] : null;
        for (var i = 1; i < path.Count && rows is not null; i++)
        {
            var row = rows.FirstOrDefault(r => r.Key == path[i]);
            if (row is null)
            {
                break;
            }

            // Every card along the way opens, the row itself too where it opens (a deep link to a choice shows its options).
            target = row.Key;
            if (row.Kind == SettingsRowKind.SubPage)
            {
                _steps.Add(new SubStep(row.Key));
                _open = null;
            }
            else if (row.Kind is SettingsRowKind.Expander or SettingsRowKind.Choice or SettingsRowKind.Lookup or SettingsRowKind.Colour or SettingsRowKind.Font or SettingsRowKind.Icon)
            {
                _open = row.Key;
            }

            rows = row.Rows;
        }

        Show();
        if (target is null)
        {
            return false;
        }

        UpdateLayout();
        if ((Tagged(this, target + "#row") ?? Tagged(this, target + "#section")) is not { } found)
        {
            return _steps.Count > 0;
        }

        Light(found);
        BringIntoView?.Invoke(Tagged(found, target + "#head") ?? found);
        return true;
    }

    /// <summary>[S-1862] Open the page at the row the card's Set up button opens it at; false where there is none.</summary>
    public bool OpenSetup()
    {
        _model ??= _build();
        foreach (var section in _model.Sections)
        {
            if (section.Rows.FirstOrDefault(r => r.SetsUp) is { } row)
            {
                return Open([section.Key, row.Key]);
            }
        }

        return false;
    }

    /// <summary>[S-3736] A line of text asked on a page of its own (Rename…).</summary>
    public void Ask(PageEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        _steps.Add(new AskStep(edit));
        Show();
        Refocus(new FocusTag("ask#box", edit.Initial.Length));
    }

    /// <summary>[S-3737] An "are you sure?" on a page of its own; only its button does it.</summary>
    public void Confirm(PageQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        _steps.Add(new QuestionStep(question));
        Show();
        Refocus(new FocusTag("question#cancel", 0));
    }

    /// <summary>[S-2327] Escape and the back head: the page it stands on, or the open card closed; false with nothing to go back from.</summary>
    public bool Back()
    {
        if (_steps.Count > 0)
        {
            var step = _steps[^1];
            _steps.RemoveAt(_steps.Count - 1);
            if (step is AskStep { Edit.Wipe: { } wipe })
            {
                wipe();
            }

            Show();
            FocusFirst();
            return true;
        }

        if (_open is not null)
        {
            var open = _open;
            _open = null;
            Show();
            Refocus(new FocusTag(open + "#head", 0));
            return true;
        }

        return false;
    }

    /// <summary>[S-3557] The page goes: what a password's box held is wiped, a search stops.</summary>
    public void Close()
    {
        foreach (var step in _steps.OfType<AskStep>())
        {
            step.Edit.Wipe?.Invoke();
        }

        _steps.Clear();
        Cancel();
    }

    /// <summary>The keyboard on the page's first control; false where it has none.</summary>
    public bool FocusFirst() => FocusManager.FindFirstFocusableElement(this) is Control first && first.Focus(FocusState.Keyboard);

    private void Cancel()
    {
        _finding?.Cancel();
        _finding?.Dispose();
        _finding = null;
    }

    /// <summary>[S-2305] The page's own level: its header card, then its sections.</summary>
    private void DrawRoot(PageModel model)
    {
        if (model.Header is { } header)
        {
            Children.Add(HeaderCard(header));
        }

        var first = model.Header is null;
        foreach (var section in model.Sections)
        {
            if (section.Heading is { Length: > 0 } heading)
            {
                var words = Fluent.Text(FamilyType.BodyStrong);
                words.Text = heading;
                words.Tag = section.Key + "#section";
                words.Margin = new Thickness(0, first ? 0 : P.SectionTop - P.CardGap, 0, P.HeadingGap);
                words.TextWrapping = TextWrapping.Wrap;
                Children.Add(words);
            }
            else if (!first)
            {
                Children.Add(new Border { Height = P.SectionTop - P.CardGap, Tag = section.Key + "#section" });
            }

            AddCards(section.Rows, false);
            first = false;
        }
    }

    /// <summary>[S-2304] Rows as cards 4 apart.</summary>
    private void AddCards(IReadOnlyList<PageRow> rows, bool inside)
    {
        foreach (var card in SettingsRows.Cards(rows, r => r.Kind))
        {
            Children.Add(Card(card, inside));
        }
    }

    /// <summary>[S-2305] A card: Windows' card fill and stroke, radius 4, its rows in it, 4 under it.</summary>
    private Border Card(IReadOnlyList<PageRow> rows, bool inside)
    {
        var stack = new StackPanel();
        for (var i = 0; i < rows.Count; i++)
        {
            stack.Children.Add(RowOf(rows[i], inside, i == 0));
        }

        return new Border
        {
            Child = stack,
            Background = ShellBrush.Of(ShellRole.CardFill),
            BorderBrush = ShellBrush.Of(ShellRole.CardStroke),
            BorderThickness = new Thickness(FamilyShape.Hairline),
            CornerRadius = new CornerRadius(P.CardRadius),
            Margin = new Thickness(0, 0, 0, P.CardGap),
            Tag = rows[0].Key + "#card",
        };
    }

    /// <summary>[S-3534] A row lit for a moment where a way into settings landed.</summary>
    private void Light(FrameworkElement found)
    {
        if (found is not Border card)
        {
            return;
        }

        var rest = card.Background;
        card.Background = ShellBrush.Of(ShellRole.ControlHover);
        _lit?.Stop();
        _lit = DispatcherQueue.CreateTimer();
        _lit.Interval = TimeSpan.FromMilliseconds(P.LitMs);
        _lit.IsRepeating = false;
        _lit.Tick += (_, _) => card.Background = rest;
        _lit.Start();
    }

    /// <summary>The row of <paramref name="model"/> at <paramref name="key"/>, at any depth.</summary>
    private static PageRow? Find(PageModel model, string key)
    {
        static PageRow? In(IReadOnlyList<PageRow> rows, string key)
        {
            foreach (var row in rows)
            {
                if (row.Key == key)
                {
                    return row;
                }

                if (row.Rows is { } inner && In(inner, key) is { } found)
                {
                    return found;
                }

                if (row.Page is { } page && page.Sections.Select(s => In(s.Rows, key)).FirstOrDefault(r => r is not null) is { } deeper)
                {
                    return deeper;
                }
            }

            return null;
        }

        return model.Sections.Select(s => In(s.Rows, key)).FirstOrDefault(r => r is not null);
    }

    /// <summary>The element under <paramref name="root"/> tagged <paramref name="tag"/>.</summary>
    private static FrameworkElement? Tagged(DependencyObject root, string tag)
    {
        if (root is FrameworkElement { Tag: string t } element && t == tag)
        {
            return element;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (Tagged(VisualTreeHelper.GetChild(root, i), tag) is { } found)
            {
                return found;
            }
        }

        return root is Border { Child: { } child } ? Tagged(child, tag) : null;
    }

    /// <summary>Where the keyboard is on the page: its control's tag and a text box's caret.</summary>
    private readonly record struct FocusTag(string Tag, int Caret);

    private FocusTag? FocusedTag()
    {
        if (XamlRoot is null || FocusManager.GetFocusedElement(XamlRoot) is not FrameworkElement { Tag: string tag } element || !IsInside(element))
        {
            return null;
        }

        return new FocusTag(tag, element is TextBox box ? box.SelectionStart : 0);
    }

    private bool IsInside(DependencyObject element)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, this))
            {
                return true;
            }
        }

        return false;
    }

    private void Refocus(FocusTag focus)
    {
        UpdateLayout();
        if (Tagged(this, focus.Tag) is Control control)
        {
            control.Focus(FocusState.Programmatic);
            if (control is TextBox box)
            {
                box.SelectionStart = Math.Min(focus.Caret, box.Text.Length);
            }
        }
    }

    /// <summary>[S-2328] A render's click on the row at <paramref name="key"/>, as a click would do it; false where there is none.</summary>
    internal bool DebugClick(string key)
    {
        _model ??= _build();
        if (Find(_model, key) is not { } row)
        {
            return false;
        }

        switch (row.Kind)
        {
            case SettingsRowKind.SubPage:
                Push(row);
                break;
            case SettingsRowKind.Expander or SettingsRowKind.Choice or SettingsRowKind.Lookup or SettingsRowKind.Colour or SettingsRowKind.Font or SettingsRowKind.Icon:
                _open = row.Key;
                Show();
                break;
            case SettingsRowKind.Toggle:
                Do(() => row.Toggle?.Invoke(!row.On));
                break;
            default:
                Do(row.Run);
                break;
        }

        return true;
    }

    /// <summary>[S-2328] A render's typing into the open lookup's box.</summary>
    internal void DebugType(string text)
    {
        if (_open is { } open)
        {
            _typed[open] = text;
            Show();
        }
    }

    /// <summary>[S-2328] A render's click on an item's tool.</summary>
    internal bool DebugTool(string key, int index)
    {
        _model ??= _build();
        if (Find(_model, key) is not { Tools: { } tools } || index >= tools.Count)
        {
            return false;
        }

        Do(tools[index].Run);
        return true;
    }

    private abstract record Step;

    private sealed record SubStep(string Key) : Step;

    private sealed record AskStep(PageEdit Edit) : Step;

    private sealed record QuestionStep(PageQuestion Question) : Step;

    /// <summary>The words a row is said by (a tooltip, a screen reader).</summary>
    private static string Said(PageRow row) => row.Value is { Length: > 0 } value ? row.Text + ", " + value : row.Text;

    /// <summary>A Caption line in the secondary colour (a row's second line, a lookup's status).</summary>
    private static TextBlock CaptionText(string text, bool critical = false)
    {
        var words = Fluent.Text(FamilyType.Caption, critical ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush");
        words.Text = text;
        words.TextWrapping = TextWrapping.Wrap;
        return words;
    }

    /// <summary>A Body line, trimmed with an ellipsis.</summary>
    private static TextBlock BodyText(string text, bool danger = false)
    {
        var words = Fluent.Text(FamilyType.Body, danger ? "SystemFillColorCriticalBrush" : "TextFillColorPrimaryBrush");
        words.Text = text;
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        words.TextWrapping = TextWrapping.NoWrap;
        words.VerticalAlignment = VerticalAlignment.Center;
        return words;
    }

    /// <summary>The words of what was found for a lookup, or that nothing was.</summary>
    private static string FoundWords(int count) => count == 0
        ? Str.Common_NoResults.Text()
        : Str.EditBar_Page_Found.Format(("count", LocaleFormat.Integer(count)));
}
