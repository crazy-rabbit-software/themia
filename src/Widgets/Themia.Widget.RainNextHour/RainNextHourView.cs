using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.RainNextHour;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5120] [S-11140] [S-6754] Rain in the next two hours at a place, on the family's tokens and the one chart
/// ([S-6750]): the plot the card's edge to edge, a column per quarter hour on a fixed scale; over its top one sentence
/// ("Rain in 15 min") on a Title line at the inset and the intensity, total and chance the Body line under it; at its
/// foot the window's two ends over the columns ([S-5126], [S-16436]); the same at every size in its range
/// (<see cref="RainLayout"/>). A dry window or one too short to speak for the hour is the same face, its sentence the
/// answer and every quarter its empty track; no nowcast yet keeps it too, "--" its sentence and why the caption
/// ([S-16437]). The sentence is re-read from the held nowcast every minute, so "in 30 min" counts down between fetches.
/// The card is one item, lit under the pointer while a click opens the forecast ([S-16435]).
/// </summary>
internal sealed partial class RainNextHourView : Grid, IWidgetView, IWidgetOpens,
    IWidgetContextActions, IWidgetTopEdge, IWidgetSurfaceAware
{
    /// <summary>[S-5700] [S-6754] The plot is drawn to the card's top edge; the sentence keeps the inset over it.</summary>
    public bool OwnsTopEdge => true;

    /// <inheritdoc />
    public CardOpen? Opens => RainNextHourModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    /// <summary>[S-7064] The status's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitStatus _status;
    private readonly TextBlock _headline = Family.Text(FamilyType.Title);
    private readonly TextBlock _caption = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly StackPanel _top = new();

    /// <summary>[S-1113] Time runs left to right in every language, as on the stock chart.</summary>
    private readonly Chart _chart = new() { FlowDirection = FlowDirection.LeftToRight };

    private RainNextHourConfig _config = new();
    private readonly SkyFeed<RainInterest, RainNowcast> _feed;

    /// <summary>Debug renders pin the clock so the fixture's window stays where it was drawn.</summary>
    /// <summary>[S-11850] A sample's fixed moment; null on the desktop.</summary>
    private DateTimeOffset? _pinnedNow;

    /// <summary>[S-16435] The card lit under the pointer while a click opens something (WR-13).</summary>
    private readonly OpensLight _opensLight;
    private IWidgetSurface? _surface;

    public RainNextHourView(IWidgetHost host)
    {
        _status = new KitStatus(host);
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Rain, host.Weather.Show),
            () => _config.Interest(),
            Render);
        // [S-16437] The sentence's capitals on the card's inset (WR-1), not its Title line's box (they stood 19.3 down).
        _top.Margin = new Thickness(FamilySpace.Inset, RainLayout.SentenceTop, FamilySpace.Inset, 0);
        _top.VerticalAlignment = VerticalAlignment.Top;
        _headline.TextWrapping = TextWrapping.NoWrap;
        _headline.TextTrimming = TextTrimming.CharacterEllipsis;
        _caption.TextTrimming = TextTrimming.CharacterEllipsis;
        _top.Children.Add(_headline);
        _top.Children.Add(_caption);

        // [S-6754] The plot first, the card's whole box; the sentence over its top.
        Children.Add(_chart);
        Children.Add(_top);
        _status.Place(this);
        _opensLight = new OpensLight(this, () => _surface?.EditMode != true && Opens is not null);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as RainNextHourConfig ?? new RainNextHourConfig();

        // The sentence counts down with the clock; a minute is the finest it changes.
        _feed.Attach(ticker, TimeSpan.FromMinutes(1));
        _opensLight.Attach();
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as RainNextHourConfig ?? new RainNextHourConfig();
        _feed.Reload();
    }

    /// <summary>
    /// [S-6753] The window's chart is always drawn (owner, 2026-11-19: "even if there is no rain in the next two hours
    /// ... it should show the default values in the graph"): no nowcast, a dry window or one too short is the chart at
    /// its foot's hairline with its sentence over it, never a Status row in its place.
    /// </summary>
    private void Render()
    {
        var now = _pinnedNow ?? DateTimeOffset.Now;
        var outlook = _feed.Held is { Value: { } cast } ? RainRules.Outlook(cast, now) : null;
        if (_feed.Held is { Value: not null } held)
        {
            _status.Show(OnlineParts.Problems(held), busy: false);
        }
        else
        {
            _status.Clear();
        }

        // [S-16437] One face in every state (WR-10): the answer's sentence and its caption, a dry hour's too; with no nowcast
        // the sentence "--" and why the caption (not set up says nothing more: the card's Set up row says it).
        var warns = outlook is not null && RainLayout.Warns(outlook.Headline);
        _headline.Text = outlook is not null ? RainText.Headline(outlook) : RainLayout.NoAnswer;
        _headline.Foreground = Family.Brush(outlook is not null ? ColourRole.TextPrimary : ColourRole.TextDisabled);
        _caption.Text = outlook is not null ? RainText.Caption(outlook) : OnlineParts.Caption(_feed.Status);

        var start = (outlook is { Steps.Count: > 0 } ? outlook.Steps[0].Start : now).ToLocalTime();
        var ends = new ChartEnds(RainNextHourStr.Online_Rain_Now.Text(), LocaleFormat.Time(TimeOnly.FromDateTime(start.AddHours(2).DateTime)));

        // [S-5125] A column per quarter hour on a fixed scale; a dry one none.
        var shares = Enumerable.Range(0, RainRules.WindowSteps)
            .Select(i => warns && i < outlook!.Steps.Count ? RainRules.BarShare(outlook.Steps[i].Millimetres) : 0)
            .ToList();
        _chart.Show(size => RainLayout.Chart(shares, ends, size.Width));
    }

    public void Detach()
    {
        _opensLight.Detach();
        _feed.Detach();
    }

}
