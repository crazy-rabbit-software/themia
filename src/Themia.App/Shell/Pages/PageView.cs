using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>
/// [S-2305] A page of the edit bar drawn as Windows' settings page, after Karpo's (karpo-app,
/// docs/widgets.md §575, §594, §670): its sections, a BodyStrong heading 24 after the last group and 8
/// over its cards; its cards 4 apart; a row 48 tall (60 with a caption), its icon at 16, its name at 48,
/// its control 16 from the card's end. A card opens one level inside it (an expander, an inline picker,
/// [S-2351] a line of text or a question asked in place: one at a time, scrolled into view); a page of its own
/// (a view's list of desktop icons: many lines, never one input) stands in the page's place behind a back head.
/// <para>
/// The page is drawn again from its model on every change (<see cref="Show"/>): what is open, the
/// sub-page, the scroll and the keyboard's place are kept by the rows' keys, so a change made while one
/// is typing, dragging or picking leaves it where it was. [S-2361] A change of the page's own (what is
/// open, unfolded, typed or shown: <see cref="Redraw"/>) draws the same rows again after the event that
/// asked, in the panel that stands: every card that would be drawn the same stays where it is, untouched
/// (<see cref="KeptCards"/>), and only the cards it changed are built and put in.
/// </para>
/// <para>
/// [S-2346] Never the panel changed inside an event: a drawing of new rows is a new panel put in the old one's place,
/// and a drawing of the same rows is posted ([S-2361]). A drawing inside a row's own pointer event (a click, a
/// release) that emptied the panel took that row out of the panel XAML was still handing the event through, and XAML
/// threw out of the panel's <c>Clear</c>.
/// </para>
/// </summary>
internal sealed partial class PageView : ContentControl
{
    private const string Scope = "pages";

    private readonly Func<PageModel> _build;

    /// <summary>The sub-pages opened, deepest last.</summary>
    private readonly List<Step> _steps = [];

    /// <summary>The one card opened inside (an expander, a picker), by its row's key.</summary>
    private string? _open;

    /// <summary>
    /// [S-2303] The pickers (and lists) unfolded inside the open card, by their rows' keys: inside a card each is folded
    /// until clicked (owner, 2026-11-19), as Karpo's picker heads in an expander; emptied as the card closes.
    /// </summary>
    private readonly HashSet<string> _unfolded = new(StringComparer.Ordinal);

    /// <summary>[S-2345] The tab chosen, by its key, kept while the page lives; null: the first.</summary>
    private string? _tab;

    /// <summary>[S-2302] Why a text typed into a box was not kept, by the box's key.</summary>
    private readonly Dictionary<string, string> _refused = new(StringComparer.Ordinal);

    /// <summary>[S-2308] What is typed in a lookup's box, by its row's key; [S-2351] in an open section's field, by its key and "#ask".</summary>
    private readonly Dictionary<string, string> _typed = new(StringComparer.Ordinal);

    /// <summary>[S-2308] A lookup's last answer, by its row's key, with what it was asked for.</summary>
    private readonly LookupAnswers<MenuSearchAnswer> _found = new();

    private CancellationTokenSource? _finding;

    private PageModel? _model;

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _lit;

    /// <summary>
    /// [S-2346] The panel the page stands in: a drawing of new rows fills a new one and puts it in this one's place; [S-2361] a
    /// drawing of the same rows (posted, never inside an event) puts what changed in this one.
    /// </summary>
    private StackPanel _body = Body();

    /// <summary>What this drawing puts on the page, in order.</summary>
    private List<UIElement> _drawn = [];

    /// <summary>[S-2361] The cards this drawing has put so far: one card is never put twice.</summary>
    private readonly HashSet<UIElement> _used = new(ReferenceEqualityComparer.Instance);

    /// <summary>[S-2361] A drawing of the same rows is posted; what is to be done once it is drawn (a reveal, the keyboard's place).</summary>
    private bool _redrawPosted;

