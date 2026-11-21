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
/// The page's rows are built again from the state on every change (<see cref="Show"/>): what is open, the
/// sub-page, the scroll and the keyboard's place are kept by the rows' keys, so a change made while one
/// is typing, dragging or picking leaves it where it was. [S-2361] A change of the state or of the page's own (what is
/// open, unfolded, typed or shown: <see cref="Redraw"/>) is drawn in the panel that stands: every card whose rows read
/// the same as when it was drawn (<see cref="PageBinding.Signature(PageRow)"/>) under the same marks (<see cref="KeptCards"/>)
/// stays where it is, untouched, doing what the newest rows of its keys do (<see cref="PageBinding"/>), and only the
/// cards that changed are built and put in: switching one setting draws its card alone.
/// </para>
/// <para>
/// [S-2346] Never the panel changed inside an event in it: a drawing the page's own events ask for is posted (<see cref="Redraw"/>,
/// <see cref="Rebuild"/>), and a change of the state reaches the page posted too. A drawing inside a row's own pointer event
/// (a click, a release) that took that row out of the panel while XAML was still handing the event through made XAML throw.
/// </para>
/// </summary>
internal sealed partial class PageView : ContentControl
{
    private const string Scope = "pages";

    private readonly Func<PageModel> _build;

    /// <summary>
    /// [S-2303] [S-2307] [S-2345] [S-2351] Where the page stands (Core's state machine): the sub-pages opened, the one card
    /// open, the pickers unfolded in it, the tab, the section asked in place. The view only draws it.
    /// </summary>
    private readonly PageNavigation _nav = new();

    /// <summary>The model <see cref="_navPage"/> was read from.</summary>
    private PageModel? _navFor;

    private NavPage? _navPage;

    /// <summary>[S-2302] Why a text typed into a box was not kept, by the box's key.</summary>
    private readonly Dictionary<string, string> _refused = new(StringComparer.Ordinal);

    /// <summary>[S-2308] What is typed in a lookup's box, by its row's key; [S-2351] in an open section's field, by its key and "#ask".</summary>
    private readonly Dictionary<string, string> _typed = new(StringComparer.Ordinal);

    /// <summary>[S-2308] A lookup's last answer, by its row's key, with what it was asked for.</summary>
    private readonly LookupAnswers<SettingsSearchAnswer> _found = new();

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

    /// <summary>
    /// [S-2361] The cards drawn, by their first row's key, with the marks they were drawn with and what their rows drew
    /// (<see cref="PageBinding.Signature(PageRow)"/>): a card whose rows and marks read the same is kept, whatever model it
    /// was drawn from.
    /// </summary>
    private readonly Dictionary<string, (CardMarks Marks, string Signature, FrameworkElement Card)> _kept = new(StringComparer.Ordinal);

    /// <summary>[S-2361] The rows' handlers bound late, by key, to the newest model's: a kept card does what the newest rows do.</summary>
    private readonly PageBinding _binding = new();

    /// <summary>[S-2361] The signature of each row of the model drawn, read once a model.</summary>
    private readonly Dictionary<PageRow, string> _signatures = new(ReferenceEqualityComparer.Instance);

    /// <summary>[S-2361] The keys of the cards this drawing put: a card kept for a row no longer drawn is let go.</summary>
    private readonly HashSet<string> _drawnKeys = new(StringComparer.Ordinal);

    /// <summary>[S-2361] A drawing of new rows is posted (<see cref="Rebuild"/>).</summary>
    private bool _rebuildPosted;

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
    public bool Deep => _nav.Steps.Count > 0;

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

