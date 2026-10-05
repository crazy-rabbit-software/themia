using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-606] Files on a Microsoft account's OneDrive: the ones touched lately, or a folder walked into
/// from the top of the drive.
/// <para>
/// Read-only, and nothing is downloaded: a file opens as its OneDrive page in the browser, through
/// <see cref="ShellLauncher"/>'s allow list like every other launch in the app — on a double-click
/// by default, or a single click when the widget is set so ([S-1291]). Walking into a folder is a
/// single click either way: it only changes the widget.
/// </para>
/// <para>
/// [S-1290] It uses the whole tile: names alone on a small one, a second line of details on a
/// narrow tall one, and on anything wider one line per item with the details in columns — when,
/// how big, who, where — so a strip lists more items and a big tile has no empty half. As many
/// whole rows as fit are fetched (the count setting is a floor); the largest tile adds the drive's
/// quota under the list.
/// </para>
/// </summary>
internal sealed partial class OneDriveView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "onedrive-widget";

    /// <summary>Below this the link under an empty state is dropped; the sentence is worth more.</summary>
    private const double LinkNeedsHeight = 140;

    private static readonly TimeSpan CountDebounce = TimeSpan.FromMilliseconds(400);

    private readonly ProgressRing _busy = new();
    private readonly FontIcon _warning;
    private readonly StackPanel _trailing;
    private readonly Border _crumb;
    private readonly TextBlock _crumbText;
    private readonly Grid _columnHeader = new();
    private readonly StackPanel _list = new() { Spacing = 0 };
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _footer;
    private readonly Grid _emptyHost = new();
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();
    private readonly HyperlinkButton _emptyAction;
    private readonly List<IDisposable> _hovers = [];
    private readonly List<Border> _rows = [];
    private readonly IDisposable _crumbHover;
    private readonly DispatcherQueueTimer? _countTimer;

    private DriveService? _service;
    private IDisposable? _subscription;
    private ScrollFade? _fade;
    private OneDriveConfig _config = new();
    private (string? AccountId, DriveStart Start, int Count)? _bound;

    private IReadOnlyList<DriveFolder> _trail = [];
    private IReadOnlyList<DriveItem>? _items;
    private AccountException? _failure;
    private DriveAccount? _account;
    private DriveQuota? _quota;
    private string? _blocked;
    private int _generation;
    private bool _detached;

    private WidgetArea _area;

    /// <summary>What the rows on screen were built for, so a resize redraws only when an answer changes.</summary>
    private DriveShape _shape = new(new DriveFit(DriveRowMode.NameOnly, [], false), string.Empty, false, OneDriveConfig.MinFiles, false);

    public OneDriveView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _busy.Width = Fluent.IconSize;
        _busy.Height = Fluent.IconSize;
        _busy.Visibility = Visibility.Collapsed;

        _warning = Fluent.Icon(Glyphs.Warning, "SystemFillColorCautionBrush");
        _warning.Visibility = Visibility.Collapsed;

        _trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _warning, _busy },
        };

        // [S-609] The way back out of a folder: a row like any other, so it reaches both edges.
        _crumbText = Fluent.Text(FluentType.BodyStrong);
        _crumbText.TextTrimming = TextTrimming.CharacterEllipsis;
        _crumbText.VerticalAlignment = VerticalAlignment.Center;

        var back = Fluent.Icon(Glyphs.Back);
        back.Margin = new Thickness(0, 0, Fluent.Space3, 0);

        var crumbBody = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        Grid.SetColumn(_crumbText, 1);
        crumbBody.Children.Add(back);
        crumbBody.Children.Add(_crumbText);

        _crumb = Fluent.Row(crumbBody, compact: true);
        RowFocus.Prepare(_crumb);
        _crumb.Visibility = Visibility.Collapsed;
        _crumb.Tapped += OnCrumbTapped;
        _crumbHover = Fluent.Hover(_crumb);
        ToolTipService.SetToolTip(_crumb, Str.Common_Back.Text());
        Grid.SetRow(_crumb, 0);

        _columnHeader.Visibility = Visibility.Collapsed;
        Grid.SetRow(_columnHeader, 1);

        _scroll = new ScrollViewer
        {
            Padding = Fluent.ListPadding,
            Content = _list,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        Grid.SetRow(_scroll, 2);

        // [S-1294] The largest tile's quota line under the list.
        _footer = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        _footer.Margin = new Thickness(Fluent.Space4, 0, Fluent.Space4, Fluent.Space2);
        _footer.Visibility = Visibility.Collapsed;
        Grid.SetRow(_footer, 3);

        _empty = Fluent.EmptyState(Glyphs.OneDrive, _emptyText, out _);
        _emptyAction = new HyperlinkButton
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
            Visibility = Visibility.Collapsed,
        };
        _emptyAction.Click += OnEmptyActionClicked;

        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _emptyHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_empty, 1);
        Grid.SetRow(_emptyAction, 2);
        _emptyHost.Children.Add(_empty);
        _emptyHost.Children.Add(_emptyAction);
        _emptyHost.Visibility = Visibility.Collapsed;
        Grid.SetRow(_emptyHost, 1);
        Grid.SetRowSpan(_emptyHost, 3);

        Children.Add(_crumb);
        Children.Add(_columnHeader);
        Children.Add(_scroll);
        Children.Add(_footer);
        Children.Add(_emptyHost);

        _countTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();
        if (_countTimer is not null)
        {
            _countTimer.Interval = CountDebounce;
            _countTimer.IsRepeating = false;
            _countTimer.Tick += OnCountSettled;
        }
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _trailing;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _service = AppServices.Drive;
        _fade = new ScrollFade(_scroll, _list);

        if (_service is not null)
        {
            _service.AccountsChanged += OnAccountsChanged;
        }

        Strings.Changed += OnLanguage;

        // [S-608] A drive changes slowly; the shared ticker asks again every five minutes.
        _subscription = ticker.Subscribe(OneDriveConfig.RefreshInterval, OnTick);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as OneDriveConfig ?? new OneDriveConfig();
        Lay();

        // The settings page edits this very object in place, so the comparison is against what was
        // last loaded, never against "the previous config".
        var key = (Empty(_config.AccountId), _config.StartIn, _config.EffectiveFileCount);
        if (key == _bound)
        {
            // [S-1291] The click mode changes only how the rows answer.
            Render();
            return;
        }

        var onlyCount = _bound is { } bound && bound.AccountId == key.Item1 && bound.Start == key.StartIn;
        _bound = key;

        if (onlyCount && _countTimer is not null)
        {
            // A slider dragged across the range is one request when it settles, not one per step.
            _countTimer.Stop();
            _countTimer.Start();
            return;
        }

        // [S-609] A different account or start throws the folder trail away.
        _trail = [];
        _items = null;
        _quota = null;
        Load(TimeSpan.Zero);
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        FitEmptyState();

        var drawn = _shape;
        Lay();

        if (_shape.Fetch != drawn.Fetch || _shape.Quota != drawn.Quota)
        {
            // More rows fit (or fewer): ask for that many. A listing already held for the address
            // is still served from memory, so shrinking back costs nothing.
            Load(TimeSpan.FromMinutes(1));
        }
        else if (_shape != drawn)
        {
            Render();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            var actions = new List<WidgetAction> { new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Load(TimeSpan.Zero)) };

            if (_trail.Count > 0)
            {
                actions.Add(new WidgetAction(Glyphs.Home, (_config.StartIn == DriveStart.Root ? Str.WidgetsC_OneDrive_BackToMyFiles : Str.WidgetsC_OneDrive_BackToRecent).Text(), () =>
                {
                    _trail = [];
                    _items = null;
                    Lay();
                    Load(TimeSpan.Zero);
                }));
            }

            return actions;
        }
    }

    private static string? Empty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private void OnCountSettled(DispatcherQueueTimer sender, object args)
    {
        Lay();
        Load(TimeSpan.Zero);
    }

    private void OnTick() => Load(OneDriveConfig.RefreshInterval - TimeSpan.FromSeconds(30));

    private void OnAccountsChanged()
    {
        _items = null;
        _quota = null;
        Load(TimeSpan.Zero);
    }

    // ---- the ladder ----------------------------------------------------------------------------------

    /// <summary>
    /// [S-1290] What the tile holds, top to bottom: the way back (inside a folder), the column titles
    /// when the details are in columns and there is height, then as many whole rows as fit — which
    /// is also how many items to fetch, never fewer than the count setting. The quota line of the
    /// largest tile ([S-1294]) is not a rung: it sits under the list, and the fade covers what it hides.
    /// </summary>
    private void Lay()
    {
        var compact = _area.Height > 0 && _area.Height < Fluent.CompactBelow;
        var room = new Disclosure(_area);

        if (_trail.Count > 0)
        {
            room.Reserve(Fluent.RowHeightCompact);
        }

        var showLocation = _trail.Count == 0 && _config.StartIn == DriveStart.Recent;
        var fit = DriveLayout.Fit(TextWidth(_area.Width), room.Remaining, compact, showLocation);
        var header = fit.ColumnHeader && room.Take(FluentType.Caption.LineHeight + Fluent.Space1);
        var rowHeight = fit.Mode == DriveRowMode.Columns || compact ? Fluent.RowHeightCompact : Fluent.RowHeight;
        var rows = _area.IsMeasured ? room.TakeRows(rowHeight, OneDriveConfig.MaxFiles) : 0;

        _shape = new DriveShape(
            fit with { ColumnHeader = header },
            string.Join(",", fit.Columns.Select(c => c.Column)),
            compact,
            _config.FetchCount(rows),
            _area.HeightClass >= SizeClass.Huge);
    }

    /// <summary>The width a row's text shares: the row less its padding, the focus ring and the icon.</summary>
    private static double TextWidth(double width)
        => Math.Max(0, width - (2 * (Fluent.Space4 + RowFocus.Ring)) - Fluent.IconSize - Fluent.Space3);

    /// <summary>[S-1110] The fixed tooltip, then the place again from memory: every line on screen is re-read.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_crumb, Str.Common_Back.Text());
        Load(TimeSpan.FromMinutes(5));
    }

    // ---- loading -----------------------------------------------------------------------------------

    /// <summary>
    /// Show the current place: the innermost folder of the trail, else the configured start. A
    /// listing younger than <paramref name="maxAge"/> comes from the service's memory.
    /// </summary>
    private void Load(TimeSpan maxAge)
    {
        if (_detached)
        {
            return;
        }

        // [S-609] A reply for a place the widget has since left is dropped, never drawn (#110).
        var generation = ++_generation;
        _blocked = null;
        _account = null;

        if (_service is null)
        {
            _blocked = Str.WidgetsC_OneDrive_Starting.Text();
            Render();
            return;
        }

        var accounts = _service.Accounts;
        var named = !string.IsNullOrEmpty(_config.AccountId);
        _account = named ? _service.Find(_config.AccountId) : accounts.FirstOrDefault();

        if (_account is null)
        {
            // No account at all: the card's Set up button covers this ([S-1862]).
            _blocked = named && accounts.Count > 0
                ? Str.WidgetsC_OneDrive_AccountGone.Text()
                : string.Empty;
            Render();
            return;
        }

        if (_account.Problem is not null)
        {
            _blocked = Str.WidgetsC_OneDrive_AccountNeedsSignIn.Format(("account", _account.Label));
            Render();
            return;
        }

        var count = _shape.Fetch;
        _ = LoadAsync(_account.Id, DriveRules.Address(_trail, _config.StartIn, count), count, maxAge, generation);

        if (_shape.Quota)
        {
            _ = LoadQuotaAsync(_account.Id, generation);
        }
    }

    private async Task LoadAsync(string accountId, Uri address, int count, TimeSpan maxAge, int generation)
    {
        SetBusy(true);
        Render();

        try
        {
            var items = await _service!.ListAsync(accountId, address, count, maxAge).ConfigureAwait(true);

            if (_detached || generation != _generation)
            {
                return;
            }

            _items = items;
            _failure = null;
        }
        catch (AccountException ex)
        {
            if (_detached || generation != _generation)
            {
                return;
            }

            Log.Warn(Scope, $"could not list {address.AbsolutePath}", ex);
            _failure = ex;
        }
        catch (Exception ex)
        {
            if (_detached || generation != _generation)
            {
                return;
            }

            Log.Error(Scope, $"listing {address.AbsolutePath} failed unexpectedly", ex);
            _failure = new AccountException(AccountError.BadResponse, "The files could not be read.", 0, ex);
        }
        finally
        {
            if (generation == _generation)
            {
                SetBusy(false);
            }
        }

        Render();
    }

    /// <summary>[S-1294] The quota for the footer: an extra, so a failure only means no footer (the service logs it).</summary>
    private async Task LoadQuotaAsync(string accountId, int generation)
    {
        try
        {
            var quota = await _service!.QuotaAsync(accountId).ConfigureAwait(true);
            if (_detached || generation != _generation)
            {
                return;
            }

            _quota = quota;
            UpdateFooter();
        }
        catch (Exception ex)
        {
            // async from a resize or a tick: nothing above this may see an exception.
            Log.Error(Scope, "reading the quota failed unexpectedly", ex);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy.IsActive = busy;
        _busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- drawing -----------------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        ClearRows();

        _crumb.Visibility = _trail.Count > 0 && _blocked is null ? Visibility.Visible : Visibility.Collapsed;
        _crumbText.Text = _trail.Count > 0 ? _trail[^1].Name : string.Empty;
        Fluent.SetDensity(_crumb, true);

        var stale = _items is not null && _failure is not null;
        _warning.Visibility = stale ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_warning, stale ? Str.WidgetsC_OneDrive_CouldNotRefresh.Format(("reason", FetchState.Message(_failure!.ToFetchStatus()))) : null);

        if (_blocked is { } blocked)
        {
            ShowEmpty(blocked, _service is null || blocked.Length == 0 ? null : EmptyAction.Accounts);
            return;
        }

        if (_items is null)
        {
            if (_failure is { } failure)
            {
                ShowEmpty(Explain(failure), failure.Error is AccountError.SignInRequired or AccountError.Forbidden
                    ? EmptyAction.Accounts
                    : EmptyAction.Retry);
            }
            else
            {
                ShowEmpty(Str.WidgetsC_OneDrive_Loading.Text(), null);
            }

            return;
        }

        if (_items.Count == 0)
        {
            ShowEmpty((_trail.Count > 0 ? Str.WidgetsC_OneDrive_FolderEmpty : _config.StartIn == DriveStart.Root ? Str.WidgetsC_OneDrive_DriveEmpty : Str.WidgetsC_OneDrive_NoRecent).Text(), null);
            return;
        }

        _emptyHost.Visibility = Visibility.Collapsed;
        _scroll.Visibility = Visibility.Visible;

        BuildColumnHeader();
        UpdateFooter();

        var now = DateTimeOffset.Now;
        foreach (var item in _items)
        {
            _list.Children.Add(BuildRow(item, now));
        }

        _shown = _items;
        RestoreFocus();
        _fade?.Refresh();
    }

    private void UpdateFooter()
    {
        var text = _shape.Quota && _quota is { } quota && _items is { Count: > 0 } && _blocked is null ? DriveRules.QuotaText(quota) : null;
        _footer.Text = text ?? string.Empty;
        _footer.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string Explain(AccountException failure) => failure.Error switch
    {
        AccountError.SignInRequired => Str.WidgetsC_OneDrive_Error_SignInRequired.Text(),
        AccountError.Forbidden => Str.WidgetsC_OneDrive_Error_Forbidden.Text(),
        AccountError.NotFound => Str.WidgetsC_OneDrive_Error_NotFound.Text(),
        _ => FetchState.Message(failure.ToFetchStatus()) is { Length: > 0 } message ? message : Str.WidgetsC_OneDrive_ReadFailed.Text(),
    };

    private enum EmptyAction
    {
        Accounts,
        Retry,
    }

    private void ShowEmpty(string text, EmptyAction? action)
    {
        _scroll.Visibility = Visibility.Collapsed;
        _columnHeader.Visibility = Visibility.Collapsed;
        _footer.Visibility = Visibility.Collapsed;
        _emptyHost.Visibility = Visibility.Visible;
        _emptyText.Text = text;
        _emptyAction.Tag = action;
        _emptyAction.Content = action switch
        {
            EmptyAction.Accounts => Str.WidgetsC_OneDrive_OpenAccounts.Text(),
            EmptyAction.Retry => Str.Connect_TryAgain.Text(),
            _ => null,
        };
        _shown = [];
        FitEmptyState();
    }

    private void FitEmptyState()
    {
        var height = _area.Height;
        var link = _emptyAction.Tag is EmptyAction && (height <= 0 || height >= LinkNeedsHeight);
        _emptyAction.Visibility = link ? Visibility.Visible : Visibility.Collapsed;
        _empty.MaxHeight = height > 0 ? Math.Max(0, height - (link ? 36 : 0)) : double.PositiveInfinity;
    }

    // ---- clicks ------------------------------------------------------------------------------------------

    /// <summary>[S-609] Walk into a folder.</summary>
    private void EnterFolder(DriveItem folder)
    {
        _trail = DriveRules.Enter(_trail, folder);
        _items = null;
        ReleaseFocus();
        Lay();
        Load(TimeSpan.FromMinutes(1));
    }

    /// <summary>[S-609] One step back; past the first folder, back to the configured start.</summary>
    private void OnCrumbTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        _trail = DriveRules.Back(_trail);
        _items = null;
        ReleaseFocus();
        Lay();
        Load(TimeSpan.FromMinutes(1));
    }

    /// <summary>[S-606] A file opens as its OneDrive page, through the launcher's allow list.</summary>
    private static void OpenOnWeb(DriveItem item)
    {
        var outcome = ShellLauncher.Open(item.WebUrl);
        if (outcome != LaunchOutcome.Opened)
        {
            Log.Warn(Scope, $"could not open {item.Name}: {outcome}");
        }
    }

    private void OnEmptyActionClicked(object sender, RoutedEventArgs args)
    {
        if (_emptyAction.Tag is EmptyAction.Retry)
        {
            Load(TimeSpan.Zero);
        }
        else if (AppServices.ShowAccounts is { } show)
        {
            show();
        }
        else
        {
            Log.Info(Scope, "no settings window here to show the Accounts page in");
        }
    }

    private void ClearRows()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        foreach (var row in _rows)
        {
            row.Tapped -= OnRowTapped;
            row.DoubleTapped -= OnRowDoubleTapped;
            row.RightTapped -= OnRowRightTapped;
        }

        _hovers.Clear();
        _rows.Clear();
        _list.Children.Clear();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _generation++;
        Strings.Changed -= OnLanguage;

        _crumb.Tapped -= OnCrumbTapped;
        _crumbHover.Dispose();
        _emptyAction.Click -= OnEmptyActionClicked;

        if (_countTimer is not null)
        {
            _countTimer.Stop();
            _countTimer.Tick -= OnCountSettled;
        }

        _subscription?.Dispose();
        _subscription = null;

        _fade?.Dispose();
        _fade = null;

        if (_service is not null)
        {
            _service.AccountsChanged -= OnAccountsChanged;
            _service = null;
        }

        ClearRows();
    }

    /// <summary>The answers a layout pass gave; a resize that changes none of them redraws nothing.</summary>
    private readonly record struct DriveShape(DriveFit Fit, string Columns, bool Compact, int Fetch, bool Quota)
    {
        // DriveFit holds a list; two passes that chose the same columns are the same shape.
        public bool Equals(DriveShape other)
            => Fit.Mode == other.Fit.Mode && Fit.ColumnHeader == other.Fit.ColumnHeader && Columns == other.Columns
                && Compact == other.Compact && Fetch == other.Fetch && Quota == other.Quota;

        public override int GetHashCode() => HashCode.Combine(Fit.Mode, Fit.ColumnHeader, Columns, Compact, Fetch, Quota);
    }
}
