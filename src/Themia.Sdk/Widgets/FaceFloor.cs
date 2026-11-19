using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>[S-12833] A face's plan with some of its parts drawn: how wide it is and how tall its ink, at its own size (no inset).</summary>
public readonly record struct FacePlan(double Width, double Ink);

/// <summary>
/// [S-12833] How a face that scales stands in a room: the smallest text it draws (<see cref="Floor"/>: its parts smaller
/// than that give way), the scale it is drawn at (<see cref="Scale"/>, as its plan estimates it) and the most it may be
/// drawn at (<see cref="Most"/>: unbounded while every part is drawn, else the scale the last part gave way at).
/// </summary>
public readonly record struct FaceFit(double Floor, double Scale, double Most)
{
    /// <summary>Every part drawn, the face as its fit draws it: a room not measured yet.</summary>
    public static FaceFit All => new(0, 1, double.PositiveInfinity);

    /// <summary>Whether a part of text <paramref name="size"/> is drawn.</summary>
    public bool Draws(double size) => size >= Floor - 1e-9;
}

/// <summary>
/// [S-12833] The one rule for how small a face that scales as one goes (owner, 2026-11-19: "for the widgets that can scale
/// there should basically be no lower limit except for one that makes sense so you can still see the widget"; "the clock has
/// a min height that is way to big"). A face is its <b>main reading</b> (its largest text: a clock's figures, a date's line
/// or its day's number) and smaller parts (a meridiem, the line under, a weekday). The main reading alone sets how small the
/// face goes: until it is drawn at the legibility floor (<see cref="ScaleFloor.Text"/>, 9 DIP). A smaller part is drawn while
/// the face's scale keeps it at that floor too and gives way under it, the smallest first, so no text is ever drawn under 9
/// and none holds the reading large. Once a part gave way the face is drawn no larger than the scale it gave way at, so a
/// smaller room never draws a larger face (the scale only falls as the room shrinks). The least box is the main reading alone
/// at its floor in the card's inset (<see cref="LeastBox"/>), never under the grid's least widget.
/// </summary>
public static class FaceFloor
{
    /// <summary>
    /// How the face stands in a <paramref name="width"/> × <paramref name="height"/> room (the card's content box):
    /// <paramref name="sizes"/> are its parts' text sizes (the main reading's the largest), <paramref name="plan"/> its plan
    /// drawing the parts of at least the size it is given.
    /// </summary>
    public static FaceFit Fit(double width, double height, IReadOnlyCollection<double> sizes, Func<double, FacePlan> plan)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        ArgumentNullException.ThrowIfNull(plan);
        var levels = Levels(sizes);
        if (levels.Count == 0 || !(width > 0) || !(height > 0) || !double.IsFinite(width) || !double.IsFinite(height))
        {
            return FaceFit.All;
        }

        var main = levels[^1];
        var least = ScaleFloor.Least(main);
        var most = double.PositiveInfinity;
        foreach (var floor in levels)
        {
            var scale = Math.Min(most, Scale(width, height, plan(floor)));
            if (floor >= main || floor * scale >= ScaleFloor.Text - 1e-9)
            {
                return new FaceFit(floor, Math.Max(least, scale), most);
            }

            // It gives way here: at a smaller room the rest is never drawn larger than it was when it went.
            most = Math.Min(most, ScaleFloor.Text / floor);
        }

        return new FaceFit(main, least, most);
    }

    /// <summary>
    /// The least box of the face (no inset): its main reading alone (<paramref name="plan"/> of the largest of
    /// <paramref name="sizes"/>) at the legibility floor.
    /// </summary>
    public static SizeDip LeastBox(IReadOnlyCollection<double> sizes, Func<double, FacePlan> plan)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        ArgumentNullException.ThrowIfNull(plan);
        var levels = Levels(sizes);
        if (levels.Count == 0)
        {
            return new SizeDip(0, 0);
        }

        var main = levels[^1];
        var alone = plan(main);

        // Times the floor, then over the reading's size: a whole box stays whole (364 × 9 / 28 is 117, not 117.00000000000001).
        return ScaleFloor.Least(main) < 1
            ? new SizeDip(alone.Width * ScaleFloor.Text / main, alone.Ink * ScaleFloor.Text / main)
            : new SizeDip(alone.Width, alone.Ink);
    }

    /// <summary>The scale a plan fills a room at: its width or its ink's height stops it, whichever first (<c>BlockFit</c>'s rule).</summary>
    public static double Scale(double width, double height, FacePlan plan)
    {
        var byWidth = plan.Width > 0 && double.IsFinite(plan.Width) ? width / plan.Width : double.PositiveInfinity;
        var byHeight = plan.Ink > 0 && double.IsFinite(plan.Ink) ? height / plan.Ink : double.PositiveInfinity;
        var scale = Math.Min(byWidth, byHeight);
        return double.IsFinite(scale) ? scale : 1;
    }

    /// <summary>The distinct usable sizes, smallest first.</summary>
    private static List<double> Levels(IReadOnlyCollection<double> sizes)
        => [.. sizes.Where(s => s > 0 && double.IsFinite(s)).Distinct().Order()];
}
