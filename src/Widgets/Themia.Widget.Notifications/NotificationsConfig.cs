using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-4400] Windows' notification list. [S-11404] What it shows is its own: how many notifications
/// (<see cref="Rows"/>), their lines (the title; the body under it) and whether they stand under their
/// apps' headings; its size follows.
/// </summary>
public sealed class NotificationsConfig : WidgetConfig, Themia.Sdk.Kit.IKitRows
{
    /// <summary>[S-11404] A new notification list's rows.</summary>
    public const int DefaultRows = 4;

    /// <summary>[S-11404] How many notifications show; more scroll inside them.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-11404] Lines per notification: the title with when it came, then its body.</summary>
    public int Lines { get; set; } = 2;

    /// <summary>[S-11404] [S-4402] The notifications under their apps' headings (off: one list, an icon a row).</summary>
    public bool AppHeadings { get; set; }

    /// <summary>[S-11404] The rows as drawn.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Themia.Sdk.Kit.KitRowsChoice RowsShown => new(Themia.Sdk.Kit.KitSizing.ClampRows(Rows), Themia.Sdk.Kit.KitSizing.ClampLines(Lines));

    public override IReadOnlyList<string> Repair()
    {
        var shown = RowsShown;
        if ((shown.Rows, shown.Lines) == (Rows, Lines))
        {
            return [];
        }

        (Rows, Lines) = (shown.Rows, shown.Lines);
        return ["notifications: rows or lines out of range"];
    }

    public override WidgetConfig Clone() => new NotificationsConfig { Rows = Rows, Lines = Lines, AppHeadings = AppHeadings };
}
