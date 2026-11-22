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
/// [S-15023] The condition now on the text face (<see cref="TextView"/>): "Partly cloudy", alone on its line (the place is the
/// group's Header widget's to name, [S-12801]); with no reading "--" drawn unavailable, why ("Offline") its line; a
/// condition kept through a failed refresh stays and the status's problem glyph says so.
/// </summary>
internal sealed partial class WeatherConditionView : TextView
{
    private readonly WeatherNowFeed _feed;

    public WeatherConditionView(IWidgetHost host)
        : base(host, WeatherNowKinds.Condition)
    {
        _feed = new WeatherNowFeed(host, Render);
    }

    /// <summary>[S-730] Refresh.</summary>
    protected override IReadOnlyList<WidgetAction> OwnActions => _feed.Actions;

    protected override void OnAttach(Ticker ticker, WidgetConfig config) => _feed.Attach(ticker, config);

    protected override void OnApply(WidgetConfig config) => _feed.Apply(config);

    protected override void OnDetach() => _feed.Detach();

    private void Render() => Present(Shown(_feed.Reading, _feed.Why, _feed.Problems));

    /// <summary>
    /// [S-15023] The condition for <paramref name="reading"/>; with none, "--" unavailable (WR-10: the face kept, never its why
    /// in its place, [S-15713]), why its tooltip and its line ([S-15710]), a failure the status mark's ([S-15711]).
    /// </summary>
    internal static TextShown Shown(CurrentWeather? reading, string why, IReadOnlyList<string> problems)
        => reading is null
            ? TextShown.None(why) with { Problems = problems }
            : new TextShown(WeatherReport.Describe(reading.Code)) { Problems = problems };
}
