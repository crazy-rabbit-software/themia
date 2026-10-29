using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Contacts;
using Windows.ApplicationModel.DataTransfer;

namespace Themia.App.Widgets;

/// <summary>
/// [S-580] People from the user's Microsoft, Google and CardDAV address books, through Themia's own
/// accounts, merged into one list: as a list, compact rows, a details table or a grid of cells
/// ([S-581]), with a search box ([S-584]) and per-person actions on right-click ([S-587]).
/// <para>
/// The view asks the shared <see cref="IOfficeSource"/> ([S-588]) and keeps the merged list it was
/// given; searching, capping and switching layouts redraw from that list without asking again.
/// </para>
/// <para>
/// It is drawn from the family's parts ([S-6711]..[S-6714]): the kit's header extras (the problem
/// glyph, the ring, the search button, the neutral count), Row blocks of one, two or three lines by
/// the tier leading with a 32 monogram (<see cref="ContactsPlan"/>, [S-1270]), the table's cells in
/// a Row block, <c>Row.Cell</c>s for the grid, and the kit's state row. No detail pane beside the
/// list at any size ([S-1274] retired).
/// </para>
/// </summary>
internal sealed partial class ContactsView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "contacts-widget";

    /// <summary>Contacts change rarely; the sync service keeps them fresher than this anyway.</summary>
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(15);

    private static readonly string PhoneGlyph = char.ConvertFromUtf32(0xE717);
    private static readonly string MessageGlyph = char.ConvertFromUtf32(0xE8BD);

    private readonly TextBox _search = new();
    private readonly Grid _columnHeader = new();
    private readonly ScrollViewer _scroll;
    private readonly KitStatePanel _state;
    private readonly KitHeader _header;
    private ScrollFade? _fade;
    private readonly List<IDisposable> _hovers = [];
    private readonly Dictionary<string, FrameworkElement> _rows = new(StringComparer.Ordinal);

    private IWidgetSurface? _surface;
    private IOfficeSource? _source;
    private IDisposable? _poll;
    private ContactsConfig _config = new();
    private WidgetArea _area;
    private string _query = string.Empty;
    private string? _selectedId;
    private bool _searchOpen;
    private IReadOnlyList<Contact> _contacts = [];
    private IReadOnlyList<Contact> _visible = [];

    /// <summary>[S-1983] Type-ahead over the names on screen.</summary>
    private readonly TypeAhead _typeAhead = new();
    private Screen _screen = Screen.Loading;
    private Func<string> _problemText = () => string.Empty;
    private bool _problemOffersAccounts;
    private Func<string>? _warningText;
    private bool _warningOpensAccounts;
    private string _requestKey = string.Empty;
    private bool _refreshing;
    private bool _busy;
    private bool _again;
    private bool _againForced;
    private bool _detached;

#if DEBUG
    /// <summary>A Debug render's fixture holds the list: no refresh replaces it.</summary>
    private bool _frozen;
#else
    private const bool _frozen = false;
