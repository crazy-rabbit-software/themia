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

namespace Themia.Widget.AirQualityGraph;

/// <summary>
/// [S-10400] The air graph's view: the place's hourly air answer through the SDK's <c>SkyFeed</c> ([S-11171]), drawn
/// by the weather graphs' one panel ([S-12706]) with <see cref="AirQualityGraphRules"/>.
/// </summary>
internal sealed partial class AirQualityGraphView : HourGraphPanel, IWidgetView, IWidgetOpens, IWidgetContextActions
{
    private readonly SkyFeed<AirHoursInterest, AirHours> _feed;
    private AirQualityGraphConfig _config = new();

    public AirQualityGraphView(IWidgetHost host)
        : base(AirQualityGraphModule.Glyph)
    {
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.AirHours, host.Weather.Show),
            () => _config.AirInterest(),
            Redraw);
    }

    /// <inheritdoc />
    public CardOpen? Opens => AirQualityGraphModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as AirQualityGraphConfig ?? new AirQualityGraphConfig();
        _feed.Attach(ticker, HourGraphPanel.Cadence);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as AirQualityGraphConfig ?? new AirQualityGraphConfig();
        _feed.Reload();
    }

    public void Detach() => _feed.Detach();

    private void Redraw()
        => Render(
            _feed.Held,
            _feed.Status,
            (answer, now) => AirQualityGraphRules.Model(answer, _config, now, Strings.Current, TimeZoneInfo.Local),
            Str.WeatherGraph_Empty.Text(),
            _feed.Refresh);
}
