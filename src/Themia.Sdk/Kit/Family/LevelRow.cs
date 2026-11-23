using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-17184] [S-17225] A level set by a slider as one row (owner, 2026-11-23: "brightness slider should be moved to the quick
/// settings and should have a row design and not columnar design"; 2026-11-24: "volume slider should go into the quick settings
/// too"), as Windows' quick settings draw it: in the card's side insets its glyph (16), 8, the level (Body, tabular figures,
/// held to "100%" so the slider never moves), 12, the slider across what is left; the row centred in the card, one height: the
/// inset, the slider's thumb (its ink, taller than a Body line) and the inset. The Brightness and Volume widgets' one row, so
/// the two read as a pair. Pure.
/// </summary>
public static class LevelRow
{
    /// <summary>The level's widest case: its column is held to it.</summary>
    public const string Worst = "100%";

    /// <summary>The level's column: "100%" in Body, estimated, whole DIPs.</summary>
    public static double LevelWidth => Math.Ceiling(HeroFit.Ems(Worst) * FamilyType.Body.FontSize);

    /// <summary>The row's one height: the inset, the slider's thumb or a Body line (the taller), the inset.</summary>
    public static double Height => (2 * FamilySpace.Inset) + Math.Max(FamilyType.BodyLine, ControlsRules.SliderThumb);

    /// <summary>The narrowest row: the insets, the glyph, its gap, the level, 12 and the slider's least travel.</summary>
    public static double MinWidth => Math.Ceiling((2 * FamilySpace.Inset) + FamilyShape.Icon + FamilySpace.GlyphGap + LevelWidth + FamilySpace.S3 + ControlsRules.MinSlider);

    /// <summary>A new row: wide and short, as Windows' quick settings row.</summary>
    public const double NaturalWidth = 300;

    /// <summary>
    /// The kind's sizing: one height, the row's, kept (never relaxed: a taller card would only be empty round the row), from
    /// <see cref="MinWidth"/> wide, a new one 300, as wide as the user makes it ([S-12411]: the slider longer).
    /// </summary>
    public static WidgetSizing Sizing()
        => WidgetSizing.Range(new SizeDip(MinWidth, Height), new SizeDip(NaturalWidth, Height), new SizeDip(NaturalWidth, Height)) with { KeepsHeight = true };
}
