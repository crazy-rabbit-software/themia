using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-11320] The cover atom: the cover of what Windows is playing, alone, as a square of its Size
/// (Small 96, Medium 160, Large 240), and when it hides ([S-1464], as the track atom's).
/// </summary>
public sealed class MusicCoverConfig : WidgetConfig, IReadoutSized, IHidesWhenIdle
{
    /// <summary>[S-11320] How big the square is: Small, Medium, Large (96, 160, 240); the card is it.</summary>
    public ReadoutSize Size { get; set; } = ReadoutSizes.Default;

    /// <summary>[S-1464] When the widget paints nothing at all; edit mode always shows it.</summary>
    public MusicHideWhenIdle HideWhenIdle { get; set; } = MusicHideWhenIdle.Never;

    /// <summary>An unknown size reads Medium, an unknown hide-when-idle mode Never.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        if (!Enum.IsDefined(Size))
        {
            repairs.Add($"album art size {(int)Size} unknown; medium");
            Size = ReadoutSizes.Default;
        }

        if (!Enum.IsDefined(HideWhenIdle))
        {
            repairs.Add("album art had an unknown hide-when-idle mode");
            HideWhenIdle = MusicHideWhenIdle.Never;
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new MusicCoverConfig { Size = Size, HideWhenIdle = HideWhenIdle };
}
