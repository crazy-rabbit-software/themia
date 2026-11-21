using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;

namespace Themia.Widget.WeatherNow;

/// <summary>
/// [S-15021] The sky now on the icon face (<see cref="IconView"/>): the condition's glyph (<see cref="WeatherGlyph"/>, [S-733])
/// filling the card, the condition in words its tooltip; with no reading the weather's glyph drawn unavailable, why its
/// tooltip ([S-15002]); a reading kept through a failed refresh stays and the status's problem glyph says so.
/// </summary>
internal sealed partial class WeatherSkyView : IconView
{
    private readonly WeatherNowFeed _feed;

    public WeatherSkyView(IWidgetHost host)
        : base(host, WeatherNowKinds.Sky)
    {
        _feed = new WeatherNowFeed(host, Render);
    }

    /// <summary>[S-730] Refresh.</summary>
    protected override IReadOnlyList<WidgetAction> OwnActions => _feed.Actions;

    protected override void OnAttach(Ticker ticker, WidgetConfig config) => _feed.Attach(ticker, config);

    protected override void OnApply(WidgetConfig config) => _feed.Apply(config);

    protected override void OnDetach() => _feed.Detach();

    private void Render() => Present(Shown(_feed.Reading, _feed.Why, _feed.Problems));

    /// <summary>[S-15021] The sky for <paramref name="reading"/>; with none, the weather's glyph unavailable, why its tooltip.</summary>
    internal static IconShown Shown(CurrentWeather? reading, string why, IReadOnlyList<string> problems)
        => reading is null
            ? IconShown.None(Glyphs.Weather, why) with { Name = WeatherNowKinds.Sky.Label.Text() }
            : new IconShown(WeatherGlyph.Of(WeatherReport.Look(reading.Code))) { Name = WeatherReport.Describe(reading.Code), Problems = problems };
}
