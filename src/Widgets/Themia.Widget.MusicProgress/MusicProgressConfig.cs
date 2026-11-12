using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-11330] The progress atom: how far into what Windows is playing it is, the seek bar between the
/// elapsed time and the total. What it shows is fixed; its one option is when it hides ([S-1464]).
/// </summary>
public sealed class MusicProgressConfig : WidgetConfig, IHidesWhenIdle
{
    /// <summary>[S-1464] When the widget paints nothing at all; edit mode always shows it.</summary>
    public MusicHideWhenIdle HideWhenIdle { get; set; } = MusicHideWhenIdle.Never;

    /// <summary>An unknown hide-when-idle mode reads Never.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(HideWhenIdle))
        {
            return [];
        }

        HideWhenIdle = MusicHideWhenIdle.Never;
        return ["track progress had an unknown hide-when-idle mode"];
    }

    public override WidgetConfig Clone() => new MusicProgressConfig { HideWhenIdle = HideWhenIdle };
}
