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
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;

namespace Themia.Widget.Meetings;

/// <summary>
/// [S-16960] One meeting on the item base's face (<see cref="ItemFace"/>, [S-16957]): the next one or the one picked, its subject,
/// when, its place and organiser, Join at its end where it has a join link (owner, 2026-11-23: "the meetings should get a
/// join button on the right side for example instead of the percent thingy"). It reads the meeting widgets' one feed
/// (<see cref="MeetingFeed"/> over <see cref="IWidgetHost.Office"/>, on the calendars' 5-minute poll and the accounts' change
/// event) and redraws every minute. A click on the meeting opens it in its calendar, Join joins it; with no meeting the face
/// stays, "--" and why (WR-10), and the card opens the calendar. The card lights as the one meeting, for the same meeting
/// pointed at elsewhere on the view too ([S-4825]).
/// </summary>
internal sealed partial class MeetingView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private readonly IWidgetHost _host;
    private readonly ItemFace _face;
    private readonly MeetingFeed _feed;
    private readonly OpensLight _opensLight;
    private readonly WidgetLinks _links = new();
    private LinkCard<EventLink>? _card;
    private IWidgetSurface? _surface;
    private MeetingConfig _config = new();
    private IDisposable? _poll;
    private IDisposable? _tick;
    private CalendarEvent? _shown;
    private bool _attached;

    /// <summary>[S-2050] The clock the meeting is read at: the real one, or a preview's sample moment.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    public MeetingView(IWidgetHost host)
    {
        _host = host;
        _feed = new MeetingFeed(() => _config);
        _feed.Changed += Render;
        _face = new ItemFace(host);
        _face.List.Opened = _ => OpenMeeting();
        _face.List.Acted = _ => Join();
        _face.List.CanOpen = () => _shown is not null && _surface is not { EditMode: true } && !_host.IsPreview;
        Children.Add(_face);
        _opensLight = new OpensLight(this, () => _surface?.EditMode != true);
    }

    /// <inheritdoc />
    public CardOpen? Opens => MeetingModule.Opens(_config.AccountIds, _host.AccountsWith(HostFeatures.Calendar));

    private LinkCard<EventLink> Card => _card ??= new LinkCard<EventLink>(_links, LinkTopics.HoveredEvent, this);

    /// <summary>Refresh, as every calendar widget has.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = _feed.RefreshAsync(force: true))];

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as MeetingConfig ?? new MeetingConfig();
        _attached = true;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _links.Join(_surface?.Links);
        Card.Publishes(this);
        _opensLight.Attach();
        _feed.Start();
        if (_surface is not { IsPreview: true })
        {
            _feed.Use(_host.Office is { } source ? new OfficeCalendarData(source) : null);
        }

        // [S-588] The poll fires once on subscribing: the first load; the minute redraws how long until it.
        _poll = ticker.Subscribe(Poll, () => _ = _feed.RefreshAsync(force: false));
        _tick = ticker.Subscribe(Minute, Render);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MeetingConfig ?? new MeetingConfig();
        Render();
        _ = _feed.RefreshAsync(force: false);
    }

    public void ApplySize(WidgetArea area) => _face.Size(area);

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        Strings.Changed -= OnLanguage;
        _poll?.Dispose();
        _poll = null;
        _tick?.Dispose();
        _tick = null;
        _feed.Stop();
        Card.Release();
        _links.Leave();
        _opensLight.Detach();
    }

    private void OnLanguage(Locale locale) => Render();

    /// <summary>[S-16960] The meeting picked (else the next) on the face; none: the face with why (WR-10).</summary>
    private void Render()
    {
        if (!_attached)
        {
            return;
        }

        var locale = Strings.Current;
        var now = _clock();
        var screen = _feed.Screen(locale);
        if (_surface is not { IsPreview: true } && screen.Kind == KitScreenKind.Items)
        {
            MeetingsSeen.Last = _feed.Events;
        }

        _shown = screen.Kind == KitScreenKind.Items ? MeetingItems.Pick(_feed.Events, _config, now) : null;
        var item = _shown is { } meeting
            ? MeetingItems.Item(meeting, _config, now, TimeZoneInfo.Local, locale)
            : MeetingItems.Nothing(screen.Kind == KitScreenKind.Items ? MeetingsStr.Meeting_None.Text(locale) : screen.Sentence);
        Card.Key = _shown is { } key ? CalendarShare.Key(key) : null;
        _face.Show(item, new ItemDraw(_config.HiddenParts, ItemShape.Of(MeetingItems.Parts, _config.HiddenParts, action: true)));
    }

    /// <summary>[S-16948] Join: the meeting's link through the host's launcher; a render joins nothing.</summary>
    private void Join()
    {
        if (!_host.IsPreview && _shown?.JoinUrl is { } link)
        {
            _host.Launcher.OpenMeetingLink(link);
        }
    }

    /// <summary>[S-1268] A click on the meeting opens it in its calendar, as a calendar widget's does.</summary>
    private void OpenMeeting()
    {
        if (_shown is not { } item)
        {
            return;
        }

        ProviderId? provider = _feed.Providers.TryGetValue(item.AccountId, out var known) ? known : null;
        if (CalendarLinks.Open(item, provider, provider == HostProviders.Microsoft && _host.Launcher.OutlookInstalled) is { } link)
        {
            _host.Launcher.OpenCalendarLink(link);
        }
    }
}
