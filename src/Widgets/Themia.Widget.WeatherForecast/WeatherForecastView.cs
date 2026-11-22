using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.WeatherForecast;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6164] The forecast curve: the next few days from open-meteo's daily block, each day a column (its day, its sky's
/// glyph, its precipitation) over one chart of the highs and lows, drawn from the family's blocks.
/// <para>
/// [S-11145] The days are every one the card's width reaches (<see cref="ForecastCurve.Plan"/>); the chart takes the
/// height the other parts leave.
/// </para>
/// </summary>
internal sealed partial class WeatherForecastView : Grid, IWidgetView, IWidgetOpens,
    IWidgetSizeAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => WeatherForecastModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    private const string Scope = "forecast";

    /// <summary>[S-7064] The status's problem glyph ([S-7042]): the one stale rule, no age.</summary>
    private readonly KitStatus _mark;
    private readonly Grid _body = new();


    private WeatherForecastConfig _config = new();

    /// <summary>
    /// [S-11171] [S-2663] The forecast the background sync keeps, followed as every sky atom follows its answer: what it
    /// asks for (null without a place), the newest forecast (kept through a failure) or the state to say.
    /// </summary>
    private readonly SkyFeed<WeatherInterest, Forecast> _feed;

    /// <summary>[S-12813] Without a forecast: its days with nothing read, drawn as the curve's columns.</summary>
    private IReadOnlyList<ForecastDay>? _unread;
    private WidgetArea _area;

    /// <summary>What is on screen: the plan and the day the labels were computed for.</summary>
    private (ForecastCurvePlan Plan, DateOnly Today)? _built;
    private bool _detached;

    public WeatherForecastView(IWidgetHost host)
    {
        _mark = new KitStatus(host);

        // [S-2663] The forecast comes with the reading: its answer is heard under the reading's key too.
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Forecast, host.Weather.Show)
            {
                Hears = (want, key) => key == host.Weather.Key(want) || key == host.Weather.Key(want with { ForecastDays = null }),
            },
            () => WeatherInterests.For(_config),
            Render);
        _days = new LinkLights<DateLink>(_links, LinkTopics.HoveredDate, LinkTopics.SelectedDate);

        Children.Add(_body);
        _mark.Place(this);
    }

    /// <summary>[S-730] Refresh.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;

        // [S-1110] [S-2663] The forecast from the background sync and the texts in the app's language: the feed hears both.
        _feed.Attach(ticker, _config.TickInterval);
        _links.Join(_surface?.Links);

        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as WeatherForecastConfig ?? new WeatherForecastConfig();
        _config = next;

        // [S-2663] Always through the sync: it asks nothing for a forecast already held and fresh. Another place is
        // another want, so the forecast in hand goes with it; the precipitation line redraws what is held.
        _feed.Cadence(_config.TickInterval);
        _feed.Reload();
    }

    /// <summary>[S-663] The card's word on how much room the widget has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var held = _feed.Held;
        var days = held?.Value?.Days;

        if (days is null || days.Count == 0)
        {
            // [S-12813] The days stay as the curve's columns with the unknown sky and no line; why is their tooltip (not set
            // up is the card's Set up row, [S-1862]); a status glyph would stand over the first day's name.
            _unread = ForecastDefaults.Unread(DateOnly.FromDateTime(DateTime.Now), WeatherForecastConfig.MostDays);
            _body.Visibility = Visibility.Visible;
            _mark.Clear();
            ToolTipService.SetToolTip(_body, OnlineParts.Caption(_feed.Status) is { Length: > 0 } why ? why : null);
            _built = null;
            Lay();
            return;
        }

        _unread = null;
        ToolTipService.SetToolTip(_body, null);
        _body.Visibility = Visibility.Visible;
        _mark.Show(OnlineParts.Problems(held), busy: false);

        // New data always rebuilds; a tick with the same data rebuilds only when the day changed.
        _built = null;
        Lay();
    }

    /// <summary>The curve's plan for the current area, rebuilt only if it changed.</summary>
    private void Lay()
    {
        if (_detached || (_feed.Held?.Value?.Days is { Count: > 0 } held ? held : _unread) is not { Count: > 0 } days || !_area.IsMeasured)
        {
            return;
        }

        // The plan holds the precipitation switch, so a toggle rebuilds. One clock reading for the whole build, so a
        // forecast drawn across midnight cannot label two different days "Today".
        var key = (Plan: ForecastCurve.Plan(_area, _config), Today: DateOnly.FromDateTime(DateTime.Now));
        if (_built == key)
        {
            return;
        }

        ClearBody();
        _built = key;
        _body.Children.Add(BuildCurve(days, key.Today, key.Plan));
    }

    /// <summary>The same glyph the current-conditions widget draws for a condition. [S-652]</summary>
    private static string LookGlyph(WeatherLook look) => WeatherGlyph.Of(look);

    /// <summary>Drop the layout: the next one is built from the plan.</summary>
    private void ClearBody()
    {
        _body.Children.Clear();
        _days.Clear();
        _built = null;
    }

    public void Detach()
    {
        _detached = true;
        _links.Leave();

        ClearBody();
        _feed.Detach();
    }

}
