using System.Text.Json.Serialization;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-4840] Recent files: the list is Windows' own Recent Items. [S-11200] How many files it shows and
/// whether each has its folder under its name are the user's (the kit's rows, [S-2440]); its height follows.
/// </summary>
public sealed class RecentFilesConfig : WidgetConfig, IKitRows
{
    /// <summary>[S-11200] How many files the list shows; more scroll.</summary>
    public int Rows { get; set; } = KitSizing.DefaultRows;

    /// <summary>[S-11200] Lines per file: two put its folder under its name.</summary>
    public int Lines { get; set; } = 2;

    /// <summary>[S-11200] The rows as drawn, held to their ranges.</summary>
    [JsonIgnore]
    public KitRowsChoice RowsShown => KitSizing.Choice(this);

    /// <summary>[S-4843] Keeps "2h" honest; the list itself redraws from the folder watch.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    public override IReadOnlyList<string> Repair()
    {
        if (Rows == KitSizing.ClampRows(Rows) && Lines == KitSizing.ClampLines(Lines))
        {
            return [];
        }

        (Rows, Lines) = (KitSizing.ClampRows(Rows), KitSizing.ClampLines(Lines));
        return ["a Recent files widget's rows or lines were out of range"];
    }

    public override WidgetConfig Clone() => new RecentFilesConfig { Rows = Rows, Lines = Lines };
}
