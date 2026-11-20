using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-11320] The cover atom: the cover of what Windows is playing, alone, as large as its card, and when it hides
/// ([S-1464], as the track atom's).
/// </summary>
public sealed class MusicCoverConfig : WidgetConfig, IHidesWhenIdle
{
    /// <summary>[S-1464] When the widget paints nothing at all; edit mode always shows it.</summary>
    public MusicHideWhenIdle HideWhenIdle { get; set; } = MusicHideWhenIdle.Never;

    /// <summary>An unknown hide-when-idle mode reads Never.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        if (!Enum.IsDefined(HideWhenIdle))
        {
            repairs.Add("album art had an unknown hide-when-idle mode");
            HideWhenIdle = MusicHideWhenIdle.Never;
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new MusicCoverConfig { HideWhenIdle = HideWhenIdle };
}
