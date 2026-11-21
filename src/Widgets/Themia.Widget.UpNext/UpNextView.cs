using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.UpNext;

/// <summary>
/// [S-11408] The meetings after the next one ([S-2160]) as a kit list ([S-7074]): a one-line row each (the title the
/// name, the start the value). It reads the same feed as Next meeting (<see cref="MeetingFeed"/>, the office feed:
/// the same pick, the same events), so the two agree on which meeting is next; it never fetches on its own (a 5-minute
/// poll and the accounts' change event). A click on a row opens that meeting in its calendar ([S-1268], [S-7075]).
/// </summary>
internal sealed partial class UpNextView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private readonly IWidgetHost _widgetHost;
    private readonly KitBinding _binding;
    private readonly MeetingSource _source;
    private readonly MeetingFeed _feed;
    private IWidgetSurface? _surface;
    private IDisposable? _poll;
    private UpNextConfig _config = new();
    private bool _attached;

    /// <summary>[S-2050] The clock the rows read: the real one, or a preview's sample moment.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    public UpNextView(IWidgetHost host)
    {
        _widgetHost = host;
        _feed = new MeetingFeed(() => _config);
        _source = new MeetingSource(_feed, () => _clock());
        _binding = new KitBinding(host, UpNextSpec.Spec, Glyphs.Calendar, UpNextSpec.Actions, Run)
        {
            Decorate = LinkRow,
            Redrawing = ForgetRows,
        };

        Children.Add(_binding.List);
        Children.Add(_binding.State);
        _binding.Status.Place(this);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as UpNextConfig ?? new UpNextConfig();
        _attached = true;
        _feed.Start();
        _feed.Use(_widgetHost.Office is { } source ? new OfficeCalendarData(source) : null);
        _links.Join(_surface?.Links);

        // [S-588] The poll fires once on subscribing: the first load; the minute redraws the starts.
        _poll = ticker.Subscribe(Poll, () => _ = _feed.RefreshAsync(force: false));
        _binding.Attach(ticker, _source, _config, Minute);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as UpNextConfig ?? new UpNextConfig();
        if (_attached)
        {
            _binding.Apply(_source, _config, Minute, show: false);
            _ = _feed.RefreshAsync(force: false);
        }
    }

    public void ApplySize(WidgetArea area) => _binding.Size(area);

    public bool HandleKey(WidgetKey key) => _binding.HandleKey(key);

    /// <summary>Refresh, with the age of what is on screen, as every calendar widget has.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _binding.CardActions;

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _links.Leave();
        _poll?.Dispose();
        _poll = null;
        _feed.Stop();
        _binding.Dispose();
    }

    /// <summary>[S-1268] A row's open: that meeting in its calendar; a preview and edit mode open nothing.</summary>
    private void Run(KitItem item, KitAction action)
    {
        if (action.Id != UpNextSpec.OpenId || _surface?.IsPreview == true || _surface?.EditMode == true || _source.MeetingOf(item.Id) is not { } meeting)
        {
            return;
        }

        ProviderId? provider = _feed.Providers.TryGetValue(meeting.AccountId, out var known) ? known : null;
        if (CalendarLinks.Open(meeting, provider, provider == HostProviders.Microsoft && _widgetHost.Launcher.OutlookInstalled) is { } link)
        {
            _widgetHost.Launcher.OpenCalendarLink(link);
        }
    }

    /// <summary>
    /// [S-11408] The meeting feed as a kit source: the meetings after the one shown, the feed's screen where it is not
    /// the list ([S-7077]), its stale accounts as the outcomes.
    /// </summary>
    private sealed class MeetingSource : IKitSource
    {
        private readonly MeetingFeed _feed;
        private readonly Func<DateTimeOffset> _clock;
        private IReadOnlyList<CalendarEvent> _after = [];

        public MeetingSource(MeetingFeed feed, Func<DateTimeOffset> clock)
        {
            _feed = feed;
            _clock = clock;
            feed.Changed += () => Changed?.Invoke();
        }

        public event Action? Changed;

        public bool Busy => _feed.Busy;

        public void Show(bool refresh)
        {
            if (refresh)
            {
                _ = _feed.RefreshAsync(force: true);
            }
        }

        public KitRead Read()
        {
            var locale = Strings.Current;
            var screen = _feed.Screen(locale);
            switch (screen.Kind)
            {
                case KitScreenKind.NoAccounts:
                    // [S-1862] The card's Set up row says so over the face.
                    return new KitRead([], [], Accounts: []);

                case KitScreenKind.Loading:
                    return new KitRead([], [new KitOutcome(null, FetchStatus.Loading)]);

                case not KitScreenKind.Items:
                    return new KitRead([], [], Stated: new KitStated(screen.Sentence) { OffersAccounts = screen.OffersAccounts });
            }

            var now = _clock();
            var zone = TimeZoneInfo.Local;
            _after = _feed.After(now, zone);
            // Nothing after the meeting shown is the spec's sentence, whatever an account's last sync said.
            KitOutcome[] outcomes = _after.Count > 0 ? [.. _feed.Outcomes, new KitOutcome(null, FetchStatus.Ok)] : [new KitOutcome(null, FetchStatus.Ok)];
            return new KitRead([.. _after.Select(meeting => UpNextSpec.Item(meeting, now, zone, locale))], outcomes);
        }

        public CalendarEvent? MeetingOf(string id) => _after.FirstOrDefault(m => UpNextSpec.Id(m) == id);
    }
}
