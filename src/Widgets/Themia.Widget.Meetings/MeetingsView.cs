using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Meetings;

/// <summary>
/// [S-16961] The meetings of the chosen calendars on the item base (<see cref="ItemKit"/>, [S-16958]): the kit's binding keeps
/// the feed's screens (no account, loading, a problem, nothing in this time) and the stale rule, the items are
/// <see cref="MeetingItems"/>'s in the config's arrangement. It reads the meeting widgets' one feed for the days its reach
/// asks (back for the past ones) and redraws every minute. A click opens a meeting in its calendar, Join joins it.
/// </summary>
internal sealed partial class MeetingsView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys, IWidgetGrounds
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    /// <summary>The Open action's id: the view opens the meeting in its calendar.</summary>
    private const string OpenId = "open";

    /// <summary>The Join action's id.</summary>
    private const string JoinId = "join";

    private readonly IWidgetHost _host;
    private readonly KitBinding _binding;
    private readonly MeetingSource _source;
    private readonly MeetingFeed _feed;
    private IWidgetSurface? _surface;
    private IDisposable? _poll;
    private MeetingsConfig _config = new();
    private bool _attached;

    /// <summary>[S-2050] The clock the items read: the real one, or a preview's sample moment.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    public MeetingsView(IWidgetHost host)
    {
        _host = host;
        _feed = new MeetingFeed(() => _config, Window);
        _source = new MeetingSource(this);
        var kit = new ItemKit(host, Map, MeetingItems.Parts, () => _config, action: true)
        {
            Acted = item => Join(item),
            CanOpen = () => _surface is not { EditMode: true } && !_host.IsPreview,
        };
        kit.List.PartTitle = part => MeetingItems.PartName(part, Strings.Current);
        kit.List.GroundsChanged += () => GroundsChanged?.Invoke();
        _binding = new KitBinding(host, Spec(), Glyphs.Calendar, Actions, Run)
        {
            Arranged = kit,
            Decorate = LinkRow,
            Redrawing = ForgetRows,
        };

        Children.Add(_binding.Face);
        Children.Add(_binding.State);
        _binding.Status.Place(this);
    }

    /// <inheritdoc />
    public event Action? GroundsChanged;

    /// <summary>[S-16640] With the gap on, every meeting's box.</summary>
    public IReadOnlyList<Themia.Core.Geometry.DipRect>? Grounds => _binding.Arranged!.List.ViewGrounds;

    /// <summary>Refresh, with the age of what is on screen, as every calendar widget has.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _binding.CardActions;

    public bool WantsKeys => _binding.WantsKeys;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as MeetingsConfig ?? new MeetingsConfig();
        _attached = true;
        _feed.Start();
        if (_surface is not { IsPreview: true })
        {
            _feed.Use(_host.Office is { } source ? new OfficeCalendarData(source) : null);
        }

        _links.Join(_surface?.Links);

        // [S-588] The poll fires once on subscribing: the first load; the minute redraws the starts.
        _poll = ticker.Subscribe(Poll, () => _ = _feed.RefreshAsync(force: false));
        _binding.Attach(ticker, _source, _config, Minute);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MeetingsConfig ?? new MeetingsConfig();
        if (_attached)
        {
            _binding.Apply(_source, _config, Minute, show: false);
            _ = _feed.RefreshAsync(force: false);
        }
    }

    public void ApplySize(WidgetArea area) => _binding.Size(area);

    public bool HandleKey(WidgetKey key) => _binding.HandleKey(key);

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

    /// <summary>[S-16962] The days the feed asks for: from yesterday (one still under way) ahead, back as far as the reach for the past.</summary>
    private (DateOnly First, DateOnly Last) Window(DateOnly today)
    {
        var days = _config.EffectiveDays;
        return _config.When == MeetingWhen.Future ? (today.AddDays(-1), today.AddDays(days)) : (today.AddDays(1 - days), today.AddDays(days));
    }

    /// <summary>[S-16961] The kit's spec: its one-line rows are the item base's; nothing in this time says which.</summary>
    private KitSpec Spec() => new(
        KitRowLook.Line,
        KitShow.When,
        static () => Str.Calendar_NoTitle.Text(),
        () => _config.When == MeetingWhen.Past ? MeetingsStr.Meetings_None_Past.Text() : _config.AfterNext ? MeetingsStr.UpNext_None.Text() : MeetingsStr.Meeting_None.Text(),
        KitTime.None);

    /// <summary>A kit item as its meeting's item.</summary>
    private Themia.Sdk.Items.Item Map(KitItem item, DateTimeOffset now)
        => _source.MeetingOf(item.Id) is { } meeting
            ? MeetingItems.Item(meeting, _config, now, TimeZoneInfo.Local, Strings.Current)
            : MeetingItems.Nothing(string.Empty);

    /// <summary>[S-1268] A meeting's menu: Open in its calendar, Join where it has a link.</summary>
    private IReadOnlyList<KitAction> Actions(KitItem item)
    {
        var actions = new List<KitAction> { new(KitActionKind.Open, Str.Common_Open.Text(), KitIcon.Event, Id: OpenId) };
        if (_source.MeetingOf(item.Id) is { JoinUrl: not null } meeting && meeting.End > _clock())
        {
            actions.Add(new KitAction(KitActionKind.Change, MeetingsStr.Meeting_Join.Text(), KitIcon.App, Id: JoinId));
        }

        return actions;
    }

    private void Run(KitItem item, KitAction action)
    {
        if (_host.IsPreview || _surface?.EditMode == true || _source.MeetingOf(item.Id) is not { } meeting)
        {
            return;
        }

        if (action.Id == JoinId)
        {
            Join(item);
            return;
        }

        ProviderId? provider = _feed.Providers.TryGetValue(meeting.AccountId, out var known) ? known : null;
        if (CalendarLinks.Open(meeting, provider, provider == HostProviders.Microsoft && _host.Launcher.OutlookInstalled) is { } link)
        {
            _host.Launcher.OpenCalendarLink(link);
        }
    }

    /// <summary>[S-16948] Join: the meeting's link through the host's launcher; a render joins nothing.</summary>
    private void Join(KitItem item)
    {
        if (!_host.IsPreview && _source.MeetingOf(item.Id)?.JoinUrl is { } link)
        {
            _host.Launcher.OpenMeetingLink(link);
        }
    }

    /// <summary>
    /// [S-16961] The meeting feed as a kit source: the meetings the config selects, the feed's screen where it is not the list
    /// ([S-7077]), its stale accounts as the outcomes.
    /// </summary>
    private sealed class MeetingSource : IKitSource
    {
        private readonly MeetingsView _view;
        private IReadOnlyList<CalendarEvent> _shown = [];

        public MeetingSource(MeetingsView view)
        {
            _view = view;
            view._feed.Changed += () => Changed?.Invoke();
        }

        public event Action? Changed;

        public bool Busy => _view._feed.Busy;

        public void Show(bool refresh)
        {
            if (refresh)
            {
                _ = _view._feed.RefreshAsync(force: true);
            }
        }

        public KitRead Read()
        {
            var feed = _view._feed;
            var locale = Strings.Current;
            var screen = feed.Screen(locale);
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

            if (_view._surface is not { IsPreview: true })
            {
                MeetingsSeen.Last = feed.Events;
            }

            var now = _view._clock();
            _shown = MeetingItems.Select(feed.Events, _view._config, now, TimeZoneInfo.Local);

            // Nothing in this time is the spec's sentence, whatever an account's last sync said.
            KitOutcome[] outcomes = _shown.Count > 0 ? [.. feed.Outcomes, new KitOutcome(null, FetchStatus.Ok)] : [new KitOutcome(null, FetchStatus.Ok)];
            return new KitRead([.. _shown.Select(m => new KitItem(MeetingItems.Id(m), m.Subject))], outcomes);
        }

        public CalendarEvent? MeetingOf(string id) => _shown.FirstOrDefault(m => MeetingItems.Id(m) == id);
    }
}
