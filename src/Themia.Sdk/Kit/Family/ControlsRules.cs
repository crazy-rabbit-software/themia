namespace Themia.Sdk.Kit;

/// <summary>[S-6716] What a control in a Controls row is.</summary>
public enum ControlKind
{
    /// <summary>The one main action (Start, Play, Join, Empty): a text button in the accent.</summary>
    Main,

    /// <summary>A standard text button.</summary>
    Button,

    /// <summary>A 32 × 32 icon button with a 16 glyph (transport, steppers).</summary>
    Icon,

    /// <summary>A slider: the rail is the track (4, in Track), the value in the accent; it takes the room left.</summary>
    Slider,
}

/// <summary>[S-6716] One control and the width it needs (a slider's is its least).</summary>
public readonly record struct ControlPart(ControlKind Kind, double Width);

/// <summary>
/// [S-6716] The Controls block: what a person does to a widget, not what it shows. One row of
/// buttons (32 tall, 8 apart) at the card's bottom inset, never mid-content: at most one main action
/// in the accent, the rest standard buttons and 32 icon buttons, and an optional slider that takes
/// the room left. On a narrow card the icon buttons give way from the last, then the standard
/// buttons, then the slider; the main action stays.
/// </summary>
public static class ControlsRules
{
    /// <summary>The gap between two controls.</summary>
    public const double Gap = FamilySpace.S2;

    /// <summary>A slider's least width: less than this and it gives way.</summary>
    public const double MinSlider = 80;

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
