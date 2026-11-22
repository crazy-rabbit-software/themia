using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-11311] Now playing's track atom: the title, the artist and album line and the transport, read
/// from Windows' own media session. What it shows is fixed; its one option is when it hides ([S-1464]).
/// The cover and the progress are atoms of their own (<c>musicCover</c>, <c>musicProgress</c>).
/// </summary>
public sealed class MusicConfig : WidgetConfig, IHidesWhenIdle
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
        return ["Music widget had an unknown hide-when-idle mode"];
    }

    public override WidgetConfig Clone() => new MusicConfig
    {
        HideWhenIdle = HideWhenIdle,
    };
}