    private readonly List<Action> _afterRedraw = [];

    /// <summary>[S-3557] The page went: a posted drawing draws nothing.</summary>
    private bool _closed;

    /// <summary>[S-2361] The cards drawn over <see cref="_keptFor"/>'s rows, by their first row's key, with the marks they were drawn with.</summary>
    private readonly Dictionary<string, (CardMarks Marks, FrameworkElement Card)> _kept = new(StringComparer.Ordinal);

    /// <summary>[S-2361] The model whose cards <see cref="_kept"/> holds: a new model keeps none.</summary>
    private PageModel? _keptFor;

    /// <summary>[S-2361] How many answers each lookup has had, by its row's key: an answer draws its card anew.</summary>
    private readonly Dictionary<string, int> _answered = new(StringComparer.Ordinal);

    /// <summary>[S-2361] This drawing's marks (<see cref="KeptCards"/>).</summary>
    private PageMarks? _marks;

    /// <summary>What this drawing has put on the page so far.</summary>
    private List<UIElement> Children => _drawn;

    public PageView(Func<PageModel> build)
    {
        _build = build ?? throw new ArgumentNullException(nameof(build));
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Top;
        Content = _body;
        Unloaded += (_, _) =>
        {
            Cancel();
            DropPreview();
        };

        // [S-2351] A press anywhere else on the page closes the section open, handled presses too (a head, a box).
        AddHandler(PointerPressedEvent, new PointerEventHandler(PressedOnPage), handledEventsToo: true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(ReleasedOnPage), handledEventsToo: true);
    }

    /// <summary>The page's key (its id, or the selection's).</summary>
    public string Key => _model?.Key ?? string.Empty;

    /// <summary>[S-2347] Asked to scroll an element whole into sight, as little as it takes (the opened card with its rows, a deep link's row).</summary>
    public event Action<FrameworkElement>? BringIntoView;

    /// <summary>Whether a sub-page stands in the page's place.</summary>
    public bool Deep => _steps.Count > 0;

    /// <summary>
    /// [S-3531] Draw the page as the state holds it now: its rows built again, the open card, the sub-page, the
    /// keyboard's place kept by the rows' keys. It throws nothing.
    /// </summary>
    public void Show() => Draw(rebuild: true);

