using System.Text.Json.Serialization;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-1320] A details-view column after Name. A closed set: the original's string keys, typed.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FolderColumn>))]
public enum FolderColumn
{
    Size = 0,
    Modified = 1,
    Type = 2,
    Cloud = 3,
    Git = 4,
    ItemCount = 5,
}

/// <summary>
/// The details view's column set: what exists, what is on by default, how wide each is, and which of
/// the chosen ones fit a given width ([S-1322]).
/// </summary>
public static class FolderColumns
{
    /// <summary>Every column, in the order the settings page offers them.</summary>
    public static IReadOnlyList<FolderColumn> All { get; } =
        [FolderColumn.Size, FolderColumn.Modified, FolderColumn.Type, FolderColumn.Cloud, FolderColumn.Git, FolderColumn.ItemCount];

    /// <summary>On for a new widget, as in the original: Size, Modified, Type.</summary>
    public static IReadOnlyList<FolderColumn> Defaults { get; } = [FolderColumn.Size, FolderColumn.Modified, FolderColumn.Type];

    /// <summary>The Name column never sheds below this; a name cut to three letters is no listing.</summary>
    public const double NameMinWidth = 120;

    /// <summary>The gap between two columns.</summary>
    public const double Gap = 8;

    /// <summary>A column's width in DIPs, wide enough for its worst-case text at the Caption step.</summary>
    public static double Width(FolderColumn column) => column switch
    {
        FolderColumn.Size => 64,
        FolderColumn.Modified => 80,
        FolderColumn.Type => 96,
        FolderColumn.Cloud => 24,
        FolderColumn.Git => 40,
        FolderColumn.ItemCount => 48,
        _ => 64,
    };

    public static string Title(FolderColumn column) => column switch
    {
        FolderColumn.Size => Str.Folder_ColSize.Text(),
        FolderColumn.Modified => Str.Folder_ColModified.Text(),
        FolderColumn.Type => Str.Folder_ColType.Text(),
        FolderColumn.Cloud => Str.WidgetsB_Folder_ColCloud.Text(),
        FolderColumn.Git => Str.WidgetsB_Folder_ColGit.Text(),
        FolderColumn.ItemCount => Str.WidgetsB_Folder_ColItems.Text(),
        _ => column.ToString(),
    };

    /// <summary>
    /// Whether the column's text sits at the right edge (numbers) rather than the left. [S-6411] The
    /// row's <paramref name="last"/> column always does, so the rightmost text ends at the row's right
    /// inset instead of leaving its slot's slack as a blank strip.
    /// </summary>
    public static bool AlignsRight(FolderColumn column, bool last)
        => last || column is FolderColumn.Size or FolderColumn.ItemCount or FolderColumn.Modified;

    /// <summary>
    /// [S-1322] The chosen columns that fit <paramref name="width"/> beside a Name column of at least
    /// <see cref="NameMinWidth"/>, taken in the user's order: the first one that does not fit closes
    /// the row, so a column never appears without the ones the user put before it.
    /// </summary>
    public static IReadOnlyList<FolderColumn> Fit(IReadOnlyList<FolderColumn> chosen, double width)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        var left = width - NameMinWidth;
        var fitted = new List<FolderColumn>(chosen.Count);

        foreach (var column in chosen)
        {
            var cost = Width(column) + Gap;

            if (cost > left)
            {
                break;
            }

            left -= cost;
            fitted.Add(column);
        }

        return fitted;
    }

    /// <summary>
    /// A stored column list with unknown values and repeats removed, order kept. Returns how many
    /// were dropped so the caller can say so.
    /// </summary>
    public static (List<FolderColumn> Clean, int Dropped) Clean(IEnumerable<FolderColumn>? stored)
    {
        var clean = new List<FolderColumn>();
        var dropped = 0;

        foreach (var column in stored ?? [])
        {
            if (!Enum.IsDefined(column) || clean.Contains(column))
            {
                dropped++;
                continue;
            }

            clean.Add(column);
        }

        return (clean, dropped);
    }

    /// <summary>The list with <paramref name="column"/> added at its canonical place, or removed.</summary>
    public static List<FolderColumn> Toggle(IReadOnlyList<FolderColumn> chosen, FolderColumn column)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        if (chosen.Contains(column))
        {
            return [.. chosen.Where(c => c != column)];
        }

        // Added where it sits in the canonical order relative to the ones already on, so toggling a
        // column off and on again puts it back where it was rather than at the end.
        var result = new List<FolderColumn>(chosen);
        var rank = All.ToList().IndexOf(column);
        var at = result.FindIndex(c => All.ToList().IndexOf(c) > rank);
        result.Insert(at < 0 ? result.Count : at, column);
        return result;
    }
}
