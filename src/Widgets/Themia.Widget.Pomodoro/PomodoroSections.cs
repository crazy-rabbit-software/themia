using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-8115] One phase's section of the cycle's progress: its share of the cycle, how much of it is filled, its colour role.</summary>
public readonly record struct PomodoroSection(double Share, double Fill, ColourRole Role);

/// <summary>[S-8115] A section laid along a bar: its left edge and width, and the width of its fill from that edge.</summary>
public readonly record struct SectionRect(double Left, double Width, double Filled, ColourRole Role);

/// <summary>[S-8115] A section laid round a ring: where it starts and how far it runs (degrees from twelve, clockwise), and how far its fill runs.</summary>
public readonly record struct SectionArc(double Start, double Sweep, double Filled, ColourRole Role);

/// <summary>
/// [S-8115] The cycle's one sectioned progress (owner, 2026-11-20: "the progress bar should be a sectioned one that
/// shows all the phases in one progress bar sectioned"): a section per phase, its length its share of the cycle, a
/// thin gap (<see cref="Gap"/>) between two; the phases done filled, the current one filling with its progress, the
/// ones to come the track. A focus section fills in the accent, a break's calm (the secondary text's role): the
/// colour says the kind, never a word.
/// </summary>
public static class PomodoroSections
{
    /// <summary>[S-8115] The gap between two sections: the family's least space.</summary>
    public const double Gap = FamilySpace.S1;

    /// <summary>[S-8115] A section's fill role by its phase's kind: focus the accent, a break calm.</summary>
    public static ColourRole Role(PomodoroPhaseKind kind) => kind == PomodoroPhaseKind.Focus ? ColourRole.Accent : ColourRole.TextSecondary;

    /// <summary>
    /// [S-8115] The sections of a cycle whose phases run <paramref name="lengths"/> seconds and are of
    /// <paramref name="kinds"/>, at phase <paramref name="current"/> with <paramref name="progress"/> (0..1) of it gone:
    /// the earlier phases filled, the current one by its progress, the later ones empty.
    /// </summary>
    public static IReadOnlyList<PomodoroSection> Of(IReadOnlyList<int> lengths, IReadOnlyList<PomodoroPhaseKind> kinds, int current, double progress)
    {
        ArgumentNullException.ThrowIfNull(lengths);
        ArgumentNullException.ThrowIfNull(kinds);
        var count = Math.Min(lengths.Count, kinds.Count);
        if (count == 0)
        {
            return [];
        }

        var total = 0.0;
        for (var i = 0; i < count; i++)
        {
            total += Math.Max(0, lengths[i]);
        }

        var at = PomodoroCycle.Normalise(current, count);
        var share = double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0;
        var sections = new PomodoroSection[count];
        for (var i = 0; i < count; i++)
        {
            var part = total > 0 ? Math.Max(0, lengths[i]) / total : 1.0 / count;
            var fill = i < at ? 1 : i == at ? share : 0;
            sections[i] = new PomodoroSection(part, fill, Role(kinds[i]));
        }

        return sections;
    }

    /// <summary>
    /// [S-8115] The sections along a bar <paramref name="width"/> wide: the gaps taken first, the rest shared by the
    /// sections' shares, the first on the bar's start and the last on its end.
    /// </summary>
    public static IReadOnlyList<SectionRect> Bar(double width, IReadOnlyList<PomodoroSection> sections, double gap = Gap)
    {
        ArgumentNullException.ThrowIfNull(sections);
        if (sections.Count == 0 || !(width > 0) || !double.IsFinite(width))
        {
            return [];
        }

        var gaps = gap * (sections.Count - 1);
        if (gaps >= width)
        {
            gap = 0;
            gaps = 0;
        }

        var room = width - gaps;
        var rects = new SectionRect[sections.Count];
        var left = 0.0;
        for (var i = 0; i < sections.Count; i++)
        {
            // The last ends on the bar's end exactly, whatever the shares add up to.
            var w = i == sections.Count - 1 ? Math.Max(0, width - left) : room * sections[i].Share;
            rects[i] = new SectionRect(left, w, w * Math.Clamp(sections[i].Fill, 0, 1), sections[i].Role);
            left += w + gap;
        }

        return rects;
    }

    /// <summary>
    /// [S-8115] The sections round a ring <paramref name="diameter"/> wide at the Chart's stroke, from twelve,
    /// clockwise: each gap <paramref name="gap"/> long on the ring's line between two round caps (so a cap's half
    /// stroke either side is added to it), the first section starting half a gap after twelve so twelve stands in a
    /// gap; a cycle of one phase is the whole ring.
    /// </summary>
    public static IReadOnlyList<SectionArc> Ring(double diameter, IReadOnlyList<PomodoroSection> sections, double gap = Gap)
    {
        ArgumentNullException.ThrowIfNull(sections);
        if (sections.Count == 0 || !(diameter > FamilyShape.Stroke) || !double.IsFinite(diameter))
        {
            return [];
        }

        if (sections.Count == 1)
        {
            return [new SectionArc(0, 360, 360 * Math.Clamp(sections[0].Fill, 0, 1), sections[0].Role)];
        }

        var radius = (diameter - FamilyShape.Stroke) / 2;
        var gapDegrees = (gap + FamilyShape.Stroke) / radius * 180 / Math.PI;
        if (gapDegrees * sections.Count >= 360)
        {
            gapDegrees = 0;
        }

        var room = 360 - (gapDegrees * sections.Count);
        var arcs = new SectionArc[sections.Count];
        var start = gapDegrees / 2;
        for (var i = 0; i < sections.Count; i++)
        {
            var sweep = i == sections.Count - 1 ? Math.Max(0, 360 - (gapDegrees / 2) - start) : room * sections[i].Share;
            arcs[i] = new SectionArc(start, sweep, sweep * Math.Clamp(sections[i].Fill, 0, 1), sections[i].Role);
            start += sweep + gapDegrees;
        }

        return arcs;
    }
}
