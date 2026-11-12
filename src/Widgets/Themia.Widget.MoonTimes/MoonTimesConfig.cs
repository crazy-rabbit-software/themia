using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Widget.MoonTimes;

/// <summary>[S-11125] What one moon times card remembers: its place (the sky's, [S-3101]; a new one Zurich) and its rows, which and in what order.</summary>
public sealed class MoonTimesConfig : SkyPlaceConfig
{
    /// <summary>[S-11125] The rows it shows, in order: the card's height follows the count ([S-11126]).</summary>
    public List<MoonTimeKind> Times { get; set; } = [.. MoonTimeRules.Defaults];

    protected override void RepairOwn(List<string> repairs)
    {
        var (times, changed) = SkyFacts.Repair<MoonTimeKind>(Times, MoonTimeRules.All, MoonTimeRules.Defaults);
        Times = times;
        if (changed)
        {
            repairs.Add("moon times: the rows were empty or held one twice or one it does not have; repaired");
        }
    }

    public override WidgetConfig Clone() => CopyPlaceTo(new MoonTimesConfig { Times = [.. Times] });
}
