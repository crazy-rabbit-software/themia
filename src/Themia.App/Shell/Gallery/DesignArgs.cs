#if DEBUG
using Themia.Core.Model;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug renders only: <c>--design key=value,...</c> sets appearance values on the design a render
/// draws its cards with, so a border, radius or fill can be looked at without ⚙ › Appearance. The
/// text is read by <see cref="DesignText"/> (its keys are listed there); a pair it cannot read is
/// printed and skipped.
/// </summary>
internal static class DesignArgs
{
    /// <summary>The design for a render: <paramref name="theme"/>, then the <c>--design</c> values in order.</summary>
    public static DesignState From(string[] args, AppTheme theme)
    {
        var design = new DesignState();
        var at = Array.IndexOf(args, "--design");
        if (at >= 0 && at + 1 < args.Length)
        {
            foreach (var pair in DesignText.Apply(design, args[at + 1]))
            {
                Console.WriteLine($"design: skipped '{pair}'");
            }
        }

        // The render's --theme wins over a preset's own, so one look can be drawn in both.
        if (theme != AppTheme.System)
        {
            design.Theme = theme;
        }

        return design;
    }
}
#endif