    /// <summary>
    /// [S-2361] Draw the same rows again for a change of the page's own (a card opened or closed, a picker unfolded, a
    /// tab, a sub-page and back, a lookup's typing or answer), then do <paramref name="after"/> (a reveal, the keyboard
    /// put somewhere). Asked from the rows' own events, it is drawn after the event, once however often it was asked
    /// ([S-2346]: never inside the event that asked), in the panel that stands: every card that would be drawn the same
    /// stays. It throws nothing.
    /// </summary>
    private void Redraw(Action? after = null)
    {
        if (after is not null)
        {
            _afterRedraw.Add(after);
        }

        if (_redrawPosted)
        {
            return;
        }

        _redrawPosted = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, Redrawn))
        {
            // The dispatcher is shutting down: the window goes with it, there is nothing to draw on.
            _redrawPosted = false;
            _afterRedraw.Clear();
            Log.Warn(Scope, "a page's drawing could not be posted; the dispatcher is shutting down");
        }
    }

    /// <summary>[S-2361] The posted drawing's turn: the same rows drawn in place, then what waited for it.</summary>
    private void Redrawn()
    {
        _redrawPosted = false;
        var after = _afterRedraw.ToList();
        _afterRedraw.Clear();
        if (_closed)
        {
            return;
        }

        try
        {
            Draw(rebuild: false);
            foreach (var then in after)
            {
                then();
            }
        }
        catch (Exception ex)
        {
            // A dispatcher callback: nothing may escape it. The page stays as last drawn.
            Log.Error(Scope, "a page could not be drawn again", ex);
        }
    }

    private void Draw(bool rebuild)
    {
        _closed = false;
        var focused = FocusedTag();
        if (rebuild || _model is null)
        {
            try
            {
                _model = _build();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a page could not be built", ex);
                return;
            }
        }

        if (!ReferenceEquals(_keptFor, _model))
        {
            _kept.Clear();
            _keptFor = _model;
        }

        // [S-2346] New rows go into a new panel, the old one left whole: the row under the pointer stays in it until XAML is
        // done with it. Only the preview, kept from one drawing to the next ([S-2311]), comes out of it to go into the new one.
        if (rebuild && _preview is not null)
        {
            // ignore: false where the last drawing did not show it (a sub-page, a question).
            _ = _body.Children.Remove(_preview);
        }

        _drawn = [];
        _used.Clear();
        _grips.Clear();
        Drawings++;
        while (_steps.Count > 0 && _steps[^1] is SubStep { Key: var key } && Find(_model, key) is null)
        {
            _steps.RemoveAt(_steps.Count - 1);
        }

        // [S-2351] A section whose row went (a view deleted, a list's item removed) goes with it.
        if (_asks.Open is { } gone && HolderOf(_model, gone) is null)
        {
            DropAsk();
        }

        // [S-2361] [S-2351] What this drawing's cards are drawn with: the section asked in place is its card's.
        var asked = _asks.Open is { } open ? new PageAsked(open, _question?.Question, _typed.GetValueOrDefault(open + "#ask")) : null;
        _marks = new PageMarks(_open, _unfolded, _typed, _refused, _answered, asked);

        try
        {
            switch (_steps.LastOrDefault())
            {
                case SubStep sub when Find(_model, sub.Key) is { } row:
                    Children.Add(BackHead(row.Text));
                    AddCards(row.Rows ?? [], false);
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

        try
        {
            if (rebuild)
            {
                _body = Body();
                foreach (var element in _drawn)
                {
                    _body.Children.Add(element);
                }

                Content = _body;
            }
            else
            {
                // [S-2361] Posted, so no event is in the panel: what stays stays where it is, what changed is put in.
                Lay(_body.Children, _drawn);
            }

            if (focused is not null)
            {
                Refocus(focused.Value);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the page '{_model.Key}' could not be put in place", ex);
        }
    }

    /// <summary>A drawing's panel: the page's gutters round it.</summary>
    private static StackPanel Body() => new()
    {
        Padding = new Thickness(ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, ShellMetrics.Flyout.Gutter, P.Bottom),
    };

    /// <summary>[S-2346] How many times the page has been drawn: a render's check that a drawing waited for its turn.</summary>
    internal int Drawings { get; private set; }

    /// <summary>
    /// [S-3534] Open the page at the row along <paramref name="path"/> (the rows' keys, a section's first):
    /// its sub-pages and its card opened, the row scrolled into view and lit for a moment.
    /// </summary>
    public bool Open(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _steps.Clear();
        _open = null;
        _unfolded.Clear();
        DropAsk();
        _model ??= _build();

        // [S-2345] A way in lands on the tab its section stands on.
        if (path.Count > 0 && _model.Tabs.FirstOrDefault(t => t.Sections.Contains(path[0])) is { } holding)
        {
            _tab = holding.Key;
        }

        var rows = _model.Sections.FirstOrDefault(s => path.Count > 0 && s.Key == path[0])?.Rows;
        string? target = path.Count > 0 ? path[0] : null;
        var inCard = false;
        for (var i = 1; i < path.Count && rows is not null; i++)
        {
            var row = rows.FirstOrDefault(r => r.Key == path[i]);
            if (row is null)
            {
                break;
            }

            // Every card along the way opens, the row itself too where it opens (a deep link to a choice shows its options;
            // [S-2351] to a line that asks, its section).
            target = row.Key;
            if (row.Kind == SettingsRowKind.Ask)
            {
                _asks.Show(row.Key);
                if (!inCard)
                {
                    _open = null;
                    _unfolded.Clear();
                }
            }
            else if (row.Kind == SettingsRowKind.SubPage)
            {
                _steps.Add(new SubStep(row.Key));
                _open = null;
                _unfolded.Clear();
                inCard = false;
            }
            else if (Opens(row.Kind))
            {
                // [S-2303] Inside an opened card a picker is folded: a way in unfolds it, the card staying the one open.
                if (inCard)
                {
                    _unfolded.Add(row.Key);
                }
                else
                {
                    _open = row.Key;
                    inCard = row.Kind == SettingsRowKind.Expander;
                }
            }

            rows = row.Rows;
        }

        Show();
        if (target is null)
        {
            return false;
        }

        // [S-2347] The row with what it opened, as a click opens it: scrolled only as far as it takes.
        if (Reveal(target, section: true) is not { } found)
        {
            return _steps.Count > 0;
        }

        Light(found);
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

    /// <summary>
    /// [S-2327] Escape and the back head: [S-2351] the section asked closed first (nothing kept), then the page it stands
    /// on, then the open card closed; false with nothing to go back from.
    /// </summary>
    public bool Back()
    {
        if (CloseAsk())
        {
            return true;
        }

        if (_steps.Count > 0)
        {
            _steps.RemoveAt(_steps.Count - 1);
            Redraw(() => FocusFirst());
            return true;
        }

        if (_open is not null)
        {
            var open = _open;
            var keys = FromKeys();
            _open = null;
            _unfolded.Clear();
            Redraw(() => Refocus(new FocusTag(open + "#head", 0, keys)));
            return true;
        }

        return false;
    }

    /// <summary>[S-3557] The page goes: a search stops; [S-2351] a section asked closes, nothing kept.</summary>
    public void Close()
    {
        _steps.Clear();
        DropAsk();
        _closedAt = null;
        foreach (var asked in _typed.Keys.Where(k => k.EndsWith("#ask", StringComparison.Ordinal)).ToList())
        {
            _typed.Remove(asked);
        }

        _kept.Clear();
        _keptFor = null;
        _closed = true;
        _afterRedraw.Clear();
        Cancel();
        DropPreview();
    }

    /// <summary>The keyboard on the page's first control; false where it has none.</summary>
    public bool FocusFirst() => FocusManager.FindFirstFocusableElement(this) is Control first && first.Focus(FocusState.Keyboard);

    private void Cancel()
    {
        _finding?.Cancel();
        _finding?.Dispose();
        _finding = null;
    }

    /// <summary>
    /// [S-2305] The page's own level: its header card, then its sections; [S-2345] where it has tabs, their
    /// strip 8 under the header card and the chosen tab's sections 8 under it, a section named as its tab
    /// without its heading.
    /// </summary>
    private void DrawRoot(PageModel model)
    {
        if (model.Header is { } header)
        {
            Children.Add(Kept("header#card", KeptCards.Of([], false, _marks!), () => HeaderCard(header)));
        }

        var first = model.Header is null;
        var sections = model.Sections;
        string? tabTitle = null;
        if (model.Tabs.Count > 1)
        {
            var tab = model.Tabs.FirstOrDefault(t => t.Key == _tab) ?? model.Tabs[0];
            _tab = tab.Key;
            tabTitle = tab.Title;
            var strip = PageTabButton.Strip(model.Tabs.Select(t => (t.Key, t.Title)), tab.Key, key =>
            {
                _tab = key;
                _open = null;
                _unfolded.Clear();
                DropAsk();
                Redraw();
            });
            strip.Margin = new Thickness(0, first ? 0 : P.TabsGap - P.CardGap, 0, P.TabsGap);
            Children.Add(strip);
            sections = [.. model.Sections.Where(s => tab.Sections.Contains(s.Key))];
            first = true;
        }

        foreach (var section in sections)
        {
            if (section.Heading is { Length: > 0 } heading && heading != tabTitle)
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

    /// <summary>
    /// [S-2304] Rows as cards 4 apart; [S-2311] the preview bare, no card round it; [S-2318] a row that only acts
    /// its button alone, 8 under the card before it, the next such beside it 8 apart (Karpo's §578), what is said
    /// of how it went under it.
    /// </summary>
    private void AddCards(IReadOnlyList<PageRow> rows, bool inside)
    {
        StackPanel? feet = null;
        foreach (var card in SettingsRows.Cards(rows, r => r.Kind))
        {
            if (card[0] is var lead && Foot(lead) && AskOf(lead) is null && card.Skip(1).All(r => r.Kind == SettingsRowKind.Note))
            {
                if (feet is null)
                {
                    feet = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = P.FootGap,
                        Margin = new Thickness(0, P.FootGap - P.CardGap, 0, P.CardGap),
                    };
                    Children.Add(feet);
                }

                feet.Children.Add(new Border { Child = FootButton(lead, inside), Tag = lead.Key + "#row", CornerRadius = new CornerRadius(P.CardRadius) });
                foreach (var note in card.Skip(1).Where(n => n.Text.Length > 0))
                {
                    var words = CaptionText(note.Text, note.Danger);
                    words.Margin = new Thickness(P.Label, 0, P.EndInset, P.CardGap);
                    Children.Add(words);
                    feet = null;
                }

                continue;
            }

            feet = null;
            Children.Add(Kept(card, inside));
        }
    }

    /// <summary>[S-2361] A card of this drawing: the one drawn last over the same rows where its marks are the same, else drawn now.</summary>
    private FrameworkElement Kept(IReadOnlyList<PageRow> rows, bool inside)
        => Kept(rows[0].Key, KeptCards.Of(Keys(rows), inside, _marks!), () => rows is [{ Kind: SettingsRowKind.Preview } preview] ? PreviewRow(preview) : Card(rows, inside));

    /// <summary>[S-2361] The element kept under <paramref name="key"/> where it was drawn with <paramref name="marks"/> (and is not put yet), else <paramref name="draw"/>'s.</summary>
    private FrameworkElement Kept(string key, CardMarks marks, Func<FrameworkElement> draw)
    {
        if (!_kept.TryGetValue(key, out var kept) || !kept.Marks.Equals(marks) || _used.Contains(kept.Card))
        {
            kept = (marks, draw());
            _kept[key] = kept;
        }

        _used.Add(kept.Card);
        return kept.Card;
    }

    /// <summary>
    /// [S-2361] <paramref name="children"/> made <paramref name="wanted"/>, in its order: what goes is taken out, what is
    /// new put in, and what stays is left where it stands (never taken out and put back: it is not laid out again). A kept
    /// card is in this panel or in none: a drawing of the same rows takes what it does not show out of this panel, and new
    /// rows keep no card (the preview leaves the old panel before they are drawn).
    /// </summary>
    private static void Lay(UIElementCollection children, List<UIElement> wanted)
    {
        var keep = new HashSet<UIElement>(wanted, ReferenceEqualityComparer.Instance);
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(children[i]))
            {
                children.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < children.Count && ReferenceEquals(children[i], wanted[i]))
            {
                continue;
            }

            var at = children.IndexOf(wanted[i]);
            if (at >= 0)
            {
                children.RemoveAt(at);
            }

            children.Insert(i, wanted[i]);
        }
    }

    /// <summary>[S-2361] The keys of <paramref name="rows"/> and of every row in them, [S-2351] their tools' too.</summary>
    private static IEnumerable<string> Keys(IReadOnlyList<PageRow> rows)
    {
        foreach (var row in rows)
        {
            yield return row.Key;

            // [S-2351] A tool that asks first holds its section in this card.
            foreach (var tool in row.Tools ?? [])
            {
                yield return tool.Key;
            }

            if (row.Rows is { } inner)
            {
                foreach (var key in Keys(inner))
                {
                    yield return key;
                }
            }
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

        var card = new Border
        {
            Child = stack,
            Background = ShellBrush.Of(ShellRole.CardFill),
            BorderBrush = ShellBrush.Of(ShellRole.CardStroke),
            BorderThickness = new Thickness(FamilyShape.Hairline),
            CornerRadius = new CornerRadius(P.CardRadius),
            Margin = new Thickness(0, 0, 0, P.CardGap),
            Tag = rows[0].Key + "#card",
        };

        // [S-2310] A thing put in order is dragged by its grip, the whole card with it.
        if (rows is [{ Kind: SettingsRowKind.Expander, Order: { } order } thing] && _grips.TryGetValue(thing.Key, out var grip))
        {
            Draggable(card, grip, order, P.CardGap);
        }

        return card;
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

        // Not laid out yet: a border's child, or the page's own drawing before its template stands.
        return root switch
        {
            Border { Child: { } child } => Tagged(child, tag),
            ContentControl { Content: DependencyObject content } when count == 0 => Tagged(content, tag),
            _ => null,
        };
    }

    /// <summary>Where the keyboard is on the page: its control's tag and a text box's caret.</summary>
    /// <summary>
    /// Where the keyboard stands in a drawing, kept to the next: the control's tag, a text box's caret and [S-2351] whether the
    /// keys put it there (<paramref name="Keys"/>: its focus ring drawn, and drawn again on the control the next drawing makes).
    /// </summary>
    private readonly record struct FocusTag(string Tag, int Caret, bool Keys = false);

    private FocusTag? FocusedTag()
    {
        if (XamlRoot is null || FocusManager.GetFocusedElement(XamlRoot) is not FrameworkElement { Tag: string tag } element || !IsInside(element))
        {
            return null;
        }

        return new FocusTag(tag, element is TextBox box ? box.SelectionStart : 0, element is Control { FocusState: FocusState.Keyboard });
    }

    /// <summary>
    /// [S-2351] Whether what is being done now came from the keys: the page's focused control holds the keyboard's focus (a
    /// head or a tool pressed with Enter or Space), or a render's click, which stands for the keys (<see cref="DebugClick"/>).
    /// </summary>
    private bool FromKeys()
    {
        var keys = _keyed || FocusedTag() is { Keys: true };
        _keyed = false;
        return keys;
    }

    /// <summary>[S-2351] A render's click stands for the keys (Enter on its row): what it asks shows the keyboard's ring.</summary>
    private bool _keyed;

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
        if (Tagged(this, focus.Tag) is SearchBox search)
        {
            // [S-13606] The search box gives its keyboard to the text box inside it: the caret goes there.
            search.FocusAt(focus.Caret);
        }
        else if (Tagged(this, focus.Tag) is Control control)
        {
            // [S-2351] As the keys left it: its ring drawn again on the control this drawing made.
            control.Focus(focus.Keys ? FocusState.Keyboard : FocusState.Programmatic);
            if (control is TextBox box)
            {
                box.SelectionStart = Math.Min(focus.Caret, box.Text.Length);
            }
        }
    }

    /// <summary>
    /// [S-2347] The one way a row is brought into sight: the row keyed <paramref name="key"/> (or, with <paramref name="section"/>,
    /// the section keyed so) as the drawing just made laid it out (laid out first: until then the tree may still hold the
    /// drawing before, whose row is gone), scrolled only as far as it takes to stand whole in sight. Called once the drawing
    /// is made ([S-2346]: a drawing asked from an event is posted, <see cref="Redraw"/>, and this waits for it); null where
    /// the row is not on the page.
    /// </summary>
    private FrameworkElement? Reveal(string key, bool section = false)
    {
        UpdateLayout();
        if ((Tagged(this, key + "#row") ?? (section ? Tagged(this, key + "#section") : null)) is not { } found)
        {
            return null;
        }

        BringIntoView?.Invoke(found);
        return found;
    }

    /// <summary>[S-2308] The kinds of row that open in place: an expander and the inline pickers.</summary>
    private static bool Opens(SettingsRowKind kind)
        => kind is SettingsRowKind.Expander or SettingsRowKind.Choice or SettingsRowKind.Lookup or SettingsRowKind.Colour or SettingsRowKind.Font or SettingsRowKind.Icon;

#if DEBUG
    /// <summary>[S-2328] A render's click on the row at <paramref name="key"/>, as a click would do it; false where there is none.</summary>
    internal bool DebugClick(string key)
    {
        _model ??= _build();
        if (Find(_model, key) is not { } row)
        {
            return false;
        }

        // [S-2351] Drawn as the keys would leave it: a section it asks puts the keyboard on its answer, its ring drawn.
        _keyed = true;

        switch (row.Kind)
        {
            case SettingsRowKind.SubPage:
                Push(row);
                break;
            case SettingsRowKind.Ask:
                // [S-2351] Its cards opened as a click on the way would, its section asked in place.
                var path = PathTo(_model, key);
                if (path.Count > 1)
                {
                    _open = path[0];
                    _unfolded.Clear();
                    foreach (var on in path.Skip(1).Take(path.Count - 2))
                    {
                        _unfolded.Add(on);
                    }
                }

                ToggleAsk(row.Key, row.Key, inside: path.Count > 1);
                break;
            case var kind when Opens(kind):
                // [S-2303] A picker inside a card: its card opened, it unfolded in it.
                if (CardOf(_model, key) is { } card)
                {
                    _open = card.Key;
                    _unfolded.Add(row.Key);
                }
                else
                {
                    _open = row.Key;
                    _unfolded.Clear();
                }

                Redraw(() => Reveal(row.Key));
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

    /// <summary>[S-2345] A render's click on the tab keyed <paramref name="key"/>.</summary>
    internal void DebugTab(string key)
    {
        _tab = key;
        _open = null;
        _unfolded.Clear();
        DropAsk();
        Redraw();
    }

    /// <summary>The top-level expander of <paramref name="model"/> that <paramref name="key"/>'s row stands in; null for a row of a section's own.</summary>
    private static PageRow? CardOf(PageModel model, string key)
        => model.Sections.SelectMany(s => s.Rows).FirstOrDefault(r => r.Kind == SettingsRowKind.Expander && r.Key != key && r.Rows is { } inner && Holds(inner, key));

    private static bool Holds(IReadOnlyList<PageRow> rows, string key)
        => rows.Any(r => r.Key == key || (r.Rows is { } inner && Holds(inner, key)));

    /// <summary>[S-2360] A bench's cards to open: the keys of the page's own cards that open in place.</summary>
    internal IReadOnlyList<string> DebugCards()
        => [.. (_model ??= _build()).Sections.SelectMany(s => s.Rows).Where(r => r.Kind == SettingsRowKind.Expander).Select(r => r.Key)];

    /// <summary>[S-2328] A render's typing into the open lookup's box, or [S-2351] into the field of the section asked.</summary>
    internal void DebugType(string text)
    {
        if (_asks.Open is { } asked)
        {
            _typed[asked + "#ask"] = text;
            Redraw();
        }
        else if (_open is { } open)
        {
            _typed[open] = text;
            Redraw();
        }
    }

    /// <summary>[S-2328] A render's click on an item's tool.</summary>
    internal bool DebugTool(string key, int index)
    {
        _model ??= _build();
        if (Find(_model, key) is not { Tools: { } tools } row || index >= tools.Count)
        {
            return false;
        }

        UseTool(row, tools[index], inside: CardOf(_model, key) is not null);
        return true;
    }
#endif

    private abstract record Step;

    private sealed record SubStep(string Key) : Step;

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
}
