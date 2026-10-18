using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-3340] The screen-time donut: each app's share of the period, as a ring with the total in its hole.</summary>
public sealed class ScreenTimeDonutConfig : WidgetConfig, IShownSettings<ScreenTimeSetting>
{
    /// <summary>[S-6074] The ring's total reads against the daily limit; no app's limit shows on it.</summary>
    [JsonIgnore]
    public IReadOnlyList<ScreenTimeSetting> ShownSettings => [ScreenTimeSetting.DailyLimit];

    public ScreenTimePeriod Period { get; set; } = ScreenTimePeriod.Day;

    public override WidgetConfig Clone() => new ScreenTimeDonutConfig { Period = Period };
}