        Post();
    }

    /// <summary>
    /// [S-2361] Build the rows again and draw them for a change the page's own event made (an answer given), then do
    /// <paramref name="after"/>: posted as <see cref="Redraw"/> is ([S-2346]), every card that reads the same kept.
    /// </summary>
    private void Rebuild(Action? after = null)
    {
        _rebuildPosted = true;
        Redraw(after);
    }

    private void Post()
    {
        if (_redrawPosted)
        {
            return;
        }

        _redrawPosted = true;
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, Redrawn))
        {
            // The dispatcher is shutting down: the window goes with it, there is nothing to draw on.
            _redrawPosted = false;
            _rebuildPosted = false;
            _afterRedraw.Clear();
            Log.Warn(Scope, "a page's drawing could not be posted; the dispatcher is shutting down");
        }
    }

    /// <summary>[S-2361] The posted drawing's turn: the same rows drawn in place, then what waited for it.</summary>
    private void Redrawn()
    {
        _redrawPosted = false;
        var rebuild = _rebuildPosted;
        _rebuildPosted = false;
        var after = _afterRedraw.ToList();
        _afterRedraw.Clear();
        if (_closed)
        {
            return;
        }

        try
        {
            Draw(rebuild);
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
                // [S-2361] Its handlers bound late: a card kept from the drawing before does what the new rows do.
                _model = _binding.Bind(_build());
                _signatures.Clear();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a page could not be built", ex);
                return;
            }
        }

        _drawn = [];
        _drawnKeys.Clear();
        _used.Clear();
        _grips.Clear();
        Drawings++;
        // [S-2361] A sub-page whose row went closes; [S-2351] a section whose row went (a view deleted, a list's item
        // removed) goes with it.
        if (_nav.Prune(Nav(_model)))
        {
            _question = null;
        }

        // [S-2361] [S-2351] What this drawing's cards are drawn with: the section asked in place is its card's.
        var asked = _nav.Asks.Open is { } open ? new PageAsked(open, _question?.Question, _typed.GetValueOrDefault(open + "#ask")) : null;
        _marks = new PageMarks(_nav.Open, _nav.Unfolded, _typed, _refused, _answered, asked);

        try
        {
            if (_nav.Deepest is { } deep && Find(_model, deep) is { } row)
            {
                Children.Add(BackHead(row.Text));
                AddCards(row.Rows ?? [], false);
            }
            else
            {
                DrawRoot(_model);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the page '{_model.Key}' could not be drawn", ex);
        }

        try
        {
            // [S-2361] What stays stays where it is, what changed is put in. [S-2346] Never inside an event in the panel: a
            // drawing the page's own events ask for is posted (Redraw, Rebuild), and a change of the state reaches the page
            // posted too (the bar's redraws, never under a press held on it).
            Lay(_body.Children, _drawn);
            foreach (var gone in _kept.Keys.Where(k => !_drawnKeys.Contains(k)).ToList())
            {
                _kept.Remove(gone);
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
        _question = null;
        _model ??= _build();

        // [S-3534] [S-2345] Its tab, its sub-pages, its cards opened and a picker inside one unfolded, a line that asks asked.
        var target = _nav.Enter(Nav(_model), path);
        Show();
        if (target is null)
        {
            return false;
        }

        // [S-2347] The row with what it opened, as a click opens it: scrolled only as far as it takes.
        if (Reveal(target, section: true) is not { } found)
        {
            return _nav.Steps.Count > 0;
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

        switch (_nav.Back())
        {
            case { Kind: NavBackKind.SubPage }:
                Redraw(() => FocusFirst());
                return true;
            case { Kind: NavBackKind.Card, Card: { } open }:
                var keys = FromKeys();
                Redraw(() => Refocus(new FocusTag(open + "#head", 0, keys)));
                return true;
            default:
                return false;
        }
    }

    /// <summary>[S-3557] The page goes: a search stops; [S-2351] a section asked closes, nothing kept.</summary>
    public void Close()
    {
        _nav.Close();
        _question = null;
        _closedAt = null;
        foreach (var asked in _typed.Keys.Where(k => k.EndsWith("#ask", StringComparison.Ordinal)).ToList())
        {
            _typed.Remove(asked);
        }

        _kept.Clear();
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
            Children.Add(Kept("header#card", KeptCards.Of([], false, _marks!), PageBinding.Signature(header), () => HeaderCard(header)));
        }

        var first = model.Header is null;
        var sections = model.Sections;
        string? tabTitle = null;
        if (model.Tabs.Count > 1 && _nav.ShownTab(Nav(model)) is { } shown)
        {
            var tab = model.Tabs.First(t => t.Key == shown.Key);
            tabTitle = tab.Title;
            var strip = PageTabButton.Strip(model.Tabs.Select(t => (t.Key, t.Title)), tab.Key, key =>
            {
                _nav.ChooseTab(key);
                _question = null;
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

    /// <summary>
    /// [S-2361] A card of this drawing: the one drawn last over rows that read the same (in this model or one before) where
    /// its marks are the same, else drawn now.
    /// </summary>
    private FrameworkElement Kept(IReadOnlyList<PageRow> rows, bool inside)
        => Kept(rows[0].Key, KeptCards.Of(Keys(rows), inside, _marks!), string.Concat(rows.Select(Signature)), () => rows is [{ Kind: SettingsRowKind.Preview } preview] ? PreviewRow(preview) : Card(rows, inside));

    /// <summary>
    /// [S-2361] The element kept under <paramref name="key"/> where it was drawn with <paramref name="marks"/> over rows of
    /// <paramref name="signature"/> (and is not put yet), else <paramref name="draw"/>'s.
    /// </summary>
    private FrameworkElement Kept(string key, CardMarks marks, string signature, Func<FrameworkElement> draw)
    {
        if (!_kept.TryGetValue(key, out var kept) || !kept.Marks.Equals(marks) || kept.Signature != signature || _used.Contains(kept.Card))
        {
            kept = (marks, signature, draw());
            _kept[key] = kept;
        }

        _drawnKeys.Add(key);
        _used.Add(kept.Card);
        return kept.Card;
    }

    /// <summary>
    /// [S-2361] What <paramref name="row"/> draws, read once a model. [S-2311] A picker standing open (or the Appearance's
    /// preview) builds what it shows from its row as it opens, so it never reads the same as in a model before.
    /// </summary>
    private string Signature(PageRow row)
    {
        if (!_signatures.TryGetValue(row, out var signature))
        {
            var open = row.Picker is not null && (_nav.Open == row.Key || _nav.Unfolded.Contains(row.Key));
            signature = PageBinding.Signature(row) + (open ? "\u0001" + Guid.NewGuid().ToString("N") : string.Empty);
            _signatures[row] = signature;
        }

        return signature;
    }

    /// <summary>
    /// [S-2361] <paramref name="children"/> made <paramref name="wanted"/>, in its order: what goes is taken out, what is
    /// new put in, and what stays is left where it stands (never taken out and put back: it is not laid out again). A kept
    /// card is in this panel or in none.
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
    private static bool Opens(SettingsRowKind kind) => PageNavigation.Opens(kind);

    /// <summary>[S-2302] <paramref name="model"/> as its navigation sees it: its rows' keys, kinds and tools; read once a model.</summary>
    private NavPage Nav(PageModel model)
    {
        if (!ReferenceEquals(_navFor, model) || _navPage is null)
        {
            static NavRow Of(PageRow row) => new(row.Key, row.Kind, [.. (row.Rows ?? []).Select(Of)])
            {
                Tools = row.Tools is { Count: > 0 } tools ? [.. tools.Select(t => t.Key)] : [],
            };

            _navPage = new NavPage(
                [.. model.Sections.Select(s => new NavSection(s.Key, [.. s.Rows.Select(Of)]))],
                [.. model.Tabs.Select(t => new NavTab(t.Key, t.Sections))]);
            _navFor = model;
        }

        return _navPage;
    }

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
                ToggleAsk(row.Key, row.Key, inside: _nav.OpenAlong(Nav(_model), key));
                break;
            case var kind when Opens(kind):
                // [S-2303] A picker inside a card: its card opened, it unfolded in it.
                _nav.Reach(Nav(_model), key);
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
        _nav.ChooseTab(key);
        _question = null;
        Redraw();
    }

    /// <summary>[S-2360] A bench's cards to open: the keys of the page's own cards that open in place.</summary>
    internal IReadOnlyList<string> DebugCards()
        => [.. (_model ??= _build()).Sections.SelectMany(s => s.Rows).Where(r => r.Kind == SettingsRowKind.Expander).Select(r => r.Key)];

    /// <summary>[S-2328] A render's typing into the open lookup's box, or [S-2351] into the field of the section asked.</summary>
    internal void DebugType(string text)
    {
        if (_nav.Asks.Open is { } asked)
        {
            _typed[asked + "#ask"] = text;
            Redraw();
        }
        else if (_nav.Open is { } open)
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

        UseTool(row, tools[index], inside: Nav(_model).CardOf(key) is not null);
        return true;
    }
#endif

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
