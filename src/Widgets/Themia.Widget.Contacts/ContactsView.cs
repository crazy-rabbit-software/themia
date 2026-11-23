using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Contacts;
using Windows.ApplicationModel.DataTransfer;

namespace Themia.App.Widgets;

/// <summary>
/// [S-580] [S-16972] People from the user's Microsoft, Google and CardDAV address books, through Themia's own accounts, merged into
/// one list on the item base (<see cref="ItemList"/>; owner, 2026-11-23: "same should be done for email and contacts too,
/// although contacts has the search box that should be kept"): the search box in its band on the card's top edge ([S-584],
/// [S-15775]), the people under it in the config's arrangement, each their monogram, name, role, address and number; a click
/// writes to them (else calls them, [S-1277]), a right-click offers their menu ([S-587]).
/// <para>
/// The view asks the shared <see cref="IOfficeSource"/> ([S-588]) and keeps the merged list it was given; searching and capping
/// redraw from that list without asking again. The kit's status and state row say what is not the list ([S-12481], [S-7077]).
/// </para>
/// </summary>
internal sealed partial class ContactsView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys, IWidgetGrounds
{
    /// <inheritdoc />
    public CardOpen? Opens => ContactsModule.Opens(_config.AccountIds, _widgetHost.AccountsWith(HostFeatures.Contacts));

    private const string Scope = "contacts-widget";

    /// <summary>Contacts change rarely; the sync service keeps them fresher than this anyway.</summary>
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(15);

    private readonly SearchBox _search = new(ContactsStr.Contacts_SearchPlaceholder.Text());

    /// <summary>[S-15775] The field's band: on the card's top edge (WR-1), whole cells tall, the field in it at the card's side inset.</summary>
    private readonly Border _fieldSlot = new() { Padding = Family.SideInset, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed, Margin = ListEdge.Margin };
    private readonly ItemList _list;
    private readonly KitStatePanel _state;
    private readonly KitStatus _status;
    private readonly ContactsFeed _feed;

    private IWidgetSurface? _surface;
    private IDisposable? _poll;
    private ContactsConfig _config = new();
    private WidgetArea _area;
    private string _query = string.Empty;
    private bool _searchOpen;
    private IReadOnlyList<Contact> _visible = [];
    private bool _detached;

    /// <summary>[S-1983] Type-ahead over the names on screen.</summary>
    private readonly TypeAhead _typeAhead = new();

    /// <summary>[S-6913] What the view asks of the host: the shared Office source, the launcher, settings › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public ContactsView(IWidgetHost host)
    {
        _widgetHost = host;
        _feed = new ContactsFeed(() => _config, () => _config.EffectiveCount);
        _feed.Changed += () =>
        {
            if (!_detached)
            {
                Render();
            }
        };
        _state = new(Glyphs.Contact, host) { Visibility = Visibility.Collapsed };
        _status = new KitStatus(host);
        _list = new ItemList(host)
        {
            Opened = item => Write(ContactOf(item)),
            CanOpen = () => _surface is not { EditMode: true } && !host.IsPreview,
            Menu = item => ContactOf(item) is { } contact ? BuildMenu(contact) : [],
            Decorate = (item, view) =>
            {
                if (ContactOf(item) is { } contact)
                {
                    LinkRow(view, contact);
                }
            },
            Redrawing = () => People.Clear(),
            CursorMoved = item => People.Point(item is not null && ContactOf(item) is { } contact ? PersonOf(contact) : null),
            PartTitle = part => ContactItems.PartName(part, Strings.Current),
        };
        _list.GroundsChanged += () => GroundsChanged?.Invoke();

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // [S-584] [S-13606] The one search box (Windows' own): the card's inset either side, [S-15775] in its band on the card's
        // top edge, the people right under it (WR-1, WR-5). [S-13607] Enter and Down select the first person shown, Escape empties it.
        _fieldSlot.Child = _search;
        ItemMark.Mark(_fieldSlot);
        _search.Count = () => _visible.Count;
        _search.PickBest = SelectFirst;
        _search.ToFirst = SelectFirst;
        _search.Changed += OnSearchChanged;
        _search.GotFocus += OnSearchFocus;
        _search.LostFocus += OnSearchBlur;

        SetRow(_list, 1);
        SetRow(_state, 1);
        Children.Add(_fieldSlot);
        Children.Add(_list);
        Children.Add(_state);

        // [S-12481] The problem glyph and the ring over the content's top end.
        _status.Place(this);
    }

    /// <inheritdoc />
    public event Action? GroundsChanged;

    /// <summary>[S-16640] With the gap on, every person's box (under the band where it shows).</summary>
    public IReadOnlyList<Themia.Core.Geometry.DipRect>? Grounds
        => _list.Grounds?.Select(r => new Themia.Core.Geometry.DipRect(r.X, r.Y + _list.Margin.Top + (_fieldSlot.Visibility == Visibility.Visible ? _fieldSlot.Height + _fieldSlot.Margin.Top : 0), r.Width, r.Height)).ToList();

