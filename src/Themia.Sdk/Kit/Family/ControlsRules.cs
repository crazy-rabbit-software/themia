namespace Themia.Sdk.Kit;

/// <summary>[S-6716] What a control in a Controls row is.</summary>
public enum ControlKind
{
    /// <summary>The one main action (Start, Play, Join, Empty): a text button, or a 28 icon button (Play), in the accent.</summary>
    Main,

    /// <summary>A standard text button.</summary>
    Button,

    /// <summary>A 28 × 28 icon button with a 16 glyph (transport, steppers).</summary>
    Icon,

    /// <summary>A slider: the rail is the track (4, in Track), the value in the accent; it takes the room left.</summary>
    Slider,

    /// <summary>An on/off switch (Windows' toggle switch, 40 wide): the widget's own state (an alarm that is set). It stays, as the main action does.</summary>
    Switch,
}

/// <summary>[S-6716] One control and the width it needs (a slider's is its least).</summary>
public readonly record struct ControlPart(ControlKind Kind, double Width);

/// <summary>
/// [S-6716] The Controls block: what a person does to a widget, not what it shows. One row of
/// buttons (28 tall, 8 apart) at the card's bottom inset, never mid-content: at most one main action
/// in the accent (a text button, or a 28 icon button such as Play), the rest standard buttons and 28
/// icon buttons, an optional slider that takes the room left and an optional on/off switch. On a
/// narrow card the icon buttons give way from the last, then the standard buttons, then the slider;
/// the main action and the switch stay.
/// </summary>
public static class ControlsRules
{
    /// <summary>The gap between two controls.</summary>
    public const double Gap = FamilySpace.S2;

    /// <summary>A slider's least width: less than this and it gives way.</summary>
    public const double MinSlider = 80;

    /// <summary>A switch's width: Windows' toggle switch track (the Tile's switch look draws the same).</summary>
    public const double SwitchWidth = TileRules.SwitchWidth;

    /// <summary>[S-14283] A switch's track's height: its ink, centred in the template's own height (Windows' template's <c>OuterBorder</c>; drawn, the template's own is read).</summary>
    public const double SwitchTrack = 20;

    /// <summary>
    /// [S-14283] A horizontal slider's thumb with its ring: its ink, centred on the rail (Windows' template: the thumb's 18 and
    /// its border standing 2 outside it each way; drawn, the template's own is read).
    /// </summary>
    public const double SliderThumb = 22;

    /// <summary>
    /// [S-12833] The text size a Controls row gives way at in a face drawn small (<c>FaceFloor</c>): its words' (Body);
    /// a 16 glyph on an icon button reads like 12, and a slider or a switch is no smaller to use than a word is to read.
    /// </summary>
    public static double GivesWayAt => FamilyType.Body.FontSize;

    /// <summary>[S-12833] The row as a face's piece: <paramref name="width"/> wide, 8 under what stands over it, giving way at Body.</summary>
    public static Themia.Core.Widgets.FaceRow Row(double width)
        => new([Themia.Core.Widgets.FacePiece.Box(width, FamilyShape.Button, GivesWayAt)], GapOver: Gap);

    /// <summary>Throws unless <paramref name="parts"/> has at most one main action: a row of two accent buttons says nothing is the main one.</summary>
    public static void Check(IReadOnlyList<ControlPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count(p => p.Kind == ControlKind.Main) > 1)
        {
            throw new ArgumentException("a Controls row has one main action at most", nameof(parts));
        }
    }

    /// <summary>Which of <paramref name="parts"/> show in a row <paramref name="width"/> wide: one flag per part. Unmeasured, all.</summary>
    public static bool[] Shown(double width, IReadOnlyList<ControlPart> parts)
    {
        Check(parts);
        var shown = Enumerable.Repeat(true, parts.Count).ToArray();
        if (!double.IsFinite(width) || width <= 0)
        {
            return shown;
        }

        foreach (var index in FoldOrder(parts))
        {
            if (Needs(parts, shown) <= width)
            {
                break;
            }

            shown[index] = false;
        }

        return shown;
    }

    /// <summary>The width the shown parts need, gaps included (a slider at its least).</summary>
    public static double Needs(IReadOnlyList<ControlPart> parts, IReadOnlyList<bool> shown)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(shown);

        var total = 0.0;
        var count = 0;
        for (var i = 0; i < parts.Count; i++)
        {
            if (shown[i])
            {
                total += parts[i].Kind == ControlKind.Slider ? Math.Max(MinSlider, parts[i].Width) : parts[i].Width;
                count++;
            }
        }

        return total + (Math.Max(0, count - 1) * Gap);
    }

    private static IEnumerable<int> FoldOrder(IReadOnlyList<ControlPart> parts)
    {
        foreach (var kind in new[] { ControlKind.Icon, ControlKind.Button, ControlKind.Slider })
        {
            for (var i = parts.Count - 1; i >= 0; i--)
            {
                if (parts[i].Kind == kind)
                {
                    yield return i;
                }
            }
        }
    }
}
