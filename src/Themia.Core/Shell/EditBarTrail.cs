namespace Themia.Core.Shell;

/// <summary>
/// [S-3507] The widths of the edit bar's trail (Display ▾ › View ▾ › Widget ▾), after Karpo's
/// (karpo-app, docs/widgets.md §352, Dock.EditDeck.cs): each crumb has a widest it may be (170, 160
/// and 160 DIPs: Karpo's four crumbs' room shared by three, the display's a little wider for a product
/// name) and the trail always reserves room for all three at their widest plus the 20 DIPs
/// between two, so nothing after it moves as the widget crumb comes and goes. Where the display
/// leaves less, every crumb gives up the same amount, none under <see cref="Min"/>. Pure.
/// </summary>
public static class EditBarTrail
{
    /// <summary>The widest each crumb may be, in their order: Display, View, Widget.</summary>
    public static IReadOnlyList<double> Widest { get; } = [170, 160, 160];

    /// <summary>The narrowest a crumb is made to give way to: below it the bar is wider than the room.</summary>
    public const double Min = 96;

    /// <summary>Between two crumbs: 20 DIPs, the 12-DIP › centred in it.</summary>
    public const double Between = 20;

    /// <summary>The room the trail takes where nothing is short: every crumb at its widest.</summary>
    public static double Reserve => Widest.Sum() + ((Widest.Count - 1) * Between);

    /// <summary>The room it takes where everything is short: every crumb at <see cref="Min"/>.</summary>
    public static double Least => (Widest.Count * Min) + ((Widest.Count - 1) * Between);

    /// <summary>
    /// [S-3507] The widest each crumb may be within <paramref name="room"/> DIPs for the whole trail:
    /// <see cref="Widest"/> where the room holds the reserve; else each gives up the same amount, a
    /// crumb that reaches <see cref="Min"/> stays there and the others give up the rest; never under
    /// <see cref="Min"/> (a room not a number counts as none).
    /// </summary>
    public static double[] Widths(double room)
    {
        var count = Widest.Count;
        var widths = Widest.ToArray();
        var spare = (double.IsFinite(room) ? room : 0) - ((count - 1) * Between);
        var over = widths.Sum() - spare;

        // Water-filling: the crumbs still above Min share what is still over, the same each.
        while (over > 0.001)
        {
            var above = widths.Count(w => w > Min);
            if (above == 0)
            {
                break;
            }

            var share = over / above;
            var step = Math.Min(share, widths.Where(w => w > Min).Min() - Min);
            for (var i = 0; i < count; i++)
            {
                if (widths[i] > Min)
                {
                    widths[i] -= step;
                }
            }

            over -= step * above;
        }

        return widths;
    }
}
