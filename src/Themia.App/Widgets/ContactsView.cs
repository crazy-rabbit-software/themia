using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Windows.ApplicationModel.DataTransfer;

namespace Themia.App.Widgets;

/// <summary>
/// [S-580] People from the user's Microsoft, Google and CardDAV address books, through Themia's own
/// accounts, merged into one list: as a list, compact rows, a details table or avatar cards
/// ([S-581]), with a search box ([S-584]) and per-person actions on right-click ([S-587]).
/// <para>
/// The view asks the shared <see cref="IOfficeSource"/> ([S-588]) and keeps the merged list it was
/// given; searching, capping and switching layouts redraw from that list without asking again.
/// </para>
/// <para>
/// It discloses by size ([S-1270]): names alone on an XS tile, then avatars and roles, a way to
/// reach each person, both address and number on a tall tile, rows flowed into columns on a wide
/// one. No detail pane beside the list at any size ([S-1274] retired).
/// </para>
/// </summary>
internal sealed partial class ContactsView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "contacts-widget";

    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(15);

    /// <summary>The search box and its margin, which the list gives up while it shows.</summary>
    private const double SearchHeight = 40;

    /// <summary>A grid card: 6 + 40 avatar + 2 + name 16 + 2 + detail 16 + 6.</summary>
    private const double CardHeight = 88;

    /// <summary>A large card ([S-1273]): 12 + 56 avatar + 4 + 2 + name 20 + 2 + two lines of 16 + 2 + 12.</summary>
    private const double LargeCardHeight = 142;

    /// <summary>Below this width the count gives its room to the title.</summary>
    private const double NarrowBelow = 160;

    private static readonly string PhoneGlyph = char.ConvertFromUtf32(0xE717);
    private static readonly string MessageGlyph = char.ConvertFromUtf32(0xE8BD);

    private readonly TextBox _search = new();
    private readonly Grid _columnHeader = new();
    private readonly ScrollViewer _scroll;
    private readonly OfficeStatePanel _state = new(Glyphs.Contact);
    private readonly ProgressRing _busy = new();
    private readonly FontIcon _warning;
    private readonly Button _searchButton;
    private readonly TextBlock _badge = new();
    private readonly Border _badgeHost;
    private readonly StackPanel _trailing;
    private readonly List<IDisposable> _hovers = [];
    private readonly Dictionary<string, Border> _indicators = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _rows = new(StringComparer.Ordinal);

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

    /// <summary>[S-1983] The names typed so far ([S-1980]).</summary>
    private readonly TypeAhead _typeAhead = new();
    private Screen _screen = Screen.Loading;
    /// <summary>The problem in words, worded when shown so a language switch reaches it ([S-1109]).</summary>
    private Func<string> _problemText = () => string.Empty;
    private bool _problemOffersAccounts;
    private Func<string>? _warningText;
    private bool _warningOpensAccounts;
    private string _requestKey = string.Empty;
    private bool _refreshing;
    private bool _again;
    private bool _againForced;
    private bool _detached;

    public ContactsView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _busy.Width = Fluent.IconSize;
        _busy.Height = Fluent.IconSize;
        _busy.IsActive = false;
        _busy.Visibility = Visibility.Collapsed;

        _warning = Fluent.Icon(Glyphs.Warning, "SystemFillColorCautionBrush");
        _warning.Visibility = Visibility.Collapsed;
        _warning.Tapped += OnWarningTapped;

        // [S-1275] Where the box would cost too much of a mid-size tile, the header offers it instead.
        // A header-sized button, like the calendar's paging ones, so the header keeps its height.
        _searchButton = new Button
        {
            Content = new FontIcon { Glyph = Glyphs.Search, FontSize = FluentType.Caption.FontSize },
            Padding = new Thickness(Fluent.Space1 + 2, Fluent.Space1, Fluent.Space1 + 2, Fluent.Space1),
            MinWidth = 0,
            MinHeight = 0,
            Background = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        ToolTipService.SetToolTip(_searchButton, Str.Contacts_SearchPlaceholder.Text());
        _searchButton.Click += OnSearchButton;

        _badgeHost = Fluent.Badge(_badge);
        _badgeHost.Visibility = Visibility.Collapsed;

        _trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _warning, _busy, _searchButton, _badgeHost },
        };

        // [S-584] The stock Windows search box, so it has the system's caret, clear button and menu.
        _search.PlaceholderText = Str.Contacts_SearchPlaceholder.Text();
        _search.IsSpellCheckEnabled = false;
        _search.IsTextPredictionEnabled = false;
        _search.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        _search.Margin = new Thickness(Fluent.Space3, 0, Fluent.Space3, Fluent.Space2);
        _search.TextChanged += OnSearchChanged;
        _search.GotFocus += OnSearchFocus;
        _search.LostFocus += OnSearchBlur;
        _search.KeyDown += OnSearchKeyDown;
        SetRow(_search, 0);

        _columnHeader.Padding = new Thickness(Fluent.Space4, 0, Fluent.Space4, Fluent.Space1);
        _columnHeader.Visibility = Visibility.Collapsed;
        SetRow(_columnHeader, 1);

        _scroll = new ScrollViewer
        {
            Padding = Fluent.ListPadding,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };
        SetRow(_scroll, 2);

        SetRow(_state, 0);
        SetRowSpan(_state, 3);
        SetColumnSpan(_state, 2);

        Children.Add(_search);
        Children.Add(_columnHeader);
        Children.Add(_scroll);
        Children.Add(_state);

        // [S-580] Clicking the background clears the selection, as the classic widget did. A click
        // anywhere else — another widget, the desktop, another app — ends the shell's keyboard lease,
        // which this widget holds from the click that selected, and that arrives as Escape ([S-807]).
        Tapped += OnBackgroundTapped;
    }

    private enum Screen
    {
        Loading,
        NoAccounts,
        AccountsGone,
        Problem,
        Ready,
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _trailing;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as ContactsConfig ?? new ContactsConfig();
        _source = OfficeSources.Current;

        if (_source is not null)
        {
            _source.Changed += OnAccountsChanged;
        }

        Strings.Changed += OnLanguage;
        _poll = ticker.Subscribe(Poll, () => _ = RefreshAsync(force: false));
    }

    /// <summary>[S-1109] Headings, states and the search box follow the language; nothing is refetched.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_searchButton, Str.Contacts_SearchPlaceholder.Text());
        _search.PlaceholderText = Str.Contacts_SearchPlaceholder.Text();
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

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    /// <summary>[S-581] Refresh. [S-1746] The layout is how the widget looks: it is chosen in its settings only.</summary>
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
        _searchButton.Click -= OnSearchButton;
        _warning.Tapped -= OnWarningTapped;
        Tapped -= OnBackgroundTapped;
        _state.Detach();
        ClearRows();
    }

    // -----------------------------------------------------------------------------------------
    // Data
    // -----------------------------------------------------------------------------------------

    private void OnAccountsChanged() => _ = RefreshAsync(force: false);

    private string RequestKey() => $"{string.Join(",", _config.AccountIds)}|{ContactRules.FetchSize(_config.EffectiveCount)}";

    private async Task RefreshAsync(bool force)
    {
        if (_detached)
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
            _problemText = () => Str.WidgetsA_Contacts_ReadFailed.Text();
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
        _busy.IsActive = busy;
        _busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
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
    private void OnSearchButton(object sender, RoutedEventArgs args)
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

    private void OnWarningTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_warningOpensAccounts)
        {
            args.Handled = true;
            OfficeStatePanel.OpenAccounts();
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
                ShellLauncher.OpenContactLink(link.Kind, link.Value);
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

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        ClearRows();

        var width = _area.Width;
        var height = _area.Height;
        var ready = _screen == Screen.Ready;
        _warning.Visibility = ready && _warningText is not null ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_warning, _warningText?.Invoke());
        _badgeHost.Visibility = Visibility.Collapsed;

        // [S-1275] The box is there when it costs little, on request on a mid-size tile, never on a small one.
        var search = ContactRules.Search(height);
        var showSearch = ready && (search == ContactSearch.Shown || (search == ContactSearch.OnDemand && (_searchOpen || _query.Length > 0)));
        _search.Visibility = showSearch ? Visibility.Visible : Visibility.Collapsed;
        _searchButton.Visibility = ready && search == ContactSearch.OnDemand && !showSearch ? Visibility.Visible : Visibility.Collapsed;

        switch (_screen)
        {
            case Screen.Loading:
                _state.Show(Str.Common_Loading.Text(), offerAccounts: false);
                return;

            case Screen.NoAccounts:
                // [S-1862] The card shows its Set up button over this.
                _state.Hide();
                return;

            case Screen.AccountsGone:
                _state.Show(SyncProblems.Message(SyncProblem.NoAccount, SyncSubject.Contacts, Strings.Current), offerAccounts: true);
                return;

            case Screen.Problem:
                _state.Show(_problemText(), _problemOffersAccounts);
                return;
        }

        // A search typed into a box that has since been hidden still filters; the box shows the
        // query again as soon as there is room for it.
        var visible = ContactRules.Visible(_contacts, _query, _config.EffectiveCount);
        _visible = visible;

        if (visible.Count == 0)
        {
            _state.Show((_contacts.Count == 0 ? Str.WidgetsA_Contacts_None : Str.Contacts_NoContacts).Text(), offerAccounts: false);

            // The box stays, so a search that matched nothing can be changed.
            if (showSearch)
            {
                _state.Margin = new Thickness(0, SearchHeight, 0, 0);
            }

            return;
        }

        _state.Margin = new Thickness(0);
        _state.Hide();

        _badge.Text = visible.Count.ToString(CultureInfo.InvariantCulture);
        // The count gives way to the title on a tile too narrow for both.
        _badgeHost.Visibility = width is > 0 and < NarrowBelow ? Visibility.Collapsed : Visibility.Visible;

        var layout = ContactRules.Effective(_config.EffectiveLayout, width, _config.EffectiveColumns.Count);

        // The list has the whole width at every size: no detail pane beside it ([S-1274] retired);
        // a person's actions are the row menu's.
        var listWidth = width;

        var listHeight = height - (showSearch ? SearchHeight : 0) - Fluent.ListPadding.Bottom;

        switch (layout)
        {
            case ContactsLayout.Details:
                DrawDetails(visible, listWidth, listHeight);
                break;

            case ContactsLayout.Grid:
                DrawCards(visible, width, listHeight);
                break;

            default:
                DrawList(visible, layout == ContactsLayout.Compact, listWidth, listHeight);
                break;
        }
    }

    private void ClearRows()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
        _indicators.Clear();
        _rows.Clear();
        _scroll.Content = null;
        _scroll.MaxHeight = double.PositiveInfinity;
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();
        _columnHeader.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// [S-581] List: avatar, name, then what the tile has room for ([S-1270]): the role, a way to
    /// reach them, or both the address and the number, each with its glyph. Compact ([S-1271]): one
    /// line — a small avatar, the name, and one field right-aligned when the column is wide enough.
    /// Rows flow into columns of 240 px, and the list shows whole rows only.
    /// </summary>
    private void DrawList(IReadOnlyList<Contact> contacts, bool compactLayout, double width, double height)
    {
        var columns = ContactRules.ListColumns(width);
        var columnWidth = width > 0 ? width / columns : 0;
        var detail = compactLayout ? ContactDetail.Name : ContactRules.ListDetail(new WidgetArea(_area.Width, height));
        var avatar = ContactRules.ShowsAvatar(columnWidth) && (compactLayout || detail != ContactDetail.Name);
        var aside = compactLayout && ContactRules.CompactAside(columnWidth);

        // Every row of a render is one height: the most lines anyone on screen has, at this level.
        var lines = contacts.Count == 0 ? 0 : contacts.Max(c => ContactRules.Lines(c, detail).Count);
        var rowHeight = compactLayout || lines == 0 ? Fluent.RowHeightCompact : ContactRules.RowHeight(lines);
        var avatarSize = compactLayout ? 24 : 32;

        var grid = new Grid();
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var i = 0; i < contacts.Count; i++)
        {
            var contact = contacts[i];
            if (i % columns == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var row = compactLayout
                ? CompactRow(contact, avatar ? avatarSize : 0, aside)
                : ListRow(contact, detail, avatar ? avatarSize : 0);
            row.MinHeight = rowHeight;
            row.Height = rowHeight;
            row.Padding = new Thickness(Fluent.Space4 - Fluent.Space2, 0, columns > 1 ? Fluent.Space2 : Fluent.Space4, 0);
            Wire(row, contact);
            SetRow(row, i / columns);
            SetColumn(row, i % columns);
            grid.Children.Add(row);
        }

        WholeRows((contacts.Count + columns - 1) / columns, rowHeight, height);
        _scroll.Content = grid;
    }

    private Border ListRow(Contact contact, ContactDetail detail, double avatarSize)
    {
        var text = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        var name = Fluent.Text(FluentType.BodyStrong);
        name.Text = contact.Name;
        text.Children.Add(name);

        foreach (var (kind, value) in ContactRules.Lines(contact, detail))
        {
            var line = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            line.Text = value;

            if (kind == ContactLine.Role || detail == ContactDetail.Role)
            {
                text.Children.Add(line);
                continue;
            }

            // [S-1270] The address and the number each carry their glyph, as the classic rows did.
            var glyph = Fluent.Icon(kind == ContactLine.Email ? Glyphs.Mail : PhoneGlyph, "TextFillColorTertiaryBrush", FluentType.Caption.FontSize);
            glyph.Margin = new Thickness(0, 0, Fluent.Space1 + 2, 0);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            var withGlyph = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
            };
            SetColumn(line, 1);
            withGlyph.Children.Add(glyph);
            withGlyph.Children.Add(line);
            text.Children.Add(withGlyph);
        }

        var row = Fluent.Row(RowBody(contact, avatarSize > 0 ? Avatar(contact, avatarSize) : null, text));
        return row;
    }

    /// <summary>[S-1271] One line: avatar, name, and the one field (email, else company, else phone) on the right.</summary>
    private Border CompactRow(Contact contact, double avatarSize, bool aside)
    {
        // The name keeps the larger share; the field trims first.
        var line = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) },
                new ColumnDefinition { Width = aside ? new GridLength(1, GridUnitType.Star) : new GridLength(0) },
            },
            VerticalAlignment = VerticalAlignment.Center,
        };

        var name = Fluent.Text(FluentType.Body);
        name.Text = contact.Name;
        line.Children.Add(name);

        if (aside && ContactRules.CompactDetail(contact) is { Length: > 0 } field)
        {
            var right = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            right.Text = field;
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.VerticalAlignment = VerticalAlignment.Center;
            right.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
            SetColumn(right, 1);
            line.Children.Add(right);
        }

        return Fluent.Row(RowBody(contact, avatarSize > 0 ? Avatar(contact, avatarSize) : null, line), compact: true);
    }

    /// <summary>
    /// [S-582] Details: a table of the chosen columns, in canonical order, under a header row; the
    /// name carries a small avatar when the table is wide enough to spare it ([S-1272]).
    /// </summary>
    private void DrawDetails(IReadOnlyList<Contact> contacts, double width, double height)
    {
        var columns = _config.EffectiveColumns;
        var avatars = width >= 360 && columns.Count > 0 && columns[0] == ContactColumn.Name;

        _columnHeader.Visibility = Visibility.Visible;
        AddColumns(_columnHeader, columns);
        for (var c = 0; c < columns.Count; c++)
        {
            var title = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            title.Text = ContactRules.ColumnTitle(columns[c], Strings.Current);
            title.Margin = new Thickness(c == 0 && avatars ? 20 + Fluent.Space2 : 0, 0, Fluent.Space2, 0);
            SetColumn(title, c);
            _columnHeader.Children.Add(title);
        }

        var list = new StackPanel { Spacing = 0 };

        foreach (var contact in contacts)
        {
            var cells = new Grid();
            AddColumns(cells, columns);

            for (var c = 0; c < columns.Count; c++)
            {
                var cell = Fluent.Text(c == 0 ? FluentType.BodyStrong : FluentType.Body, c == 0 ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
                cell.Text = ContactRules.Cell(contact, columns[c]);
                cell.Margin = new Thickness(0, 0, Fluent.Space2, 0);
                cell.VerticalAlignment = VerticalAlignment.Center;

                FrameworkElement content = cell;
                if (c == 0 && avatars)
                {
                    var face = Avatar(contact, 20);
                    face.Margin = new Thickness(0, 0, Fluent.Space2, 0);
                    var named = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = GridLength.Auto },
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        },
                    };
                    SetColumn(cell, 1);
                    named.Children.Add(face);
                    named.Children.Add(cell);
                    content = named;
                }

                SetColumn(content, c);
                cells.Children.Add(content);
            }

            var body = RowBody(contact, null, cells);
            var row = Fluent.Row(body, compact: true);
            row.Padding = new Thickness(Fluent.Space4 - Fluent.Space2, 0, Fluent.Space4, 0);
            Wire(row, contact);
            list.Children.Add(row);
        }

        WholeRows(contacts.Count, Fluent.RowHeightCompact, height - FluentType.Caption.LineHeight - Fluent.Space1);
        _scroll.Content = list;
    }

    /// <summary>
    /// Whole rows only: the viewport stops at the last row that fits instead of cutting a face in
    /// half; scrolling still reaches everyone.
    /// </summary>
    private void WholeRows(int rows, double rowHeight, double height)
    {
        var fit = Disclosure.WholeRows(height, rowHeight);
        if (height > 0 && fit > 0 && fit < rows)
        {
            _scroll.MaxHeight = (fit * rowHeight) + Fluent.ListPadding.Bottom;
        }
    }

    /// <summary>The classic widths: the phone a fixed column, the email a little wider than the rest.</summary>
    private static void AddColumns(Grid grid, IReadOnlyList<ContactColumn> columns)
    {
        foreach (var column in columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = column switch
                {
                    ContactColumn.Phone => new GridLength(124),
                    ContactColumn.Email => new GridLength(1.2, GridUnitType.Star),
                    _ => new GridLength(1, GridUnitType.Star),
                },
            });
        }
    }

    /// <summary>
    /// [S-581] Grid: avatar cards with the name and the company (or job title); on a large tile
    /// ([S-1273]) the cards grow a 56 px avatar and the address as a second line.
    /// </summary>
    private void DrawCards(IReadOnlyList<Contact> contacts, double width, double height)
    {
        var large = ContactRules.LargeCards(_area);

        // One height for every card (a person missing a field keeps the line), so rows align and
        // the viewport can stop at a whole row.
        var cardHeight = large ? LargeCardHeight : CardHeight;
        var perRow = ContactRules.CardsPerRow(width, large);
        var grid = new Grid { Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0) };

        for (var c = 0; c < perRow; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var i = 0; i < contacts.Count; i++)
        {
            if (i % perRow == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var contact = contacts[i];
            var stack = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Stretch };

            var avatar = Avatar(contact, large ? 56 : 40);
            avatar.HorizontalAlignment = HorizontalAlignment.Center;
            avatar.Margin = new Thickness(0, 0, 0, large ? Fluent.Space1 : 0);
            stack.Children.Add(avatar);

            var name = Fluent.Text(large ? FluentType.BodyStrong : FluentType.Strong(FluentType.Caption));
            name.Text = contact.Name;
            name.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(name);

            // A large card keeps one line per field even when a person lacks it, so the cards align.
            var lines = new List<string> { ContactRules.CardDetail(contact) };
            if (large)
            {
                lines.Add(contact.Email ?? contact.Phone ?? string.Empty);
            }

            foreach (var value in lines)
            {
                    var line = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                line.Text = value;
                line.HorizontalAlignment = HorizontalAlignment.Center;
                stack.Children.Add(line);
            }

            var card = new Border
            {
                Child = stack,
                Height = cardHeight,
                Padding = new Thickness(Fluent.Space1, large ? Fluent.Space3 : 6, Fluent.Space1, large ? Fluent.Space3 : 6),
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
            };

            ItemFill.Paint(card, ItemRole.Row);
            Wire(card, contact);
            SetRow(card, i / perRow);
            SetColumn(card, i % perRow);
            grid.Children.Add(card);
        }

        WholeRows((contacts.Count + perRow - 1) / perRow, cardHeight + 4, height);
        _scroll.Content = grid;
    }

    /// <summary>A row's content: the selection mark, the avatar when there is one, and the text.</summary>
    private Grid RowBody(Contact contact, FrameworkElement? avatar, FrameworkElement text)
    {
        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(Fluent.Space2) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };

        // [S-580] Selection is the accent pill Windows lists use, so it never fights the hover fill.
        var indicator = new Border
        {
            Width = 3,
            Height = Fluent.IconSize,
            CornerRadius = new CornerRadius(1.5),
            Background = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = contact.Id == _selectedId ? Visibility.Visible : Visibility.Collapsed,
        };
        _indicators[contact.Id] = indicator;
        body.Children.Add(indicator);

        if (avatar is not null)
        {
            SetColumn(avatar, 1);
            body.Children.Add(avatar);
        }

        SetColumn(text, 2);
        body.Children.Add(text);
        return body;
    }

    /// <summary>[S-586] Initials on a colour hashed from the name, so a person keeps their colour.</summary>
    private static Border Avatar(Contact contact, double size)
    {
        var initials = Fluent.Text(size >= 56 ? FluentType.Subtitle : size >= 40 ? FluentType.BodyStrong : FluentType.Caption);

        // The fill is always dark (34% lightness, the classic hsl), so the initials are always white,
        // in either theme; a theme brush would put black on it in the dark theme.
        initials.Foreground = new SolidColorBrush(Colors.White);
        initials.Text = Formatting.Monogram(contact.Name);
        initials.HorizontalAlignment = HorizontalAlignment.Center;
        initials.VerticalAlignment = VerticalAlignment.Center;
        initials.TextTrimming = TextTrimming.None;

        return new Border
        {
            Child = initials,
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = new SolidColorBrush(FromHsl(ContactRules.AvatarHue(contact.Name), 0.38, 0.34)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, size >= 32 ? Fluent.Space3 : Fluent.Space2, 0),
        };
    }

    /// <summary>Hover, select on click, and the per-person menu on right-click.</summary>
    private void Wire(Border row, Contact contact)
    {
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
    }

    private void Select(string? id)
    {
        if (id == _selectedId)
        {
            return;
        }

        if (_selectedId is { } old && _indicators.TryGetValue(old, out var previous))
        {
            previous.Visibility = Visibility.Collapsed;
        }

        _selectedId = id;

        if (id is not null && _indicators.TryGetValue(id, out var current))
        {
            current.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// [S-587] The classic contact menu: the name and role as a header, then write and copy the
    /// address, call, message and copy the number, copy the name, and refresh.
    /// </summary>
    private List<ItemMenuEntry> BuildMenu(Contact contact)
    {
        var menu = new List<ItemMenuEntry>
        {
            new(null, string.IsNullOrEmpty(ContactRules.Role(contact)) ? contact.Name : $"{contact.Name} · {ContactRules.Role(contact)}", static () => { }, Disabled: true),
            ItemMenuEntry.Separator,
        };

        if (ContactLinks.Build(ContactLinkKind.Email, contact.Email) is not null)
        {
            menu.Add(new(Glyphs.Mail, Str.CtxMenu_SendEmail.Text(), () => ShellLauncher.OpenContactLink(ContactLinkKind.Email, contact.Email)));
            menu.Add(new(Glyphs.Copy, Str.CtxMenu_CopyEmail.Text(), () => Copy(contact.Email!)));
        }

        if (ContactLinks.Build(ContactLinkKind.Call, contact.Phone) is not null)
        {
            menu.Add(new(PhoneGlyph, Str.CtxMenu_Call.Text(), () => ShellLauncher.OpenContactLink(ContactLinkKind.Call, contact.Phone)));
            menu.Add(new(MessageGlyph, Str.CtxMenu_Message.Text(), () => ShellLauncher.OpenContactLink(ContactLinkKind.Message, contact.Phone)));
        }

        if (!string.IsNullOrWhiteSpace(contact.Phone))
        {
            menu.Add(new(Glyphs.Copy, Str.CtxMenu_CopyPhone.Text(), () => Copy(contact.Phone!)));
        }

        menu.Add(new(Glyphs.Copy, Str.CtxMenu_CopyName.Text(), () => Copy(contact.Name)));
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

    /// <summary>The classic <c>hsl(h, 38%, 34%)</c>, as a colour.</summary>
    private static Windows.UI.Color FromHsl(int hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var x = c * (1 - Math.Abs((hue / 60.0 % 2) - 1));
        var m = lightness - (c / 2);

        var (r, g, b) = (hue / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return Windows.UI.Color.FromArgb(255, (byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
