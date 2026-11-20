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
/// The next few days, from open-meteo's daily block, in one of four styles ([S-6163]), drawn from the
/// family's blocks: the days as rows (the Row block with its range bar), side by side as columns, the
/// highs and lows as a temperature curve (the Chart block), or as cells in a grid shaped to the tile
/// ([S-6171]).
/// <para>
/// [S-11145] The days are the config's in every style and at every size; each style's plan
/// (<see cref="WeatherForecastLayout"/>, <see cref="ForecastCurve"/>, <see cref="ForecastGrid"/>) only
/// shares the box among them: a bar or a chart longer, a column wider.
/// </para>
/// <para>
/// The bar on each day is scaled against the whole forecast rather than that day's own range: a
/// week that runs 12° to 14° every day should draw seven short marks in the same place.
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

    /// <summary>[S-12813] Without a forecast: its days with nothing read, drawn in the style ("--").</summary>
    private IReadOnlyList<ForecastDay>? _unread;
    private WidgetArea _area;

    /// <summary>What is on screen: the style, its plan and the day the labels were computed for.</summary>
    private (ForecastStyle Style, object Plan, DateOnly Today, bool Precipitation)? _built;
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

    /// <summary>
    /// [S-730] Refresh. The style is chosen on the selection's page ([S-6007]).
    /// </summary>
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
        // another want, so the forecast in hand goes with it; the precipitation column or the style redraws what is held.
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
            // [S-12813] The days stay in the style, "--" and the unknown sky; why is their tooltip (not set up is the
            // card's Set up row, [S-1862]); a status glyph would stand over the first day's "--".
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

    /// <summary>Pick the style's plan for the current area, and rebuild only if it changed.</summary>
    private void Lay()
    {
        if (_detached || (_feed.Held?.Value?.Days is { Count: > 0 } held ? held : _unread) is not { Count: > 0 } days || !_area.IsMeasured)
        {
            return;
        }

        var style = _config.Style;
        object plan = style switch
        {
            ForecastStyle.Columns => WeatherForecastLayout.Columns(_area, _config),
            ForecastStyle.Curve => ForecastCurve.Plan(_area, _config),
            ForecastStyle.Grid => ForecastGrid.Plan(_area, _config),
            _ => WeatherForecastLayout.Rows(_area, _config),
        };

        // One clock reading for the whole build, so a forecast drawn across midnight cannot label
        // two different days "Today".
        var today = DateOnly.FromDateTime(DateTime.Now);
        var key = (style, plan, today, _config.ShowPrecipitation);

        if (_built is { } built && built.Style == key.style && Equals(built.Plan, key.plan)
            && built.Today == key.today && built.Precipitation == key.ShowPrecipitation)
        {
            return;
        }

        ClearBody();
        _built = key;

        // The bars scale against the days shown, not the ones left out for room.
        var shown = plan switch
        {
            ForecastRowsPlan r => r.Days,
            ForecastColumnsPlan c => c.Days,
            _ => days.Count,
        };
        var (coldest, warmest) = Extremes([.. days.Take(shown)]);
        var range = new Range(coldest, warmest);

        _body.Children.Add(plan switch
        {
            ForecastColumnsPlan columns => BuildColumns(days, today, range, columns),
            ForecastCurvePlan curve => BuildCurve(days, today, curve),
            ForecastGridPlan cells => BuildGrid(days, today, cells),
            ForecastRowsPlan rows => BuildRows(days, today, range, rows),
            _ => new Grid(),
        });
    }

    /// <summary>The coldest low and warmest high across the forecast, which every bar scales against.</summary>
    private readonly record struct Range(double Coldest, double Warmest);

    private static (double Coldest, double Warmest) Extremes(IReadOnlyList<ForecastDay> days)
    {
        var coldest = double.MaxValue;
        var warmest = double.MinValue;

        foreach (var day in days)
        {
            foreach (var value in new[] { day.Low, day.High })
            {
                if (value is { } v && double.IsFinite(v))
                {
                    coldest = Math.Min(coldest, v);
                    warmest = Math.Max(warmest, v);
                }
            }
        }

        // Every day missing both numbers: the bar spans the row, which RangeBar does for no span.
        return coldest <= warmest ? (coldest, warmest) : (0, 1);
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