#endif

    /// <summary>[S-6913] What the view asks of the host: the shared Office source, the launcher, ⚙ › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public ContactsView(IWidgetHost host)
    {
        _widgetHost = host;
        _state = new(Glyphs.Contact, host) { Visibility = Visibility.Collapsed };
        _header = new KitHeader(host);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // [S-584] The stock Windows search box, so it has the system's caret, clear button and menu:
        // one control of 32, the card's inset either side, 8 over the list.
        _search.PlaceholderText = ContactsStr.Contacts_SearchPlaceholder.Text();
        _search.IsSpellCheckEnabled = false;
        _search.IsTextPredictionEnabled = false;
        _search.Height = FamilyShape.Button;
        _search.Padding = new Thickness(FamilySpace.S3, FamilySpace.S1, FamilySpace.S2, FamilySpace.S1);
        _search.Visibility = Visibility.Collapsed;
        _search.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.S2);
        _search.TextChanged += OnSearchChanged;
        _search.GotFocus += OnSearchFocus;
        _search.LostFocus += OnSearchBlur;
        _search.KeyDown += OnSearchKeyDown;
        SetRow(_search, 0);

        _columnHeader.Visibility = Visibility.Collapsed;
        SetRow(_columnHeader, 1);

        _scroll = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };
        SetRow(_scroll, 2);

        SetRow(_state, 0);
        SetRowSpan(_state, 3);

        Children.Add(_search);
        Children.Add(_columnHeader);
        Children.Add(_scroll);
        Children.Add(_state);

        // [S-580] Clicking the background clears the selection, as the classic widget did. A click
        // anywhere else — another widget, the desktop, another app — ends the shell's keyboard lease,
        // which this widget holds from the click that selected, and that arrives as Escape ([S-807]).
        Tapped += OnBackgroundTapped;
    }

    /// <summary>Which of the widget's states is on screen.</summary>
    private enum Screen
    {
        Loading,
        NoAccounts,
        AccountsGone,
        Problem,
        Ready,
    }

    /// <summary>[S-7064] The kit's header extras: the problem glyph, the ring, the search button, the count.</summary>
    public FrameworkElement? HeaderTrailing => _header;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as ContactsConfig ?? new ContactsConfig();
        _source = _widgetHost.Office;

        if (_source is not null)
        {
            _source.Changed += OnAccountsChanged;
        }

        Strings.Changed += OnLanguage;

        // The ticker runs once as it subscribes.
        _poll = ticker.Subscribe(Poll, () => _ = RefreshAsync(force: false));
    }

    private void OnLanguage(Locale locale)
    {
        _search.PlaceholderText = ContactsStr.Contacts_SearchPlaceholder.Text();
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ContactsConfig ?? new ContactsConfig();

        if (RequestKey() != _requestKey)
        {
            _ = RefreshAsync(force: false);
        }

        Render();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    public IReadOnlyList<WidgetAction> ContextActions => [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = RefreshAsync(force: true))];

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _poll?.Dispose();
        _poll = null;

        if (_source is not null)
        {
            _source.Changed -= OnAccountsChanged;
            _source = null;
        }

        _surface?.RequestTextInput(false);
        _search.TextChanged -= OnSearchChanged;
        _search.GotFocus -= OnSearchFocus;
        _search.LostFocus -= OnSearchBlur;
        _search.KeyDown -= OnSearchKeyDown;
        Tapped -= OnBackgroundTapped;
        _state.Detach();
        _header.Detach();
        ClearRows();
    }

    // -----------------------------------------------------------------------------------------
    // Data
    // -----------------------------------------------------------------------------------------

    private void OnAccountsChanged() => _ = RefreshAsync(force: false);

    private string RequestKey() => $"{string.Join(",", _config.AccountIds)}|{ContactRules.FetchSize(_config.EffectiveCount)}";

    private async Task RefreshAsync(bool force)
    {
        if (_detached || _frozen)
        {
            return;
        }

        if (_refreshing)
        {
            _again = true;
            _againForced |= force;
            return;
        }

        var source = _source;
        var accounts = source?.Accounts(HostFeatures.Contacts) ?? [];
        _requestKey = RequestKey();

        if (source is null || accounts.Count == 0)
        {
            _screen = Screen.NoAccounts;
            Render();
            return;
        }

        var selected = CalendarRules.SelectAccounts(_config.AccountIds, [.. accounts.Select(a => a.Id)]);
        if (selected.Count == 0)
        {
            _screen = Screen.AccountsGone;
            Render();
            return;
        }

        var labels = accounts.ToDictionary(a => a.Id, a => a.Label, StringComparer.Ordinal);
        var size = ContactRules.FetchSize(_config.EffectiveCount);

        _refreshing = true;
        SetBusy(true);

        try
        {
            var tasks = selected.Select(a => source.ContactsAsync(a, size, force, CancellationToken.None)).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(true);

            if (_detached)
            {
                return;
            }

            var results = tasks.Select(t => t.Result).ToList();
            var problems = results.Select((r, i) => (Account: selected[i], r.Problem, r.At)).Where(p => p.Problem != SyncProblem.None).ToList();
            var anyData = results.Any(r => r.Value is not null);

            _contacts = ContactRules.Merge(results.Select(r => r.Value ?? []));

            if (!anyData && problems.Count > 0)
            {
                var worst = problems.OrderByDescending(p => SyncProblems.NeedsUser(p.Problem)).First();
                _screen = Screen.Problem;
                var problem = worst.Problem;
                _problemText = () => SyncProblems.Message(problem, SyncSubject.Contacts, Strings.Current);
                _problemOffersAccounts = SyncProblems.NeedsUser(worst.Problem);
                _warningText = null;
            }
            else
            {
                _screen = Screen.Ready;
                _warningText = problems.Count == 0
                    ? null
                    : () => string.Join(Environment.NewLine, problems.Select(p =>
                        Str.WidgetsA_Office_AccountProblem.Format(
                            ("account", labels.TryGetValue(p.Account, out var label) ? label : p.Account),
                            ("problem", Themia.Core.Sync.SyncFreshness.Stamp(Strings.Current, SyncProblems.Message(p.Problem, SyncSubject.Contacts, Strings.Current), p.At?.ToLocalTime(), DateTimeOffset.Now)))));
                _warningOpensAccounts = problems.Any(p => SyncProblems.NeedsUser(p.Problem));
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not refresh the contact list", ex);
            _screen = Screen.Problem;
            _problemText = () => ContactsStr.WidgetsA_Contacts_ReadFailed.Text();
            _problemOffersAccounts = false;
        }
        finally
        {
            _refreshing = false;
            SetBusy(false);
        }

        Render();

        if (_again && !_detached)
        {
            var forced = _againForced;
            _again = false;
            _againForced = false;
            await RefreshAsync(forced).ConfigureAwait(true);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ShowHeader();
    }

    // -----------------------------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-585] The list follows the box as it is typed in.</summary>
    private void OnSearchChanged(object sender, TextChangedEventArgs args)
    {
        if (_query == _search.Text)
        {
            return;
        }

        _query = _search.Text;
        Render();
    }

    // The surface takes no keystrokes unless asked: the box asks while it has focus ([S-584]).
    private void OnSearchFocus(object sender, RoutedEventArgs args) => _surface?.RequestTextInput(true);

    private void OnSearchBlur(object sender, RoutedEventArgs args)
    {
        _surface?.RequestTextInput(false);

        // [S-1275] A box opened from the header folds away again once it is left empty.
        if (_searchOpen && _query.Length == 0)
        {
            _searchOpen = false;
            Render();
        }
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            _search.Text = string.Empty;
        }
    }

    /// <summary>[S-1275] The header's search button opens the box and puts the caret in it.</summary>
    private void OpenSearch()
    {
        _searchOpen = true;
        Render();
        _search.Focus(FocusState.Programmatic);
    }

    private void OnBackgroundTapped(object sender, TappedRoutedEventArgs args)
    {
        if (!args.Handled && _selectedId is not null)
        {
            Select(null);
        }
    }

    // ---- keys ([S-1277], [S-807]) ----------------------------------------------------------------

    /// <summary>
    /// [S-1277]/[S-807] A key from the shell's keyboard lease (the classic S-31/32), which the
    /// widget holds from the click that selected someone: Up / Down / Home / End move the selection
    /// through the people on screen, Escape clears it — and the lease ending on a click outside
    /// arrives as Escape, which is the classic S-38 blur — Enter writes to the selected person (else
    /// calls them). Returns whether the key was used.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || _screen != Screen.Ready || _visible.Count == 0)
        {
            return false;
        }

        if (key == WidgetKey.Enter)
        {
            var person = _visible.FirstOrDefault(c => c.Id == _selectedId);
            if (person is not null && ContactRules.EnterLink(person) is { } link)
            {
                _widgetHost.Launcher.OpenContactLink(link.Kind, link.Value);
                return true;
            }

            return false;
        }

        if (key == WidgetKey.Escape)
        {
            _typeAhead.Reset();
        }

        var next = ContactRules.MoveSelection([.. _visible.Select(c => c.Id)], _selectedId, key);
        if (next == _selectedId)
        {
            return key == WidgetKey.Escape;
        }

        SelectAndShow(next);
        return true;
    }

    /// <summary>[S-1983] Type-ahead: the first letters of a name select that person.</summary>
    public bool HandleText(char character)
    {
        if (_detached || _screen != Screen.Ready || _visible.Count == 0)
        {
            return false;
        }

        var current = _selectedId is null ? -1 : _visible.ToList().FindIndex(c => c.Id == _selectedId);
        if (_typeAhead.Type(character, Environment.TickCount64, [.. _visible.Select(c => c.Name)], current) is not { } index)
        {
            return _typeAhead.Prefix.Length > 0;
        }

        SelectAndShow(_visible[index].Id);
        return true;
    }

    private void SelectAndShow(string? id)
    {
        Select(id);
        if (id is not null && _rows.TryGetValue(id, out var row))
        {
            row.StartBringIntoView();
        }
    }

    // -----------------------------------------------------------------------------------------
    // Drawing
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-1281] The rows' panel goes into the scroll, its rows fading at the foot while there is more below.</summary>
    private void Use(Panel rows)
    {
        _scroll.Content = rows;
        _fade = new ScrollFade(_scroll, rows);
        _fade.Refresh();
    }

    /// <summary>[S-7064] The header extras: the problem glyph while a list is up with an account that failed, the ring, the search button, the count.</summary>
    private void ShowHeader()
    {
        var ready = _screen == Screen.Ready;
        var problems = ready && _warningText is not null ? _warningText().Split(Environment.NewLine) : [];
        var search = ready && ContactRules.Search(_area.Height) == ContactSearch.OnDemand && !SearchShown(ready);
        IReadOnlyList<KitHeaderButton> buttons = search ? [new KitHeaderButton(Glyphs.Search, ContactsStr.Contacts_SearchPlaceholder.Text(), OpenSearch)] : [];
        _header.Show(problems, _busy, _warningOpensAccounts, ready && _visible.Count > 0 ? _visible.Count : null, buttons);
    }

    /// <summary>[S-1275] The box is there when it costs little, on request on a mid-size tile, never on a small one.</summary>
    private bool SearchShown(bool ready)
    {
        var search = ContactRules.Search(_area.Height);
        return ready && (search == ContactSearch.Shown || (search == ContactSearch.OnDemand && (_searchOpen || _query.Length > 0)));
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        ClearRows();

        var ready = _screen == Screen.Ready;
        var showSearch = SearchShown(ready);
        _search.Visibility = showSearch ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_state, 0);
        SetRowSpan(_state, 3);

        // A search typed into a box that has since been hidden still filters; the box shows the
        // query again as soon as there is room for it.
        _visible = ready ? ContactRules.Visible(_contacts, _query, _config.EffectiveCount) : [];
        ShowHeader();

        switch (_screen)
        {
            case Screen.Loading:
                _state.Show(Str.Common_Loading.Text(), offerAccounts: false);
                return;

            case Screen.NoAccounts:
                // [S-1862] The card shows its Set up row over this.
                _state.Hide();
                return;

            case Screen.AccountsGone:
                _state.Show(SyncProblems.Message(SyncProblem.NoAccount, SyncSubject.Contacts, Strings.Current), offerAccounts: true, problem: true);
                return;

            case Screen.Problem:
                _state.Show(_problemText(), _problemOffersAccounts, problem: true);
                return;
        }

        if (_visible.Count == 0)
        {
            // The box stays, so a search that matched nothing can be changed; the row stands under it.
            if (showSearch)
            {
                SetRow(_state, 2);
                SetRowSpan(_state, 1);
            }

            _state.Show((_contacts.Count == 0 ? ContactsStr.WidgetsA_Contacts_None : ContactsStr.Contacts_NoContacts).Text(), offerAccounts: false);
            return;
        }

        _state.Hide();

        var plan = ContactsPlan.Of(_area, _config.EffectiveLayout, _config.EffectiveColumns.Count, showSearch);
        switch (plan.Layout)
        {
            case ContactsLayout.Details:
                DrawDetails(_visible, plan);
                break;

            case ContactsLayout.Grid:
                DrawCells(_visible, plan);
                break;

            default:
                DrawRows(_visible, plan);
                break;
        }

        if (_selectedId is { } id)
        {
            Mark(id, true);
        }
    }

    private void ClearRows()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
        _rows.Clear();
        _fade?.Dispose();
        _fade = null;
        _scroll.Content = null;
        _scroll.Height = double.NaN;
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();
        _columnHeader.Visibility = Visibility.Collapsed;
    }

    /// <summary>[S-6712] The list is as tall as its whole rows; the rest is a scroll away.</summary>
    private void WholeRows(int lines, ContactsLayoutPlan plan)
    {
        var shown = Math.Max(1, Math.Min(lines, plan.Rows));
        _scroll.Height = lines > shown ? shown * plan.Pitch : double.NaN;
    }

    /// <summary>[S-1270] [S-1271] The list and the compact rows: Row blocks in columns, read left to right.</summary>
    private void DrawRows(IReadOnlyList<Contact> contacts, ContactsLayoutPlan plan)
    {
        var compact = plan.Layout == ContactsLayout.Compact;
        var grid = new Grid();
        for (var c = 0; c < plan.Columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var i = 0; i < contacts.Count; i++)
        {
            var contact = contacts[i];
            if (i % plan.Columns == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var block = new RowBlock(RowKind.List, plan.Avatar ? FamilyShape.IconLarge : 0) { Lines = plan.Lines };
            if (compact)
            {
                block.Show(contact.Name, plan.Aside ? ContactRules.CompactDetail(contact) : null);
            }
            else
            {
                var (second, third) = ContactsPlan.Lines(contact, plan.Lines);
                block.Show(contact.Name, null, second, third);
            }

            if (plan.Avatar)
            {
                block.Lead(Monogram(contact, FamilyShape.IconLarge));
            }

            var row = Item(block, contact);
            SetRow(row, i / plan.Columns);
            SetColumn(row, i % plan.Columns);
            grid.Children.Add(row);
        }

        WholeRows((contacts.Count + plan.Columns - 1) / plan.Columns, plan);
        Use(grid);
    }

    /// <summary>
    /// [S-582] The details table: one row of 32 per person, the chosen columns in the name's place 12
    /// apart (the name primary, the rest secondary), their titles in Caption over them.
    /// </summary>
    private void DrawDetails(IReadOnlyList<Contact> contacts, ContactsLayoutPlan plan)
    {
        var columns = _config.EffectiveColumns;

        _columnHeader.Visibility = Visibility.Visible;
        _columnHeader.Height = ContactsPlan.HeaderHeight;
        _columnHeader.Padding = Family.SideInset;
        AddColumns(_columnHeader, columns);
        for (var c = 0; c < columns.Count; c++)
        {
            var title = Family.Text(FamilyType.Caption, ColourRole.TextSecondary, ContactRules.ColumnTitle(columns[c], Strings.Current));
            title.VerticalAlignment = VerticalAlignment.Top;
            SetColumn(title, c);
            _columnHeader.Children.Add(title);
        }

        var list = new StackPanel();
        foreach (var contact in contacts)
        {
            var cells = new Grid();
            AddColumns(cells, columns);

            for (var c = 0; c < columns.Count; c++)
            {
                var cell = Family.Text(FamilyType.Body, c == 0 ? ColourRole.TextPrimary : ColourRole.TextSecondary, ContactRules.Cell(contact, columns[c]));
                cell.VerticalAlignment = VerticalAlignment.Center;
                SetColumn(cell, c);
                cells.Children.Add(cell);
            }

            var block = new RowBlock(RowKind.List, lead: 0);
            block.Cells(cells);
            list.Children.Add(Item(block, contact));
        }

        WholeRows(contacts.Count, plan);
        Use(list);
    }

    private static void AddColumns(Grid grid, IReadOnlyList<ContactColumn> columns)
    {
        grid.ColumnSpacing = FamilySpace.ColumnGap;
        foreach (var column in columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = column == ContactColumn.Email ? new GridLength(1.2, GridUnitType.Star) : new GridLength(1, GridUnitType.Star),
            });
        }
    }

    /// <summary>[S-1273] The grid: one <c>Row.Cell</c> per person (the monogram over the name), cells 12 apart, whole lines only.</summary>
    private void DrawCells(IReadOnlyList<Contact> contacts, ContactsLayoutPlan plan)
    {
        var panel = new Grid { Padding = Family.SideInset, ColumnSpacing = FamilySpace.ColumnGap, RowSpacing = FamilySpace.ColumnGap, HorizontalAlignment = HorizontalAlignment.Left };
        for (var c = 0; c < plan.Columns; c++)
        {
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(plan.ColumnWidth) });
        }

        for (var i = 0; i < contacts.Count; i++)
        {
            if (i % plan.Columns == 0)
            {
                panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var contact = contacts[i];
            var cell = new RowCell(plan.Icon);
            cell.Show(Monogram(contact, plan.Icon), contact.Name);
            cell.NameText.MaxWidth = plan.ColumnWidth;

            // The keyboard's ring over the cell, taking no room: selecting moves nothing.
            var ring = new Border { BorderThickness = new Thickness(FamilyShape.Stroke), CornerRadius = Family.Radius, BorderBrush = Family.Brush(ColourRole.Clear), IsHitTestVisible = false };
            var item = Item(new Grid { Children = { cell, ring } }, contact);
            SetRow(item, i / plan.Columns);
            SetColumn(item, i % plan.Columns);
            panel.Children.Add(item);
        }

        // The panel's last gap is not a line of its own.
        var lines = (contacts.Count + plan.Columns - 1) / plan.Columns;
        var shown = Math.Max(1, Math.Min(lines, plan.Rows));
        _scroll.Height = lines > shown ? (shown * plan.Pitch) - FamilySpace.ColumnGap : double.NaN;
        Use(panel);
    }

    /// <summary>
    /// [S-586] A person's monogram: one or two initials in the secondary role on the subtle fill, round
    /// (a person is not a data colour: no hue of its own, DECISIONS 6).
    /// </summary>
    private static Border Monogram(Contact contact, double size)
    {
        var initials = Family.Text(size >= FamilyShape.IconHuge ? FamilyType.BodyStrong : FamilyType.Caption, ColourRole.TextSecondary, Formatting.Monogram(contact.Name));
        initials.HorizontalAlignment = HorizontalAlignment.Center;
        initials.VerticalAlignment = VerticalAlignment.Center;
        initials.TextTrimming = TextTrimming.None;

        return new Border
        {
            Child = initials,
            Width = size,
            Height = size,
            CornerRadius = Family.Round(size, size),
            Background = Family.Brush(ColourRole.Subtle),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>A person's item: the item layer's fill and hover, its tooltip, a click selects, a right-click opens its menu.</summary>
    private Border Item(FrameworkElement content, Contact contact)
    {
        var row = new Border { Child = content, CornerRadius = content is RowBlock ? default : Family.Radius };
        ItemFill.Paint(row, ItemRole.Row);
        _hovers.Add(Fluent.Hover(row));
        _rows[contact.Id] = row;
        ToolTipService.SetToolTip(row, Tooltip(contact));
        WidgetItems.Mark(row); // [S-814]

        row.Tapped += (_, args) =>
        {
            // Handled, so the background tap that clears the selection does not also see it.
            args.Handled = true;
            Select(_selectedId == contact.Id ? null : contact.Id);
        };

        row.RightTapped += (_, args) =>
        {
            args.Handled = true;
            Select(contact.Id);
            ItemMenu.ShowAt(row, args.GetPosition(row), BuildMenu(contact));
        };

        return row;
    }

    /// <summary>[S-580] The selected person: the keyboard's ring (a Row block's, or a cell's outline).</summary>
    private void Mark(string id, bool on)
    {
        if (!_rows.TryGetValue(id, out var element) || element is not Border row)
        {
            return;
        }

        if (row.Child is RowBlock block)
        {
            block.Focused = on;
        }
        else if (row.Child is Grid { Children: [_, Border ring] })
        {
            ring.BorderBrush = Family.Brush(on ? ColourRole.Accent : ColourRole.Clear);
        }
    }

    private void Select(string? id)
    {
        if (id == _selectedId)
        {
            return;
        }

        if (_selectedId is { } old)
        {
            Mark(old, false);
        }

        _selectedId = id;

        if (id is not null)
        {
            Mark(id, true);
        }
    }

    private List<ItemMenuEntry> BuildMenu(Contact contact)
    {
        var menu = new List<ItemMenuEntry>
        {
            new(null, string.IsNullOrEmpty(ContactRules.Role(contact)) ? contact.Name : $"{contact.Name} · {ContactRules.Role(contact)}", static () => { }, Disabled: true),
            ItemMenuEntry.Separator,
        };

        if (ContactLinks.Build(ContactLinkKind.Email, contact.Email) is not null)
        {
            menu.Add(new(Glyphs.Mail, ContactsStr.CtxMenu_SendEmail.Text(), () => _widgetHost.Launcher.OpenContactLink(ContactLinkKind.Email, contact.Email)));
            menu.Add(new(Glyphs.Copy, ContactsStr.CtxMenu_CopyEmail.Text(), () => Copy(contact.Email!)));
        }

        if (ContactLinks.Build(ContactLinkKind.Call, contact.Phone) is not null)
        {
            menu.Add(new(PhoneGlyph, ContactsStr.CtxMenu_Call.Text(), () => _widgetHost.Launcher.OpenContactLink(ContactLinkKind.Call, contact.Phone)));
            menu.Add(new(MessageGlyph, ContactsStr.CtxMenu_Message.Text(), () => _widgetHost.Launcher.OpenContactLink(ContactLinkKind.Message, contact.Phone)));
        }

        if (!string.IsNullOrWhiteSpace(contact.Phone))
        {
            menu.Add(new(Glyphs.Copy, ContactsStr.CtxMenu_CopyPhone.Text(), () => Copy(contact.Phone!)));
        }

        menu.Add(new(Glyphs.Copy, ContactsStr.CtxMenu_CopyName.Text(), () => Copy(contact.Name)));
        menu.Add(ItemMenuEntry.Separator);
        menu.Add(new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = RefreshAsync(force: true)));
        return menu;
    }

    private static void Copy(string text)
    {
        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(text);
            Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            // Another process can hold the clipboard open; Windows fails the call rather than wait.
            Log.Error(Scope, "could not copy to the clipboard", ex);
        }
    }

    private static string Tooltip(Contact contact)
    {
        var lines = new List<string> { contact.Name };
        foreach (var line in new[] { ContactRules.Role(contact), contact.Email, contact.Phone })
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

}
