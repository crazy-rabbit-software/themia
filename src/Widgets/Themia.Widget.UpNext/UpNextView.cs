using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.UpNext;

/// <summary>
/// [S-11408] The meetings after the next one ([S-2160]): a one-line Row each (the title the name, the
/// start the value), as many as its rows, the rest a scroll away under the fade. It reads the same feed
/// as Next meeting (<see cref="MeetingFeed"/>: the same pick, the same events), so the two agree on
/// which meeting is next; it never fetches on its own (a 5-minute poll and the accounts' change event).
/// A click on a row opens that meeting in its calendar ([S-1268]).
/// </summary>
internal sealed partial class UpNextView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetContextActions
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private readonly IWidgetHost _widgetHost;
    private readonly KitStatePanel _state;
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroll;
    private readonly List<RowBlock> _rows = [];
    private readonly MeetingFeed _feed;
    private ScrollFade? _fade;
    private IWidgetSurface? _surface;
    private IDisposable? _poll;
    private IDisposable? _tick;
    private UpNextConfig _config = new();
    private bool _detached = true;

    /// <summary>[S-2050] The clock the rows read: the real one, or a preview's sample moment.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    public UpNextView(IWidgetHost host)
    {
        _widgetHost = host;
        _state = new(Glyphs.Calendar, host) { Visibility = Visibility.Collapsed };
        _feed = new MeetingFeed(() => _config);
        _feed.Changed += Render;
        _scroll = new ScrollViewer
        {
            Content = _list,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };

        _list.Background = Family.Brush(ColourRole.Clear);
        _list.Tapped += OnTapped;
        FollowLinks();
        Children.Add(_scroll);
        Children.Add(_state);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as UpNextConfig ?? new UpNextConfig();
        _feed.Start();
        _feed.Use(_widgetHost.Office is { } source ? new OfficeCalendarData(source) : null);
        _fade ??= new ScrollFade(_scroll, _list);
        Strings.Changed += OnLanguage;
        _links.Join(_surface?.Links);

        // [S-588] The poll fires once on subscribing: the first load.
        _poll = ticker.Subscribe(Poll, () => _ = _feed.RefreshAsync(force: false));
        _tick = ticker.Subscribe(Minute, Render);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as UpNextConfig ?? new UpNextConfig();
        Render();
        _ = _feed.RefreshAsync(force: false);
    }


    /// <summary>Refresh, as every calendar widget has.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = _feed.RefreshAsync(force: true))];

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _links.Leave();
        _poll?.Dispose();
        _poll = null;
        _tick?.Dispose();
        _tick = null;
        _feed.Stop();
        _fade?.Dispose();
        _fade = null;
        _list.Tapped -= OnTapped;
        _state.Detach();
    }

    private void OnLanguage(Locale locale) => Render();

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        _scroll.Visibility = Visibility.Collapsed;

        switch (_feed.Screen)
        {
            case MeetingScreen.Loading:
                _state.Show(Str.Common_Loading.Text(), offerAccounts: false);
                return;

            case MeetingScreen.NoAccounts:
                // [S-1862] The card shows its Set up row over this.
                _state.Hide();
                return;

            case MeetingScreen.AccountsGone:
                _state.Show(SyncProblems.Message(SyncProblem.NoAccount, SyncSubject.Calendar, Strings.Current), offerAccounts: true, problem: true);
                return;

            case MeetingScreen.Problem:
                _state.Show(_feed.ProblemText(), _feed.ProblemOffersAccounts, problem: true);
                return;
        }

        var now = _clock();
        var zone = TimeZoneInfo.Local;
        var locale = Strings.Current;
        var after = _feed.After(now, zone);

        if (after.Count == 0)
        {
            _state.Show(UpNextStr.UpNext_None.Text(), offerAccounts: false);
            return;
        }

        _state.Hide();

        while (_rows.Count < after.Count)
        {
            var row = new RowBlock(RowKind.List, lead: 0) { Lines = 1 };
            _rows.Add(row);
            _list.Children.Add(row);
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            row.Visibility = i < after.Count ? Visibility.Visible : Visibility.Collapsed;
            if (i >= after.Count)
            {
                row.Tag = null;
                continue;
            }

            var item = after[i];
            row.Tag = item;
            row.Show(string.IsNullOrWhiteSpace(item.Subject) ? Str.Calendar_NoTitle.Text() : item.Subject, NextMeetingRules.Start(locale, item, now, zone));
            ToolTipService.SetToolTip(row, $"{(string.IsNullOrWhiteSpace(item.Subject) ? Str.Calendar_NoTitle.Text() : item.Subject)}{Environment.NewLine}{NextMeetingRules.Range(locale, item, zone)}");
        }

        // [S-2440] As tall as the rows its config states; the rest a scroll away.
        var fit = Math.Max(1, _config.RowsShown.Rows);
        _scroll.Height = after.Count > fit ? fit * FamilyShape.Row1 : double.NaN;
        _scroll.Visibility = Visibility.Visible;
        _fade?.Refresh();
        LightRows();
    }

    /// <summary>[S-1268] A click on a row opens that meeting in its calendar.</summary>
    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_surface?.IsPreview == true || _surface?.EditMode == true || RowItem(args.OriginalSource as DependencyObject) is not { } item)
        {
            return;
        }

        ProviderId? provider = _feed.Providers.TryGetValue(item.AccountId, out var known) ? known : null;
        if (CalendarLinks.Open(item, provider, provider == HostProviders.Microsoft && _widgetHost.Launcher.OutlookInstalled) is { } link)
        {
            // [S-2004] The row's own click: the card does not open anything over it.
            args.Handled = true;
            _widgetHost.Launcher.OpenCalendarLink(link);
        }
    }

    /// <summary>The meeting whose row a click landed on.</summary>
    private static CalendarEvent? RowItem(DependencyObject? source)
    {
        for (var node = source; node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is RowBlock { Tag: CalendarEvent item })
            {
                return item;
            }
        }

        return null;
    }
}
