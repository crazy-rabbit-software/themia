using Themia.Core.Model;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-2440] A kit list that shows a set number of rows: its config states how many rows it shows and
/// how many lines each has, and its size follows from them ([S-2438]); dragging its bottom edge sets
/// the rows ([S-2442]). Any kit list adopts it by implementing this on its config, stating
/// <see cref="KitSizing.Sizing"/> and <see cref="KitSizing.SizeSets"/> on its descriptor and adding
/// <c>KitMenuLines.Rows</c> to its menu.
/// </summary>
public interface IKitRows
{
    /// <summary>How many rows the list shows: <see cref="KitSizing.MinRows"/>..<see cref="KitSizing.MaxRows"/>; more items scroll.</summary>
    int Rows { get; set; }

    /// <summary>How many lines each row has: 1 or 2.</summary>
    int Lines { get; set; }
}

/// <summary>[S-2440] What a kit list with rows of its own shows, for its plan: a number of rows of a number of lines.</summary>
public readonly record struct KitRowsChoice(int Rows, int Lines);

/// <summary>
/// [S-2440] The size of a kit list that states its rows: the family's content top (36), its rows (28
/// or 40 each by its lines), the add footer (28) where it has one and the card's bottom inset (12);
/// the height stepped by a row, the width in its range. Pure, so every kit list shares it.
/// </summary>
public static class KitSizing
{
    public const int MinRows = 1;

    public const int MaxRows = 20;

    public const int DefaultRows = 6;

    /// <summary>The narrowest a list's row reads at: a check, a title worth reading and a short time.</summary>
    public const double MinWidth = 220;

    /// <summary>A new list's width.</summary>
    public const double NaturalWidth = 300;

    /// <summary>The widest a list grows: past it a row is a sentence across a room.</summary>
    public const double MaxWidth = 520;

    /// <summary>Rows held to their range; a stored count out of it is repaired to the nearest.</summary>
    public static int ClampRows(int rows) => Math.Clamp(rows, MinRows, MaxRows);

    /// <summary>Lines held to 1 or 2.</summary>
    public static int ClampLines(int lines) => Math.Clamp(lines, 1, 2);

    /// <summary>[S-2440] The config's choice, held to its ranges.</summary>
    public static KitRowsChoice Choice(IKitRows rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new(ClampRows(rows.Rows), ClampLines(rows.Lines));
    }

    /// <summary>[S-2440] The height's steps: everything but the rows as the base, a row as the step, the config's rows as the count.</summary>
    public static SizeSteps Steps(KitRowsChoice choice, bool addFooter)
        => new(
            FamilySpace.ContentTop + (addFooter ? FamilyShape.Row1 : 0) + FamilySpace.Inset,
            FamilyShape.RowHeight(choice.Lines),
            MinRows,
            MaxRows,
            choice.Rows);

    /// <summary>
    /// [S-2440] The sizing of a kit list showing <paramref name="shown"/> (the config's rows and the lines
    /// its rows really have: a one-line style has one): its width range, its height in rows.
    /// </summary>
    public static WidgetSizing Sizing(KitRowsChoice shown, bool addFooter)
        => WidgetSizing.Range(new(MinWidth, 1), new(NaturalWidth, 1), new(MaxWidth, 1))
            .WithHeightSteps(Steps(shown, addFooter));

    /// <summary>
    /// [S-2442] The config a list of <paramref name="size"/> means: the rows that height holds, its rows
    /// being as <paramref name="shown"/> says. A copy when they change, the same config otherwise; never
    /// the given one changed.
    /// </summary>
    public static WidgetConfig SizeSets(WidgetConfig config, SizeDip size, KitRowsChoice shown, bool addFooter)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config is not IKitRows rows)
        {
            return config;
        }

        var wanted = Steps(shown, addFooter).Nearest(size.Height);
        if (wanted == rows.Rows)
        {
            return config;
        }

        var copy = config.Clone();
        ((IKitRows)copy).Rows = wanted;
        return copy;
    }
}
