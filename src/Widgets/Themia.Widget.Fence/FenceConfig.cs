using Themia.Core.Desktop;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-10900] [S-13000] What one fence remembers: its title, the desktop items it holds (by their keys, in its order:
/// <see cref="IHoldsDesktopItems"/>, which the host's desktop rules read and rewrite), whether it is rolled up and the
/// height it unrolls to, its standing sort, its rules for new items, and how its title bar rolls it up. Every property is
/// stored; <see cref="Clone"/> copies each.
/// </summary>
public sealed class FenceConfig : WidgetConfig, IHoldsDesktopItems
{
    /// <summary>The longest title kept.</summary>
    public const int MaxName = 80;

    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public List<string> Items { get; set; } = [];

    /// <inheritdoc />
    public FenceRuleSet Rules { get; set; } = new();

    /// <inheritdoc />
    public bool RolledUp { get; set; }

    /// <inheritdoc />
    public double OpenHeight { get; set; }

    /// <inheritdoc />
    public DesktopSortBy? Sort { get; set; }

    /// <summary>[S-10912] A click on the title bar rolls the fence up (and unrolls it), as Fences' own; on by default.</summary>
    public bool RollUpOnClick { get; set; } = true;

    /// <summary>[S-10913] A rolled-up fence opens while the pointer rests on its title bar, and rolls up again when it leaves.</summary>
    public bool HoverPeek { get; set; } = true;

    public override WidgetConfig Clone() => new FenceConfig
    {
        Name = Name,
        Items = [.. Items],
        Rules = Rules.Clone(),
        RolledUp = RolledUp,
        OpenHeight = OpenHeight,
        Sort = Sort,
        RollUpOnClick = RollUpOnClick,
        HoverPeek = HoverPeek,
    };

    /// <summary>
    /// [S-13002] A hand-edited file made whole: a null list or rule set is empty, the title loses its control characters and
    /// is cut at <see cref="MaxName"/>. The items' keys and the rest are the host's load rules (<c>DesktopFences.Repair</c>).
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        if (Items is null)
        {
            Items = [];
            repairs.Add("a fence's items were null; it holds none");
        }

        if (Rules is null)
        {
            Rules = new FenceRuleSet();
            repairs.Add("a fence's rules were null; it has none");
        }

        var name = CleanName(Name);
        if (!string.Equals(name, Name, StringComparison.Ordinal))
        {
            Name = name;
            repairs.Add("a fence's title was trimmed");
        }

        return repairs;
    }

    /// <summary>[S-10910] A title as it is kept: no control character, trimmed, at most <see cref="MaxName"/> characters.</summary>
    public static string CleanName(string? typed)
    {
        var clean = new string([.. (typed ?? string.Empty).Where(c => !char.IsControl(c))]).Trim();
        return clean.Length > MaxName ? clean[..MaxName].TrimEnd() : clean;
    }
}