    private bool Ready => _feed.Screen(Strings.Current).Kind == KitScreenKind.Items;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _feed.Start();
        _config = config as ContactsConfig ?? new ContactsConfig();
        _feed.Source = _widgetHost.Office;
        if (_feed.Source is not null)
        {
            _feed.Source.Changed += OnAccountsChanged;
        }

        Strings.Changed += OnLanguage;
        _links.Join(_surface?.Links);

        // The ticker runs once as it subscribes.
        _poll = ticker.Subscribe(Poll, () => _ = Refresh(force: false));
    }

    private void OnLanguage(Locale locale)
    {
        _search.Placeholder = ContactsStr.Contacts_SearchPlaceholder.Text();
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ContactsConfig ?? new ContactsConfig();
        if (_feed.Key() != _feed.RequestKey)
        {
            _ = Refresh(force: false);
        }

        Render();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        _list.Size(area);
        Render();
    }

    /// <summary>[S-12481] Refresh, and Search while the box is off (it opens the field over the people).</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => Ready && !FieldShown
            ? [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = Refresh(force: true)), new(Glyphs.Search, ContactsStr.Contacts_SearchPlaceholder.Text(), OpenSearch)]
            : [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = Refresh(force: true))];

    public bool WantsKeys => _list.WantsKeys;

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _feed.Stop();
        Strings.Changed -= OnLanguage;
        _poll?.Dispose();
        _poll = null;
        if (_feed.Source is not null)
        {
            _feed.Source.Changed -= OnAccountsChanged;
            _feed.Source = null;
        }

        _surface?.RequestTextInput(false);
        _search.Changed -= OnSearchChanged;
        _search.GotFocus -= OnSearchFocus;
        _search.LostFocus -= OnSearchBlur;
        _state.Detach();
        _status.Detach();
        _links.Leave();
    }

    private void OnAccountsChanged() => _ = Refresh(force: false);

    private Task Refresh(bool force) => _detached ? Task.CompletedTask : _feed.RefreshAsync(force);

    private Contact? ContactOf(Item item) => _visible.FirstOrDefault(c => c.Id == item.Id);

    // ---- input -------------------------------------------------------------------------------------

    /// <summary>[S-585] The list follows the box as it is typed in.</summary>
    private void OnSearchChanged(string text)
    {
        if (_query != text)
        {
            _query = text;
            Render();
        }
    }

    /// <summary>[S-13607] Enter or Down in the box: the first person shown takes the cursor.</summary>
    private void SelectFirst()
    {
        if (_visible.Count > 0)
        {
            _list.Point(0);
        }
    }

    // The surface takes no keystrokes unless asked: the box asks while it has focus ([S-584]).
    private void OnSearchFocus(object sender, RoutedEventArgs args) => _surface?.RequestTextInput(true);

    private void OnSearchBlur(object sender, RoutedEventArgs args)
    {
        _surface?.RequestTextInput(false);

        // [S-1275] A box opened from the card menu folds away again once it is left empty.
        if (_searchOpen && _query.Length == 0)
        {
            _searchOpen = false;
            Render();
        }
    }

    /// <summary>[S-1275] The card menu's Search opens the field over the people and puts the caret in it.</summary>
    private void OpenSearch()
    {
        _searchOpen = true;
        Render();
        _search.FocusEnd();
    }

    /// <summary>[S-1277] [S-807] The item base's keys over the people; Enter writes to the one under the cursor (else calls them).</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || !Ready || _visible.Count == 0)
        {
            return false;
        }

        if (key == WidgetKey.Escape)
        {
            _typeAhead.Reset();
        }

        return _list.HandleKey(key);
    }

    /// <summary>[S-1983] Type-ahead: the first letters of a name put the cursor on that person.</summary>
    public bool HandleText(char character)
    {
        if (_detached || !Ready || _visible.Count == 0)
        {
            return false;
        }

        if (_typeAhead.Type(character, Environment.TickCount64, [.. _visible.Select(c => c.Name)], -1) is not { } index)
        {
            return _typeAhead.Prefix.Length > 0;
        }

        _list.Point(index);
        return true;
    }

    /// <summary>[S-1277] A person's click or Enter: write to them, else call them.</summary>
    private void Write(Contact? person)
    {
        if (person is not null && ContactRules.EnterLink(person) is { } link)
        {
            _widgetHost.Launcher.OpenContactLink(link.Kind, link.Value);
        }
    }

    // ---- drawing -------------------------------------------------------------------------------------

    /// <summary>[S-11406] The field is there when the config has it, or the card menu opened it, or a query is typed.</summary>
    private bool FieldShown => ContactRules.Search(_config.SearchBox) == ContactSearch.Shown || _searchOpen || _query.Length > 0;

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var locale = Strings.Current;
        var grid = _area.Grid(XamlRoot?.RasterizationScale ?? 1);

        // [S-15785] The field is part of the face: it stands where the config has it in every state, the state's row under it.
        var field = FieldShown;
        _fieldSlot.Visibility = field ? Visibility.Visible : Visibility.Collapsed;
        _fieldSlot.Height = ContactItems.Band(grid);
        _search.Fill(_fieldSlot.Height);
        _search.VerticalAlignment = VerticalAlignment.Center;
        _list.Margin = field ? default : ListEdge.Margin;
        _state.Margin = field ? new Thickness(0, FamilySpace.Inset, 0, 0) : default;

        var screen = _feed.Screen(locale);
        var ready = screen.Kind == KitScreenKind.Items;
        if (ready && _feed.Held is null)
        {
            ContactsSeen.Last = _feed.People;
        }

        _visible = ready ? ContactRules.Visible(_feed.People, _query, _config.EffectiveCount) : [];

        // [S-7042] [S-7077] The one stale rule: each account whose last sync could not improve on the people held.
        var problems = ready && _feed.Held is null ? KitFreshness.Lines(locale, _feed.Outcomes, DateTimeOffset.Now) : [];
        _status.Show(problems, _feed.Busy, ready && screen.OffersAccounts);
        _state.RowPitch = ItemLayout.Size(_config.Style, ItemShape.Of(ContactItems.Parts, _config.HiddenParts), ItemCells.Base, grid).Height;

        if (screen.Kind == KitScreenKind.NoAccounts)
        {
            // [S-1862] [S-12835] [S-15785] Set up in place, as the calendar: the state's compact line says what it waits for,
            // with the way to Accounts, over the face kept empty (WR-10).
            Empty();
            _state.Show(((ISetupRule)_config).SetupSentence ?? ContactsStr.Contacts_Setup_Sentence.Text(), offerAccounts: true);
            return;
        }

        if (!ready)
        {
            Empty();
            _state.Show(screen);
            return;
        }

        if (_visible.Count == 0)
        {
            // The box stays, so a search that matched nothing can be changed; the row stands under it.
            Empty();
            _state.Show((_feed.People.Count == 0 ? ContactsStr.WidgetsA_Contacts_None : ContactsStr.Contacts_NoContacts).Text(), offerAccounts: false);
            return;
        }

        _state.Hide();
        _list.Visibility = Visibility.Visible;
        _list.Show([.. _visible.Select(ContactItems.Item)], _config, ContactItems.Parts);
    }

    private void Empty()
    {
        _list.Show([], _config, ContactItems.Parts);
        _list.Visibility = Visibility.Collapsed;
    }

    // ---- the person's menu ([S-587]) ------------------------------------------------------------------

    private List<ItemMenuEntry> BuildMenu(Contact contact) => ContactMenu.Of(contact, _widgetHost, () => _ = Refresh(force: true));
}

