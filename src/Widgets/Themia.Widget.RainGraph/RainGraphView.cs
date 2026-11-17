using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.Widget.RainGraph;

/// <summary>
/// [S-10200] The rain graph's view: the hours look follows the place's hourly answer, the nowcast look the rain
/// widget's 15-minute answer (each through the SDK's <c>SkyFeed</c>, the other look's asking nothing), drawn by the
/// weather graphs' one panel ([S-12706]) with <see cref="RainGraphRules"/>.
/// </summary>
internal sealed partial class RainGraphView : HourGraphPanel, IWidgetView, IWidgetOpens, IWidgetContextActions
{
    private readonly SkyFeed<HoursInterest, WeatherHours> _hours;
    private readonly SkyFeed<RainInterest, RainNowcast> _nowcast;
    private RainGraphConfig _config = new();

    public RainGraphView(IWidgetHost host)
        : base(RainGraphModule.Glyph)
    {
        _hours = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Hours, host.Weather.Show),
            () => _config.Hourly(),
            Redraw);
        _nowcast = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Rain, host.Weather.Show),
            () => _config.Nowcast(),
            Redraw);
    }

    /// <inheritdoc />
    public CardOpen? Opens => RainGraphModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    public IReadOnlyList<WidgetAction> ContextActions =>
        !_config.HasLocation ? []
        : _config.Style == RainGraphStyle.Nowcast ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_nowcast.Held), _nowcast.Refresh)]
        : [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_hours.Held), _hours.Refresh)];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as RainGraphConfig ?? new RainGraphConfig();

        // The nowcast's window moves by the quarter hour; the hours' by the half hour.
        _hours.Attach(ticker, HourGraphPanel.Cadence);
        _nowcast.Attach(ticker, RainRules.Step);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as RainGraphConfig ?? new RainGraphConfig();
        _hours.Reload();
        _nowcast.Reload();
    }

    public void Detach()
    {
        _hours.Detach();
        _nowcast.Detach();
    }

    private void Redraw()
    {
        var locale = Strings.Current;
        if (_config.Style == RainGraphStyle.Nowcast)
        {
            Render(_nowcast.Held, _nowcast.Status, (answer, now) => RainGraphRules.Nowcast(answer, _config, now, locale, TimeZoneInfo.Local), Str.WeatherGraph_Empty.Text(), _nowcast.Refresh);
        }
        else
        {
            Render(_hours.Held, _hours.Status, (answer, now) => RainGraphRules.Hours(answer, _config, now, locale, TimeZoneInfo.Local), Str.WeatherGraph_Empty.Text(), _hours.Refresh);
        }
    }
}
