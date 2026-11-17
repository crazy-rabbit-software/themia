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

namespace Themia.Widget.UvGraph;

/// <summary>
/// [S-10300] The graph's view: the place's hourly answer through the SDK's <c>SkyFeed</c> ([S-11171]), drawn by the
/// weather graphs' one panel ([S-12706]) with <see cref="UvGraphRules"/>. The same parts at every size of its range.
/// </summary>
internal sealed partial class UvGraphView : HourGraphPanel, IWidgetView, IWidgetOpens, IWidgetContextActions
{
    private readonly SkyFeed<HoursInterest, WeatherHours> _feed;
    private UvGraphConfig _config = new();

    public UvGraphView(IWidgetHost host)
        : base(UvGraphModule.Glyph)
    {
        _feed = new(
            () => new(h => host.Weather.Updated += h, h => host.Weather.Updated -= h, host.Weather.Key, host.Weather.Hours, host.Weather.Show),
            () => _config.Interest(),
            Redraw);
    }

    /// <inheritdoc />
    public CardOpen? Opens => UvGraphModule.Opens(_config, RegionFormat.Current.Temperature, Strings.Current);

    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.HasLocation ? [new WidgetAction(Glyphs.Refresh, OnlineParts.RefreshLabel(_feed.Held), _feed.Refresh)] : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as UvGraphConfig ?? new UvGraphConfig();
        _feed.Attach(ticker, HourGraphPanel.Cadence);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as UvGraphConfig ?? new UvGraphConfig();
        _feed.Reload();
    }

    public void Detach() => _feed.Detach();

    private void Redraw()
        => Render(
            _feed.Held,
            _feed.Status,
            (answer, now) => UvGraphRules.Model(answer, _config, now, Strings.Current, TimeZoneInfo.Local),
            Str.WeatherGraph_Empty.Text(),
            _feed.Refresh);
}
