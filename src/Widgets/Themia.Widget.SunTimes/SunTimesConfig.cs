using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Widget.SunTimes;

/// <summary>[S-11115] What one sun times card remembers: its place (the sky's, [S-3101]; a new one Zurich) and its rows, which and in what order.</summary>
public sealed class SunTimesConfig : SkyPlaceConfig
{
    /// <summary>[S-11115] The rows it shows, in order: the card's height follows the count ([S-11116]).</summary>
    public List<SunTimeKind> Times { get; set; } = [.. SunTimeRules.Defaults];

    protected override void RepairOwn(List<string> repairs)
    {
        var (times, changed) = SkyFacts.Repair<SunTimeKind>(Times, SunTimeRules.All, SunTimeRules.Defaults);
        Times = times;
        if (changed)
        {
            repairs.Add("sun times: the rows were empty or held one twice or one it does not have; repaired");
        }
    }

    public override WidgetConfig Clone() => CopyPlaceTo(new SunTimesConfig { Times = [.. Times] });
}
