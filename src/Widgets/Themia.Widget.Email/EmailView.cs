using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Mail;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Email;

namespace Themia.App.Widgets;

/// <summary>
/// [S-540] Recent mail from the accounts signed in to Themia: Microsoft (Graph), Google (Gmail) and
/// IMAP, merged into one list.
/// <para>
/// The view holds no account, token or socket. It opens a feed on the app's one
/// <see cref="IMailSource"/> for its accounts, folder and count, draws what the feed holds, and
/// redraws when the feed or the accounts change. Fetching, the cadence and the back-off are the
/// service's ([S-546]); which screen to show is the SDK's (<see cref="MailScreens"/>); the rows are
/// the kit's list (<see cref="KitList"/>, [S-6420]) in the look and on the ladder the style asks for.
/// </para>
/// <para>
/// [S-1280] It uses the whole tile: two- and three-line rows on a narrow tile, one line per message
/// on a wide strip and a big tile, the envelope from a medium tile; no provider name and no folder
/// summary. A click focuses a row, a double-click opens it, right-click offers what its account
/// allows ([S-544]).
/// </para>
/// </summary>
internal sealed partial class EmailView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "email-widget";

    /// <summary>Below this width the header has no room for a warning beside the title and the badge.</summary>
    private const double WarningNeedsWidth = 160;

    /// <summary>[S-1289] Below this width the sort button would squeeze the folder name; the menu still has it.</summary>
    private const double SortNeedsWidth = 200;

    /// <summary>[S-558] Below this width the compose button would squeeze the folder name; the menu still has it.</summary>
    private const double ComposeNeedsWidth = 240;

    /// <summary>Below this content height the accounts link under an empty state has no room.</summary>
    private const double LinkNeedsHeight = 100;

    private readonly TextBlock _badge = new();
    private readonly Border _badgeHost;
    private readonly FontIcon _warning;
    private readonly ProgressRing _busy = new();
    private readonly Border _sort;
    private readonly IDisposable _sortHover;
    private readonly Border _compose;
    private readonly IDisposable _composeHover;
    private readonly StackPanel _trailing;

    private readonly Grid _emptyHost = new();
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();
    private readonly HyperlinkButton _accountsLink;

    /// <summary>The kit's list, in the look of the style it was made for (a style change makes another).</summary>
    private KitList _list;
    private KitSpec _spec;

    /// <summary>The messages on screen, by item id, for the actions the list hands back.</summary>
    private readonly Dictionary<string, MailMessage> _shown = new(StringComparer.Ordinal);

    private IMailSource? _source;
    private IMailFeed? _feed;
    private string? _feedKey;
    private IWidgetSurface? _surface;
    private IDisposable? _clock;
    private EmailConfig _config = new();
    private string? _actionProblem;
    private bool _warningOpensAccounts;
    private bool _refreshing;
    private bool _detached;

    private WidgetArea _area;

    /// <summary>What the screen on display was drawn for, so a resize redraws only when an answer changes.</summary>
    private MailShape _shape;

    /// <summary>[S-6913] What the view asks of the host: the launcher, the Accounts page.</summary>
    private readonly IWidgetHost _widgetHost;

    public EmailView(IWidgetHost host)
    {
        _widgetHost = host;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _badgeHost = Fluent.Badge(_badge);
        _badgeHost.Visibility = Visibility.Collapsed;

        _warning = Fluent.Icon(Glyphs.Warning, "SystemFillColorCautionBrush", 14);
        _warning.Visibility = Visibility.Collapsed;
        _warning.Tapped += OnWarningTapped;

        _busy.Width = Fluent.IconSize;
        _busy.Height = Fluent.IconSize;
        _busy.IsActive = false;
        _busy.Visibility = Visibility.Collapsed;

        // [S-1289] The original's sort toggle, beside the badge: one click turns the list round.
        _sort = new Border
        {
            Child = Fluent.Icon(Glyphs.Sort, "TextFillColorSecondaryBrush", 14),
            Padding = new Thickness(Fluent.Space1),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Visibility = Visibility.Collapsed,
        };
        ItemFill.Paint(_sort, ItemRole.Row);
        _sort.Tapped += OnSortTapped;
        _sortHover = Fluent.Hover(_sort);

        // [S-558] Write a new email, beside the sort button: the account's own compose page.
        _compose = new Border
        {
            Child = Fluent.Icon(Glyphs.MailCompose, "TextFillColorSecondaryBrush", 14),
            Padding = new Thickness(Fluent.Space1),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Visibility = Visibility.Collapsed,
        };
        ItemFill.Paint(_compose, ItemRole.Row);
        _compose.Tapped += OnComposeTapped;
        _composeHover = Fluent.Hover(_compose);

        // Read once by the card when it builds the header, so it exists from construction.
        _trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _warning, _busy, _compose, _sort, _badgeHost },
        };

        _spec = MailSpec.Spec(_config.Style);
        _list = NewList(_spec);

        _empty = Fluent.EmptyState(Glyphs.Mail, _emptyText, out _);

        _accountsLink = new HyperlinkButton
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
            Visibility = Visibility.Collapsed,
        };
        _accountsLink.Click += OnAccountsClicked;

        // The sentence and the link, centred together. The sentence's panel sheds its icon and
        // lines against a height cap set on resize, because an Auto row measures without one.
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_empty, 1);
        Grid.SetRow(_accountsLink, 2);
        _emptyHost.Children.Add(_empty);
        _emptyHost.Children.Add(_accountsLink);
        _emptyHost.Visibility = Visibility.Collapsed;
        Grid.SetRow(_emptyHost, 0);
        Grid.SetRowSpan(_emptyHost, 3);

        Children.Add(_list);
        Children.Add(_emptyHost);
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _trailing;

    /// <summary>The kit's list for <paramref name="spec"/>, over the column titles' row and the rows' (the third row stays empty).</summary>
    private KitList NewList(KitSpec spec)
    {
        var list = new KitList(_widgetHost, spec, ActionsOf, Run);
        Grid.SetRow(list, 0);
        Grid.SetRowSpan(list, 2);
        return list;
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _source = _widgetHost.Mail;
        _list.Attach();

        if (_source is not null)
        {
            _source.AccountsChanged += OnAccountsChanged;
        }

        ApplyConfig(config);

        // Only the relative times ("4m") move between fetches; the feed itself is pushed.
        _clock = ticker.Subscribe(TimeSpan.FromMinutes(1), () => _list.Retime(DateTimeOffset.Now));
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as EmailConfig ?? new EmailConfig();
        UseStyle();
        PublishTitle();
        Reopen();
        Render();
    }

    /// <summary>[S-6403] A style with another look draws through a list made for it.</summary>
    private void UseStyle()
    {
        var spec = MailSpec.Spec(_config.Style);
        if (ReferenceEquals(spec, _spec))
        {
            return;
        }

        _list.Detach();
        Children.Remove(_list);
        _spec = spec;
        _list = NewList(spec);
        Children.Insert(0, _list);

        if (!_detached)
        {
            _list.Attach();
        }
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        FitEmptyState(area.Height);

        if (Shape() != _shape)
        {
            Reopen();
            Render();
        }
    }

    /// <summary>Open the feed this config asks for, letting go of the old one only if it changed.</summary>
    private void Reopen()
    {
        if (_source is null || _detached)
        {
            return;
        }

        var query = new MailQuery(_config.EffectiveAccountIds, _config.Folder, EmailConfig.FetchCount);
        if (_feed is not null && query.Key == _feedKey)
        {
            return;
        }

        CloseFeed();
        _feedKey = query.Key;
        _feed = _source.Open(query);
        _feed.Updated += OnFeedUpdated;
    }

    private void CloseFeed()
    {
        if (_feed is null)
        {
            return;
        }

        _feed.Updated -= OnFeedUpdated;
        _feed.Dispose();
        _feed = null;
        _feedKey = null;
    }

    private void OnFeedUpdated()
    {
        if (_refreshing && _feed is { Snapshot.Busy: false })
        {
            _refreshing = false;
        }

        _actionProblem = null;
        Render();
    }

    private void OnAccountsChanged() => Render();

    // -----------------------------------------------------------------------------------------
    // The ladder
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1280] What the tile holds: the kit's plan for the rows ([S-7032]..[S-7034]) and which of the
    /// header's buttons have room. The list itself is the whole shelf ([S-1286]) and scrolls past what
    /// fits, so the size never changes what is fetched.
    /// </summary>
    private MailShape Shape()
    {
        var plan = KitLadder.Plan(_area, _spec, MailSpec.Choices(_config));
        var width = _area.Width;
        return new MailShape(
            plan with { Table = null },
            plan.Table is { } table ? string.Join(",", table.Select(c => $"{c.Column}:{c.Width}:{c.Star}")) : string.Empty,
            Warning: width >= WarningNeedsWidth,
            Sort: width >= SortNeedsWidth,
            Compose: width >= ComposeNeedsWidth);
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

        _shape = Shape();

        var accounts = _source?.Accounts ?? [];
        var snapshot = _feed?.Snapshot ?? MailFeedSnapshot.Empty;
        var messages = _config.SortOrder == MailSortOrder.OldestFirst ? Enumerable.Reverse(snapshot.Messages).ToList() : snapshot.Messages;
        var screen = MailScreens.Decide(accounts, _config.EffectiveAccountIds, snapshot.Outcomes, messages.Count, _config.Folder);

        _busy.IsActive = _refreshing;
        _busy.Visibility = _refreshing ? Visibility.Visible : Visibility.Collapsed;

        var warning = _actionProblem ?? snapshot.WriteNote ?? screen.Warning;
        _warningOpensAccounts = _actionProblem is null && screen.OffersAccounts;
        // On a tile this narrow the glyph would squeeze the folder name to a letter; the warning waits.
        _warning.Visibility = warning is null || !_shape.Warning ? Visibility.Collapsed : Visibility.Visible;
        ToolTipService.SetToolTip(_warning, warning);

        // [S-558] Any screen with an account to write from offers it, an empty folder included.
        _compose.Visibility = _shape.Compose && ComposeAccounts().Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_compose, EmailStr.WidgetsB_Email_Compose.Text());

        if (screen.Kind != MailScreenKind.Messages)
        {
            ShowScreen(screen);
            return;
        }

        _emptyHost.Visibility = Visibility.Collapsed;
        _list.Visibility = Visibility.Visible;

        var unread = MailList.Unread(messages);
        _badge.Text = unread.ToString(CultureInfo.InvariantCulture);
        _badgeHost.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;

        _sort.Visibility = _shape.Sort ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_sort, _config.SortOrder == MailSortOrder.NewestFirst
            ? EmailStr.WidgetsB_Email_SortNewestTip.Text()
            : EmailStr.WidgetsB_Email_SortOldestTip.Text());

        var byId = accounts.ToDictionary(a => a.Id, StringComparer.Ordinal);
        _shown.Clear();
        var items = new List<KitItem>(messages.Count);

        foreach (var message in messages)
        {
            _shown[message.Key] = message;
            items.Add(MailSpec.Item(message, byId.GetValueOrDefault(message.AccountId)));
        }

        _list.Show(items, KitLadder.Plan(_area, _spec, MailSpec.Choices(_config)), _area, DateTimeOffset.Now);
    }

    private void ShowScreen(MailScreen screen)
    {
        _list.Clear();
        _list.Visibility = Visibility.Collapsed;
        _shown.Clear();
        _badgeHost.Visibility = Visibility.Collapsed;
        _sort.Visibility = Visibility.Collapsed;
        _emptyHost.Visibility = Visibility.Visible;
        _emptyText.Text = screen.Text;
        _accountsLink.Tag = screen.OffersAccounts;
        _accountsLink.Content = screen.Kind switch
        {
            MailScreenKind.SignInRequired => EmailStr.WidgetsB_Email_SignIn.Text(),
            _ => EmailStr.WidgetsB_Email_ChooseAccount.Text(),
        };
        FitEmptyState(_area.Height);
    }

    /// <summary>The link shows when the screen offers it and there is room; the sentence gets the rest.</summary>
    private void FitEmptyState(double height)
    {
        var offers = _accountsLink.Tag is true;
        var link = offers && (height <= 0 || height >= LinkNeedsHeight);
        _accountsLink.Visibility = link ? Visibility.Visible : Visibility.Collapsed;
        _empty.MaxHeight = height > 0 ? Math.Max(0, height - (link ? 36 : 0)) : double.PositiveInfinity;
    }

    // -----------------------------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-1288] [S-7020] A key from the shell's keyboard lease: the kit's list's one key map.</summary>
    public bool HandleKey(WidgetKey key) => !_detached && _list.HandleKey(key);

    /// <summary>[S-544] A row's actions, read fresh for every menu: what its account allows.</summary>
    private IReadOnlyList<KitAction> ActionsOf(KitItem item)
        => _shown.TryGetValue(item.Id, out var message) ? MailSpec.Actions(message, CanChange(message)) : [];

    private void OpenMessage(MailMessage message)
    {
        if (_source?.Open(message) == false)
        {
            ShowProblem(message.Provider == Themia.Core.Accounts.HostProviders.Imap
                ? EmailStr.WidgetsB_Email_ImapNoWebPage.Text()
                : EmailStr.WidgetsB_Email_CouldNotOpen.Text());
        }
    }

    /// <summary>Whether the message's account lets Themia change it (Google is read-only, [S-544]).</summary>
    private bool CanChange(MailMessage message)
        => _source?.Accounts.FirstOrDefault(a => a.Id == message.AccountId) is { ReadOnly: false };

    /// <summary>Carry out an action the list hands back: open, reply, a change, the copy.</summary>
    private void Run(KitItem item, KitAction action)
    {
        if (_detached || !action.Runnable || !_shown.TryGetValue(item.Id, out var message))
        {
            return;
        }

        switch (action.Id)
        {
            case MailSpec.OpenId:
                OpenMessage(message);
                return;

            case MailSpec.ReplyId:
                if (_source?.Reply(message) == false)
                {
                    ShowProblem(EmailStr.WidgetsB_Email_NoReplyAddress.Text());
                }

                return;
        }

        if (MailSpec.Change(action.Id) is { } change)
        {
            Change(message, change);
        }
        else if (action.Kind == KitActionKind.CopyText && !_widgetHost.CopyText(action.Text ?? string.Empty))
        {
            ShowProblem(EmailStr.WidgetsB_Email_CouldNotCopy.Text());
        }
    }

    private static ItemMenuEntry Item(string glyph, string text, Action action) => new(glyph, text, action);

    private async void Change(MailMessage message, MailChange change)
    {
        if (_source is null)
        {
            return;
        }

        try
        {
            var problem = await _source.ChangeAsync(message, change).ConfigureAwait(true);
            if (problem is not null)
            {
                ShowProblem(problem);
            }
        }
        catch (Exception ex)
        {
            // async void from a menu click: nothing above this may see an exception.
            Log.Error(Scope, $"{change} failed", ex);
            ShowProblem(Str.WidgetsB_Email_ChangeFailed.Text());
        }
    }

    /// <summary>A failed command, on the header's warning until the next update replaces it.</summary>
    private void ShowProblem(string problem)
    {
        if (_detached)
        {
            return;
        }

        _actionProblem = problem;
        _warningOpensAccounts = false;
        _warning.Visibility = _shape.Warning ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_warning, problem);
    }

    /// <summary>A warning about an account that needs signing in leads to where that is done.</summary>
    private void OnWarningTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_warningOpensAccounts)
        {
            args.Handled = true;
            OnAccountsClicked(sender, args);
        }
    }

    /// <summary>[S-558] The accounts a new message can come from: the ones this widget reads, signed in or not (compose needs no token).</summary>
    private IReadOnlyList<MailAccount> ComposeAccounts()
        => _source is null ? [] : MailScreens.Targets(_source.Accounts, _config.EffectiveAccountIds);

    /// <summary>[S-558] Start a new message from <paramref name="account"/>; a refusal is the header warning.</summary>
    private void Compose(MailAccount account)
    {
        if (_source?.Compose(account, _feed?.Snapshot.Messages ?? []) == false)
        {
            ShowProblem(EmailStr.WidgetsB_Email_CouldNotCompose.Text());
        }
    }

    /// <summary>[S-558] One account writes at once; several ask which, in a menu under the button.</summary>
    private void OnComposeTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        var accounts = ComposeAccounts();

        if (accounts.Count == 1)
        {
            Compose(accounts[0]);
        }
        else if (accounts.Count > 1)
        {
            ItemMenu.Open(_compose, accounts.Select(a => Item(Glyphs.Mail, a.Label, () => Compose(a))));
        }
    }

    /// <summary>[S-1289] The header's sort button turns the list round, saved like the menu's toggle.</summary>
    private void OnSortTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        Edit(c => c.SortOrder = c.SortOrder == MailSortOrder.NewestFirst ? MailSortOrder.OldestFirst : MailSortOrder.NewestFirst);
    }

    private void OnAccountsClicked(object sender, RoutedEventArgs args)
    {
        if (!_widgetHost.ShowAccounts())
        {
            Log.Info(Scope, "no settings window here to show the Accounts page in");
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            // [S-542] Refresh and the folder (which mail, not how it looks), the current one checked.
            // [S-1746] The view and the order are how the widget looks: they are in its settings only.
            // [S-559] Refresh says how old the list is; [S-558] a new email, from each account read.
            var age = MailScreens.UpdatedAgo(Strings.Current, _feed?.Snapshot.Outcomes ?? [], DateTimeOffset.Now);
            var actions = new List<WidgetAction>
            {
                new(Glyphs.Refresh, age is null ? Str.Common_Refresh.Text() : Str.Kit_RefreshUpdated.Format(("age", age)), Refresh),
            };

            var writers = ComposeAccounts();
            foreach (var account in writers)
            {
                actions.Add(new(Glyphs.MailCompose, writers.Count == 1
                    ? EmailStr.WidgetsB_Email_Compose.Text()
                    : EmailStr.WidgetsB_Email_ComposeFrom.Format(("account", account.Label)), () => Compose(account)));
            }

            foreach (var folder in EmailFolders.Listable)
            {
                actions.Add(new(_config.Folder == folder ? Glyphs.Done : FolderGlyph(folder), folder.Label(), () => Edit(c => c.Folder = folder)));
            }

            return actions;
        }
    }

    internal static string FolderGlyph(MailFolder folder) => folder switch
    {
        MailFolder.Sent => Glyphs.Send,
        MailFolder.Drafts => Glyphs.Edit,
        MailFolder.Deleted => Glyphs.Delete,
        MailFolder.Junk => Glyphs.Junk,
        _ => Glyphs.Mail,
    };

    private void Refresh()
    {
        if (_feed is null)
        {
            return;
        }

        _refreshing = true;
        _feed.Refresh();
        Render();
    }

    /// <summary>Change this widget's own config from its menu, save it, and redraw.</summary>
    private void Edit(Action<EmailConfig> change)
    {
        change(_config);
        _surface?.RequestSave();
        PublishTitle();
        Reopen();
        Render();
    }

    /// <summary>[S-542] The card's title names the folder; the user's own label still wins (the card decides).</summary>
    private void PublishTitle() => _surface?.SetHeaderTitle(_config.Folder.Label());

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _warning.Tapped -= OnWarningTapped;
        _sort.Tapped -= OnSortTapped;
        _sortHover.Dispose();
        _compose.Tapped -= OnComposeTapped;
        _composeHover.Dispose();
        _accountsLink.Click -= OnAccountsClicked;

        _clock?.Dispose();
        _clock = null;

        CloseFeed();

        if (_source is not null)
        {
            _source.AccountsChanged -= OnAccountsChanged;
            _source = null;
        }

        _list.Detach();
        _shown.Clear();
    }

    /// <summary>The answers a layout pass gave; a resize that changes none of them redraws nothing.</summary>
    private readonly record struct MailShape(
        KitPlan Plan,
        string Columns,
        bool Warning,
        bool Sort,
        bool Compose);
}
