using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Tasks;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Tasks;

namespace Themia.App.Widgets;

/// <summary>
/// [S-590] A task list: Microsoft To Do or Google Tasks through Themia's own accounts, or a list kept
/// on this PC — read, ticked off, added to, renamed, reordered and deleted from the widget itself.
/// <para>
/// No connection of its own. The one <see cref="TaskService"/> owns the requests, the refresh
/// cadence and the optimistic writes, and two widgets on the same list share one feed. This view
/// decides only <i>which</i> list it shows and how a row looks.
/// </para>
/// <para>
/// Every state that is not a list says which it is and what to do about it ([S-181]): no account,
/// an account that needs signing in again, an account gone, no lists, loading, offline, and a
/// provider error each have their own sentence, and the ones the Accounts page can mend link to it.
/// A list on this PC needs no account at all.
/// </para>
/// </summary>
internal sealed partial class TasksView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "tasks-widget";

    /// <summary>Below this the link under an empty state is dropped; the sentence is worth more.</summary>
    private const double LinkNeedsHeight = 140;

    /// <summary>[S-607] The kit's header: the problem glyph, the ring and the open count, before the list picker.</summary>
    private readonly KitHeader _header;
    private readonly StackPanel _trailing;

    /// <summary>The open tasks the badge counts, or null for none shown.</summary>
    private long? _openCount;
    private readonly TaskRowsPanel _rowsPanel = new();
    private readonly ScrollViewer _scroll;
    private readonly TaskListHost _listHost;

    /// <summary>[S-783] "7 open · 2 overdue · 1 due today" over the list on a panel.</summary>
    private readonly Grid _emptyHost = new();
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();
    private readonly HyperlinkButton _emptyAction;

    /// <summary>The second way out of the no-account state: a list on this PC needs no account.</summary>

    private ITaskSource? _service;

    /// <summary>The lists on this PC the rows are read from: the service's (a debug render's own fixture).</summary>
    private ILocalTaskLists? _local;
    private Ticker? _ticker;
    private IWidgetSurface? _surface;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval;
    private TasksConfig _config = new();

    /// <summary>The account feed this widget holds, or null for a list on this PC or no list at all.</summary>
    private ITaskFeed? _feed;

    /// <summary>The list on this PC this widget shows, or null.</summary>
    private string? _localListId;

    /// <summary>The account the widget resolved to, for the empty states that name it.</summary>
    private TaskAccount? _account;

    /// <summary>The default list's name, once the lists are known: the header title for an unnamed choice.</summary>
    private string? _resolvedListName;

    /// <summary>Why the lists could not be read, when resolving the default list failed.</summary>
    private AccountException? _listsFailure;

    private Screen _screen = Screen.Starting;
    private BindKey? _bound;
    private int _bindGeneration;
    private bool _detached;

    /// <summary>The slot the card last gave this view ([S-663]); unmeasured until the first layout pass.</summary>
    private WidgetArea _area;

    /// <summary>[S-789] List names by id, for the Today view's rows on a panel. Read from the service's cache.</summary>
    private IReadOnlyDictionary<string, string> _listNames = new Dictionary<string, string>();

    /// <summary>[S-6913] What the view asks of the host: the launcher, the Accounts page.</summary>
    private readonly IWidgetHost _widgetHost;

    public TasksView(IWidgetHost host)
    {
        _widgetHost = host;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // [S-607] A change the provider refused, or a refresh that failed with tasks still on screen:
        // the list stays, and the kit's problem glyph says it may be out of date and why.
        _header = new KitHeader(host);

        BuildPicker();

        _trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _header, _picker },
        };

        _scroll = new ScrollViewer
        {
            // Nothing either side, so a row and its hover reach both card edges.
            Padding = Fluent.ListPadding,
            Content = _rowsPanel,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        _empty = Fluent.EmptyState(Glyphs.Task, _emptyText, out _);

        _emptyAction = new HyperlinkButton
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
            Visibility = Visibility.Collapsed,
        };
        _emptyAction.Click += OnEmptyActionClicked;

        // The sentence and its link, centred together.
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_empty, 1);
        Grid.SetRow(_emptyAction, 2);
        _emptyHost.Children.Add(_empty);
        _emptyHost.Children.Add(_emptyAction);
        _emptyHost.Visibility = Visibility.Collapsed;

        _addRow = BuildAddRow();
        _listHost = new TaskListHost(_scroll, _rowsPanel);
        Grid.SetRow(_listHost, 1);
        Grid.SetRow(_emptyHost, 1);
        Grid.SetRow(_addRow.Element, 2);

        Children.Add(_listHost);
        Children.Add(_emptyHost);
        Children.Add(_addRow.Element);
    }

    /// <summary>What the widget is showing instead of, or as well as, a list.</summary>
    private enum Screen
    {
        Starting,
        NoAccount,
        AccountGone,
        NeedsSignIn,
        NoLists,
        ListsFailed,
        NoLocalList,
        Loading,
        Failed,
        Ready,
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _trailing;

    private bool IsLocal => _config.Source == TaskSource.Local;

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _ticker = ticker;
        _service = _widgetHost.Tasks;
        _local = _service?.Local;

        if (_service is not null)
        {
            _service.AccountsChanged += OnAccountsChanged;
            _service.Local.Changed += OnLocalChanged;
        }

        _config = config as TasksConfig ?? new TasksConfig();
        Strings.Changed += OnLanguage;
        Resubscribe();
        Rebind();
    }

    /// <summary>[S-1110] The fixed texts again, then the list redrawn from what is in hand (no fetch).</summary>
    private void OnLanguage(Locale locale)
    {
        _addRow.Label = TasksStr.WidgetsC_Tasks_AddTask.Text();
        ApplyPickerTexts();
        PublishTitle();
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as TasksConfig ?? new TasksConfig();
        Resubscribe();

        // [S-183] Only what changes the request re-binds; the rest redraws what is in hand. The
        // settings page edits this very config object in place, so the comparison is against what
        // the widget last bound to, never against "the previous config".
        if (BindKey.Of(_config) != _bound)
        {
            Rebind();
        }
        else
        {
            Render();
        }
    }

    /// <summary>The settings that decide which list is asked for, as they were when it was bound.</summary>
    private readonly record struct BindKey(TaskSource Source, string? AccountId, string? ListId, string? LocalListId, bool ShowCompleted)
    {
        public static BindKey Of(TasksConfig config)
            => new(config.Source, config.AccountId, config.ListId, config.LocalListId, config.ShowCompleted);
    }

    /// <summary>
    /// The card's menu: refresh. [S-1746] The layout and completed tasks shown or hidden are how the
    /// widget looks: they are in its settings only.
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        !IsLocal && _feed is not null ? [new WidgetAction(Glyphs.Refresh, KitFreshness.RefreshLabel(Strings.Current, Outcomes(), DateTimeOffset.Now), Refresh)] : [];

    // ---- which list --------------------------------------------------------------------------------

    /// <summary>
    /// Work out which list the config means and take hold of it. Re-run when anything that changes
    /// the request changes: the config's source, account, list or filter, or the accounts themselves.
    /// </summary>
    private void Rebind()
    {
        if (_detached)
        {
            return;
        }

        var generation = ++_bindGeneration;
        _bound = BindKey.Of(_config);
        ReleaseFeed();
        _localListId = null;
        _account = null;
        _resolvedListName = null;
        _listNames = new Dictionary<string, string>();
        _listsFailure = null;

        if (_service is null)
        {
            // Only reachable in a teardown race; a widget with no service still says so.
            Show(Screen.Starting);
            return;
        }

        if (IsLocal)
        {
            _localListId = _service.Local.Find(_config.LocalListId)?.Id;
            PublishTitle();
            Render();
            return;
        }

        var accounts = _service.Accounts;
        var named = !string.IsNullOrEmpty(_config.AccountId);
        _account = named ? _service.Find(_config.AccountId) : accounts.FirstOrDefault();

        if (_account is null)
        {
            // A named account that has gone is not quietly swapped for another one: the widget
            // would start showing somebody else's tasks without being asked.
            PublishTitle();
            Show(named && accounts.Count > 0 ? Screen.AccountGone : Screen.NoAccount);
            return;
        }

        if (_account.Problem is not null)
        {
            PublishTitle();
            Show(Screen.NeedsSignIn);
            return;
        }

        if (string.IsNullOrEmpty(_config.ListId))
        {
            Show(Screen.Loading);
            _ = ResolveDefaultListAsync(_account, generation);
            return;
        }

        Hold(_account.Id, _config.ListId);

        // The name may have changed at the provider since it was picked; Today wants every list's
        // name for its rows. Either way the lists are cached.
        _ = RefreshListNameAsync(_account.Id, _config.ListId, generation);
    }

    private async Task ResolveDefaultListAsync(TaskAccount account, int generation)
    {
        try
        {
            var lists = await _service!.ListsAsync(account.Id).ConfigureAwait(true);

            if (_detached || generation != _bindGeneration)
            {
                return;
            }

            if (TaskRules.DefaultList(lists) is not { } list)
            {
                Show(Screen.NoLists);
                return;
            }

            _resolvedListName = list.Name;
            Hold(account.Id, list.Id);
        }
        catch (AccountException ex)
        {
            if (_detached || generation != _bindGeneration)
            {
                return;
            }

            Log.Warn(Scope, $"could not read the task lists of {account.Id}", ex);
            _listsFailure = ex;
            Show(ex.Error == AccountError.SignInRequired ? Screen.NeedsSignIn : Screen.ListsFailed);
        }
    }

    private async Task RefreshListNameAsync(string accountId, string listId, int generation)
    {
        try
        {
            var lists = await _service!.ListsAsync(accountId).ConfigureAwait(true);

            if (_detached || generation != _bindGeneration)
            {
                return;
            }

            if (TaskRules.IsToday(listId))
            {
                _listNames = Names(lists);
                Render();
            }
            else if (lists.FirstOrDefault(l => l.Id == listId) is { } list)
            {
                _resolvedListName = list.Name;
                PublishTitle();
            }

            UpdatePicker();
        }
        catch (AccountException ex)
        {
            // The title keeps the name the list had when it was picked; the feed reports the failure.
            Log.Debug(Scope, $"could not refresh the list name: {ex.Message}");
        }
    }

    private void Hold(string accountId, string listId)
    {
        _feed = _service!.Acquire(accountId, listId, _config.ShowCompleted);
        _feed.Changed += OnFeedChanged;
        PublishTitle();

        // A feed another widget already filled is drawn as it is; its own tick keeps it fresh.
        if (!_feed.Loaded)
        {
            _service.Refresh(_feed, _config.RefreshInterval, force: true);
        }

        Render();
    }

    private void ReleaseFeed()
    {
        if (_feed is null)
        {
            return;
        }

        _feed.Changed -= OnFeedChanged;
        _service?.Release(_feed);
        _feed = null;
    }

    /// <summary>
    /// The ticker drives refreshes of an account list (the service skips one that is not due) and
    /// keeps "Today" and "Overdue" honest across midnight. A list on this PC has no clock in it.
    /// </summary>
    private void Resubscribe()
    {
        if (_detached)
        {
            return;
        }

        var interval = IsLocal ? TimeSpan.Zero : _config.RefreshInterval;

        if (interval == _subscribedInterval && (_subscription is not null || interval == TimeSpan.Zero))
        {
            return;
        }

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = interval;

        if (interval > TimeSpan.Zero)
        {
            _subscription = _ticker?.Subscribe(interval, OnTick);
        }
    }

    private void OnTick()
    {
        if (_detached)
        {
            return;
        }

        if (_feed is not null)
        {
            _service?.Refresh(_feed, _config.RefreshInterval, force: false);
        }

        Render();
    }

    private void Refresh()
    {
        if (_feed is not null)
        {
            _service?.Refresh(_feed, _config.RefreshInterval, force: true);
        }
        else
        {
            Rebind();
        }
    }

    /// <summary>[S-614] A sync or a rename may have renamed a list: the title and Today's row names follow.</summary>
    private void OnFeedChanged()
    {
        if (_feed?.Lists is { } lists && TaskRules.IsToday(_feed.ListId))
        {
            _listNames = Names(lists);
        }

        PublishTitle();
        Render();
    }

    private static Dictionary<string, string> Names(IReadOnlyList<TaskListInfo> lists)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var each in lists)
        {
            names.TryAdd(each.Id, each.Name);
        }

        return names;
    }

    private void OnAccountsChanged() => Rebind();

    private void OnLocalChanged()
    {
        if (IsLocal)
        {
            // A list deleted, created or renamed from settings or another widget.
            _localListId = _local?.Find(_config.LocalListId)?.Id;
            PublishTitle();
            Render();
        }
    }

    /// <summary>[S-403] The list's name as the title; the user's own label still wins in the card.</summary>
    private void PublishTitle()
    {
        string? title = null;

        if (IsLocal)
        {
            if (_local?.Find(_localListId) is { } list)
            {
                title = string.IsNullOrWhiteSpace(list.Name) ? TasksStr.WidgetsC_Tasks_UntitledList.Text() : list.Name;
            }
        }
        else if (TaskRules.IsToday(_config.ListId))
        {
            title = TasksStr.Todo_TodayList.Text();
        }
        else
        {
            // [S-614] The account's lists as the service holds them, a rename on its way included.
            title = ShownList()?.Name is { Length: > 0 } live ? live
                : _resolvedListName is { Length: > 0 } resolved ? resolved
                : _config.ListName is { Length: > 0 } picked ? picked
                : null;
        }

        _surface?.SetHeaderTitle(title);
    }

    // ---- what to draw --------------------------------------------------------------------------------

    /// <summary>The rows to draw, in the order to draw them, or null when there is no list to draw.</summary>
    private IReadOnlyList<TaskItem>? Rows(DateOnly today)
    {
        if (IsLocal)
        {
            if (_local?.Find(_localListId) is not { } list)
            {
                return null;
            }

            // A list on this PC is its own order.
            var items = list.Tasks.Select(t => LocalTaskLists.ToItem(t, list.Id)).ToList();
            return TaskRules.Sectioned(Visible(items, today));
        }

        return _feed is { Loaded: true } feed ? TaskRules.Sectioned(Visible(Arranged(feed, today), today)) : null;
    }

    /// <summary>[S-595] [S-611] [S-612] The one filter rule over a list already in its order.</summary>
    private IReadOnlyList<TaskItem> Visible(IReadOnlyList<TaskItem> tasks, DateOnly today)
        => TaskRules.Visible(tasks, _config.Filter(_feed?.IsToday == true && !IsLocal), today, TimeZoneInfo.Local, RegionFormat.Current.WeekStart);

    /// <summary>
    /// [S-598] [S-610] Every task of an account feed (Today's for Today) in the widget's order, hidden
    /// ones included: what the rows are filtered from, and what an order is written over.
    /// </summary>
    private IReadOnlyList<TaskItem> Arranged(ITaskFeed feed, DateOnly today)
    {
        var tasks = feed.IsToday ? TaskRules.Today(feed.Tasks, today, TimeZoneInfo.Local) : feed.Tasks;
        return TaskRules.ApplyOrder(tasks, TaskRules.CleanOrder(_config.TaskOrder), t => t.Id, _config.NewOnTop);
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        UpdateHeader();

        // One "today" per render, so a list drawn across midnight cannot disagree with itself.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var rows = Rows(today);

        if (rows is null)
        {
            Show(IsLocal
                ? Screen.NoLocalList
                : _feed is { Failure: not null } ? Screen.Failed
                : _feed is not null ? Screen.Loading
                : _screen);
            return;
        }

        _screen = Screen.Ready;
        _displayed = rows;
        var plan = Plan(rows.Count);
        BuildRows(rows, today, plan);

        var open = TaskRules.OpenCount(rows);
        _openCount = open > 0 ? open : null;
        UpdateHeader();

        if (rows.Count == 0)
        {
            ShowEmpty(EmptyListText(), action: null);
        }
        else
        {
            _emptyHost.Visibility = Visibility.Collapsed;
            _scroll.Visibility = Visibility.Visible;
        }

        FitAddRow(plan);
    }

    /// <summary>[S-780] The ladder for the slot the card gave this view, and the rows there are to show.</summary>
    private TaskLayoutPlan Plan(int count) => TaskLayout.Plan(
        _area,
        new TaskLayoutInput(count, CanAdd, _feed?.IsToday == true, _config.ShowDueDate, _config.Style, _config.EffectiveTitleLines),
        Fluent.RowHeight,
        Fluent.RowHeightCompact);

    /// <summary>[S-663] A new slot: re-lay the rows only when the ladder lands somewhere else.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        FitEmptyState();
        UpdatePicker();

        if (_screen == Screen.Ready)
        {
            if (Plan(_displayed.Count) != _drawnPlan)
            {
                Render();
            }
        }
        else
        {
            FitAddRow(Plan(0));
        }
    }

    /// <summary>The busy ring and the warning, which ride on the card's header.</summary>
    private void UpdateHeader()
    {
        var busy = _feed?.Busy == true || _screen == Screen.Loading;

        // [S-607] The one stale rule ([S-7042]): the widget's own note (a list on this PC that could
        // not be saved, a change the provider refused), then the failed refresh with when the list is from.
        var note = IsLocal ? _local?.Problem : _feed?.WriteProblem;
        string[] notes = note is null ? [] : [note];
        var lines = _screen == Screen.Ready ? [.. notes, .. KitFreshness.Lines(Strings.Current, Outcomes(), DateTimeOffset.Now)] : notes;

        _header.Show(lines, busy, offersAccounts: false, count: _screen == Screen.Ready ? _openCount : null);
        UpdatePicker();
    }

    /// <summary>[S-607] The account list's last refresh as the kit's outcome; none for a list on this PC.</summary>
    private IReadOnlyList<KitOutcome> Outcomes()
        => IsLocal || _feed is not { } feed ? [] : TaskFreshness.Outcomes(_account?.Label ?? string.Empty, feed.Loaded, feed.Failure, feed.FetchedAt);

    private string EmptyListText()
    {
        // [S-611] A filter that leaves nothing says so, rather than "all done".
        if (_config.Filter(today: false).Narrows)
        {
            return TasksStr.WidgetsC_Tasks_NoMatch.Text();
        }

        if (_feed?.IsToday == true)
        {
            return TasksStr.WidgetsC_Tasks_NothingDueToday.Text();
        }

        return (_config.ShowCompleted ? TasksStr.WidgetsC_Tasks_ListEmpty : TasksStr.WidgetsC_Tasks_AllDone).Text();
    }

    /// <summary>Say what is actually true, because every one of these needs a different action ([S-181]).</summary>
    private void Show(Screen screen)
    {
        _screen = screen;

        // No list, so nothing to rename or add to.
        CancelEdit();
        ClearRows();
        _displayed = [];
        _openCount = null;
        UpdateHeader();

        var who = _account?.Label;

        switch (screen)
        {
            case Screen.Starting:
                ShowEmpty(TasksStr.WidgetsC_OneDrive_Starting.Text(), null);
                break;

            case Screen.NoAccount:
                // [S-1862] The card's Set up button covers this; its settings page offers a local list.
                ShowEmpty(string.Empty, null);
                break;

            case Screen.AccountGone:
                ShowEmpty(TasksStr.WidgetsC_OneDrive_AccountGone.Text(), EmptyAction.Accounts);
                break;

            case Screen.NeedsSignIn:
                ShowEmpty(who is null ? TasksStr.WidgetsC_Tasks_ThisAccountNeedsSignIn.Text() : TasksStr.WidgetsC_OneDrive_AccountNeedsSignIn.Format(("account", who)), EmptyAction.Accounts);
                break;

            case Screen.NoLists:
                ShowEmpty(who is null ? TasksStr.WidgetsC_Tasks_NoListsThisAccount.Text() : TasksStr.WidgetsC_Tasks_NoLists.Format(("account", who)), null);
                break;

            case Screen.ListsFailed:
                ShowEmpty(Explain(_listsFailure), EmptyAction.Retry);
                break;

            case Screen.NoLocalList:
                ShowEmpty(TasksStr.WidgetsC_Tasks_PickLocal.Text(), EmptyAction.StartLocalList);
                break;

            case Screen.Loading:
                ShowEmpty(TasksStr.WidgetsC_Tasks_Loading.Text(), null);
                break;

            case Screen.Failed:
                ShowEmpty(Explain(_feed?.Failure), _feed?.Failure?.Error is AccountError.SignInRequired or AccountError.UnknownAccount
                    ? EmptyAction.Accounts
                    : EmptyAction.Retry);
                break;
        }

        FitAddRow(Plan(0));
    }

    /// <summary>A failure in the user's words: the network state, else what the provider said.</summary>
    private static string Explain(AccountException? failure) => failure?.Error switch
    {
        null => TasksStr.WidgetsC_Tasks_ReadFailed.Text(),
        AccountError.SignInRequired => TasksStr.WidgetsC_Tasks_ThisAccountNeedsSignIn.Text(),
        AccountError.Forbidden => TasksStr.WidgetsC_Tasks_Error_Forbidden.Text(),
        AccountError.NotFound => TasksStr.WidgetsC_Tasks_Error_NotFound.Text(),
        AccountError.UnknownAccount => TasksStr.WidgetsC_OneDrive_AccountGone.Text(),
        _ => FetchState.Message(failure.ToFetchStatus()) is { Length: > 0 } message ? message : TasksStr.WidgetsC_Tasks_ReadFailed.Text(),
    };

    private enum EmptyAction
    {
        Accounts,
        Retry,
        StartLocalList,
    }

    private void ShowEmpty(string text, EmptyAction? action)
    {
        _emptyText.Text = text;
        _emptyHost.Visibility = Visibility.Visible;
        _scroll.Visibility = _displayed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        _emptyAction.Tag = action;
        _emptyAction.Content = action switch
        {
            EmptyAction.Accounts => TasksStr.WidgetsC_OneDrive_OpenAccounts.Text(),
            EmptyAction.Retry => TasksStr.Connect_TryAgain.Text(),
            EmptyAction.StartLocalList => TasksStr.WidgetsC_Tasks_StartList.Text(),
            _ => null,
        };

        FitEmptyState();
    }

    private void OnEmptyActionClicked(object sender, RoutedEventArgs args)
    {
        switch ((sender as FrameworkElement)?.Tag)
        {
            case EmptyAction.Accounts:
                if (!_widgetHost.ShowAccounts())
                {
                    Log.Info(Scope, "no settings window here to show the Accounts page in");
                }

                break;

            case EmptyAction.Retry:
                Refresh();
                break;

            case EmptyAction.StartLocalList when _service is not null:
                // [S-604] The list it had if that still exists, else the first, else a new one.
                _config.Source = TaskSource.Local;
                _config.LocalListId = _service.Local.Ensure(_config.LocalListId, TasksStr.Todo_LocalLists_DefaultName.Text());
                _surface?.RequestSave();
                Resubscribe();
                Rebind();
                break;
        }
    }

    /// <summary>The link shows when there is one and room for it; the sentence gets the rest.</summary>
    private void FitEmptyState()
    {
        var height = _area.IsMeasured ? _area.Height : 0;
        var link = _emptyAction.Tag is EmptyAction && (height <= 0 || height >= LinkNeedsHeight);
        _emptyAction.Visibility = link ? Visibility.Visible : Visibility.Collapsed;
        _empty.MaxHeight = height > 0 ? Math.Max(0, height - (link ? 36 : 0)) : double.PositiveInfinity;
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        CancelDrag();
        CancelEdit();
        DetachPicker();
        _rowFocused = false;
        _detached = true;
        _bindGeneration++;
        Strings.Changed -= OnLanguage;

        _emptyAction.Click -= OnEmptyActionClicked;

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        ReleaseFeed();

        if (_service is not null)
        {
            _service.AccountsChanged -= OnAccountsChanged;
            _service.Local.Changed -= OnLocalChanged;
            _service = null;
        }

        _local = null;

        DetachAddRow();
        _ticker = null;
        ClearRows();
        _surface?.RequestTextInput(false);
    }
}
