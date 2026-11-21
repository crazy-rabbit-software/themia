using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.RainNextHour;

/// <summary>
/// [S-14940] The rain family's one connection to the place's nowcast for a text or metric view: the sync's answer through
/// the SDK's <see cref="SkyFeed{TWant, T}"/>, re-read every minute (the window moves with the clock, so "in 30 min" counts
/// down between fetches), and the window from now (<see cref="RainRules.Outlook"/>).
/// </summary>
internal sealed class RainFeed
{
    private readonly SkyFeed<RainInterest, RainNowcast> _feed;
    private RainConfig _config = new RainOutlookConfig();

    public RainFeed(IWidgetHost host, Action changed)
    {
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Rain, host.Weather.Show),
            () => _config.Interest(),
            changed);
    }

    /// <summary>[S-11850] A sample's fixed moment; null on the desktop.</summary>
    public DateTimeOffset? PinnedNow { get; set; }

    /// <summary>The card's config: its place.</summary>
    public RainConfig Config => _config;

    /// <summary>The answer held, if any.</summary>
    public Fetched<RainNowcast>? Held => _feed.Held;

    /// <summary>The window from now, or null with no answer.</summary>
    public RainOutlook? Outlook => _feed.Held is { Value: { } cast } ? RainRules.Outlook(cast, PinnedNow ?? DateTimeOffset.Now) : null;

    /// <summary>Why there is no answer, in words ("Set a place", "Offline").</summary>
    public string Why => OnlineParts.Caption(_feed.Status);

    /// <summary>[S-7042] The status glyph's problems: the answer kept through a failed refresh.</summary>
    public IReadOnlyList<string> Problems => _feed.Held is { } held ? OnlineParts.Problems(held) : [];

    /// <summary>[S-730] Refresh, on the card menu while it has a place.</summary>
    public IReadOnlyList<WidgetAction> Actions
        => _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as RainConfig ?? new RainOutlookConfig();
        _feed.Attach(ticker, TimeSpan.FromMinutes(1));
        _feed.Reload();
    }

    public void Apply(WidgetConfig config)
    {
        _config = config as RainConfig ?? new RainOutlookConfig();
        _feed.Reload();
    }

    public void Detach() => _feed.Detach();

    /// <summary>[S-11850] Hold <paramref name="answer"/> at the sample place and moment.</summary>
    public void Preview(RainConfig config, DateTimeOffset now, Fetched<RainNowcast>? answer, FetchStatus status)
    {
        _config = config;
        PinnedNow = now;
        _feed.Preview(answer, status);
    }
}

/// <summary>
/// [S-14942] The rain's sentence ("Rain in 15 min", "No rain in the next 2 hours") on the text face (<see cref="TextView"/>):
/// with no answer the face stays with why ("Set a place", "Offline"), drawn unavailable; a window too short to speak for
/// the hour says so, unavailable too; a sentence kept through a failed refresh stays and the status's problem glyph says so.
/// </summary>
internal sealed partial class RainOutlookView : TextView
{
    private readonly RainFeed _feed;

    public RainOutlookView(IWidgetHost host)
        : base(host, RainKinds.Outlook)
    {
        _feed = new RainFeed(host, Render);
    }

    /// <summary>[S-730] Refresh.</summary>
    protected override IReadOnlyList<WidgetAction> OwnActions => _feed.Actions;

    protected override void OnAttach(Ticker ticker, WidgetConfig config) => _feed.Attach(ticker, config);

    protected override void OnApply(WidgetConfig config) => _feed.Apply(config);

    protected override void OnDetach() => _feed.Detach();

    private void Render() => Present(Shown(_feed.Outlook, _feed.Why, _feed.Problems));

    /// <summary>[S-14942] The sentence for <paramref name="outlook"/>; with none, why, unavailable.</summary>
    internal static TextShown Shown(RainOutlook? outlook, string why, IReadOnlyList<string> problems)
        => outlook is null
            ? new TextShown(why, false) { Why = why }
            : new TextShown(RainText.Headline(outlook), outlook.Headline != RainHeadline.Outdated) { Problems = problems };
}

/// <summary>
/// [S-14943] The rain the next two hours hold at the card's place on the single metric's face (<see cref="MetricView"/>):
/// its millimetres; with no answer, or a window too short to speak for the hour, "--" and why its tooltip ([S-14602]).
/// </summary>
internal sealed partial class RainTotalView : MetricView
{
    private readonly RainFeed _feed;

    public RainTotalView(IWidgetHost host)
        : base(host, RainKinds.Total)
    {
        _feed = new RainFeed(host, Render);
    }

    /// <summary>[S-730] Refresh.</summary>
    protected override IReadOnlyList<WidgetAction> OwnActions => _feed.Actions;

    protected override void OnAttach(Ticker ticker, WidgetConfig config) => _feed.Attach(ticker, config);

    protected override void OnApply(WidgetConfig config) => _feed.Apply(config);

    protected override void OnDetach() => _feed.Detach();

    private void Render()
    {
        if (_feed.Outlook is not { } outlook || _feed.Held is not { } held)
        {
            Present(new MetricShown(MetricValue.Unread) { Why = _feed.Why });
            return;
        }

        var value = RainText.Total(Strings.Current, outlook);
        Present(new MetricShown(value)
        {
            Reading = held.At.UtcTicks,
            Problems = _feed.Problems,
            Why = value.Available ? null : RainText.Headline(outlook),
        });
    }
}
