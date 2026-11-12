using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-11500] The screen-time reading: the total of a day or a rolling week or month, with the line
/// under it. Like every screen-time config it holds no usage, hidden apps or limits ([S-3322]).
/// </summary>
public sealed class ScreenTimeConfig : WidgetConfig, IShownSettings<ScreenTimeSetting>
{
    /// <summary>[S-6074] The reading sums the day (or a window's average) against the daily limit.</summary>
    [JsonIgnore]
    public IReadOnlyList<ScreenTimeSetting> ShownSettings => [ScreenTimeSetting.DailyLimit];

    /// <summary>[S-3321] Day, Week (a new one's, as the chart under it in the group) or Month.</summary>
    public ScreenTimePeriod Period { get; set; } = ScreenTimePeriod.Week;

    public override WidgetConfig Clone() => new ScreenTimeConfig { Period = Period };
}