/// <summary>[S-587] A person's menu: their name and role, write, copy the address, call, message, copy the number, copy the name, Refresh.</summary>
internal static class ContactMenu
{
    private const string Scope = "contacts-widget";

    public static List<ItemMenuEntry> Of(Contact contact, IWidgetHost host, Action refresh)
    {
        var menu = new List<ItemMenuEntry>
        {
            new(null, string.IsNullOrEmpty(ContactRules.Role(contact)) ? contact.Name : $"{contact.Name} · {ContactRules.Role(contact)}", static () => { }, Disabled: true),
            ItemMenuEntry.Separator,
        };

        if (ContactLinks.Build(ContactLinkKind.Email, contact.Email) is not null)
        {
            menu.Add(new(Glyphs.Mail, ContactsStr.CtxMenu_SendEmail.Text(), () => host.Launcher.OpenContactLink(ContactLinkKind.Email, contact.Email)));
            menu.Add(new(Glyphs.Copy, ContactsStr.CtxMenu_CopyEmail.Text(), () => Copy(contact.Email!)));
        }

        if (ContactLinks.Build(ContactLinkKind.Call, contact.Phone) is not null)
        {
            menu.Add(new(Glyphs.Call, ContactsStr.CtxMenu_Call.Text(), () => host.Launcher.OpenContactLink(ContactLinkKind.Call, contact.Phone)));
            menu.Add(new(Glyphs.Unread, ContactsStr.CtxMenu_Message.Text(), () => host.Launcher.OpenContactLink(ContactLinkKind.Message, contact.Phone)));
        }

        if (!string.IsNullOrWhiteSpace(contact.Phone))
        {
            menu.Add(new(Glyphs.Copy, ContactsStr.CtxMenu_CopyPhone.Text(), () => Copy(contact.Phone!)));
        }

        menu.Add(new(Glyphs.Copy, ContactsStr.CtxMenu_CopyName.Text(), () => Copy(contact.Name)));
        menu.Add(ItemMenuEntry.Separator);
        menu.Add(new(Glyphs.Refresh, Str.Common_Refresh.Text(), refresh));
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
}
