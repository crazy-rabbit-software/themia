using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7060] How a kit list draws its items: one item as the family's Row block ([S-6711]). The list is the shell around
/// the rows and knows no look: it lays them (a scroll of whole rows at their one fixed size, pages, or the check layout),
/// wraps each in the item that takes the pointer, the menu, the keys and the focus ring, and says what is on screen.
/// The kit's own looks are <see cref="KitRichLook"/> (the rich, two-line and one-line rows), <see cref="KitTableLook"/>,
/// <see cref="KitScheduleLook"/> and <see cref="KitCheckLook"/>, one per <see cref="KitRowLook"/>
/// (<see cref="KitLooks.For"/>); a widget whose rows carry a part of their own (a level's slider, a drive's bar) hands
/// its list a look of its own.
/// </summary>
public interface IKitLook
{
    /// <summary>[S-7037] The rows are the check layout's: column by column, an add row under them, renamed and dragged in place.</summary>
    bool Checks => false;

    /// <summary>One item's row, for its place in <paramref name="draw"/>.</summary>
    RowBlock Draw(KitItem item, KitDraw draw);

    /// <summary>
    /// Rewrite a standing row for a new reading of the item in its place (<paramref name="was"/> the one it showed), so
    /// a part under the pointer (a slider held) or an icon already loaded survives a refresh; false: the list draws the
    /// row again.
    /// </summary>
    bool Update(RowBlock row, KitItem was, KitItem item, KitDraw draw) => false;

    /// <summary>A row the list took down (drawn again, or the list redrawn): what the look hooked on it comes off.</summary>
    void Dropped(RowBlock row)
    {
    }

    /// <summary>[S-7035] [S-784] A group's heading over its first row: one of the rows that tile the list, its text BodyStrong secondary.</summary>
    FrameworkElement Heading(KitGroup group, KitDraw draw) => SectionHeading.Make(group.Heading ?? string.Empty, draw.Plan.HeadingHeight, group.Selected);

    /// <summary>[S-597] An item's check flipped on screen before its write (the check look's optimistic tick).</summary>
    void Toggled(FrameworkElement check, KitItem item, bool done)
    {
    }

    /// <summary>[S-7034] Fill the column titles over the rows (a table's); false where the look has none.</summary>
    bool Titles(Grid titles, KitDraw draw) => false;
}

/// <summary>
/// [S-7060] What a look is handed to draw a row with: the spec and the plan of the draw, its moment, the row's place, and
/// the list's shared parts (the kit's leads with the one site-icon cache, the times the ticker rewrites, the check the
/// list toggles).
/// </summary>
public sealed class KitDraw
{
    private readonly Action<TextBlock, KitWhen?, KitTime> _time;
    private readonly Func<string, FrameworkElement> _site;
    private readonly Action<FrameworkElement, int> _check;

    internal KitDraw(KitSpec spec, KitPlan plan, DateTimeOffset now, ItemGrid grid, Action<TextBlock, KitWhen?, KitTime> time, Func<string, FrameworkElement> site, Action<FrameworkElement, int> check)
    {
        Spec = spec;
        Plan = plan;
        Now = now;
        Grid = grid;
        _time = time;
        _site = site;
        _check = check;
    }

    public KitSpec Spec { get; }

    public KitPlan Plan { get; }

    /// <summary>The card's grid at the display's scale: what a band over the rows ends on ([S-14500]).</summary>
    public ItemGrid Grid { get; }

    /// <summary>The draw's one reading of the clock: a list drawn across a minute boundary shows one "now".</summary>
    public DateTimeOffset Now { get; }

    /// <summary>The item's place in the list.</summary>
    public int Index { get; internal set; }

    /// <summary>The line the row stands on, down its column: its height is the plan's <see cref="KitPlan.RowAt"/>.</summary>
    public int Line { get; internal set; }

    /// <summary>The row's one fixed size ([S-14810]).</summary>
    public double Height => Plan.RowAt(Line);

    /// <summary>The item's title, or the spec's "Untitled" for one that has none.</summary>
    public string Title(KitItem item) => item.Title.Length > 0 ? item.Title : Spec.Untitled();

    /// <summary>The item's first badge, where it has one.</summary>
    public static string? Badge(KitItem item) => item.Badges is [{ Text: { Length: > 0 } text }, ..] ? text : null;

    /// <summary>[S-7061] Rewrite <paramref name="text"/> with the item's time on the ticker (the ages).</summary>
    public void Times(TextBlock text, KitWhen? when, KitTime time) => _time(text, when, time);

    /// <summary>[S-7061] The item's time in the spec's words, for this draw's moment.</summary>
    public string When(KitWhen? when, KitTime time) => KitText.When(Strings.Current, when, time, Now);

    /// <summary>[S-597] <paramref name="check"/> is the row's check: a click on it toggles the item, and it says what that does.</summary>
    public void Check(FrameworkElement check) => _check(check, Index);

    /// <summary>The kit's lead for the item (its glyph, the site's icon), or null when it has none to draw.</summary>
    public FrameworkElement? Lead(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Lead.Kind switch
        {
            KitLeadKind.SiteIcon when item.Lead.Value is { Length: > 0 } link => _site(link),
            KitLeadKind.Icon when Enum.TryParse<KitIcon>(item.Lead.Value, out var icon) => Family.Glyph(KitIcons.Glyph(icon), FamilyShape.Icon, Tone(item.Lead.Tone, ColourRole.TextSecondary)),
            _ => null,
        };
    }

    /// <summary>A text's colour for a tone: Caution and Critical say a state; every other tone is the plain role.</summary>
    public static ColourRole Tone(KitTone tone, ColourRole plain) => tone switch
    {
        KitTone.Caution => ColourRole.Caution,
        KitTone.Critical => ColourRole.Critical,
        _ => plain,
    };
}

/// <summary>[S-7060] The kit's look for each of the spec's row looks.</summary>
public static class KitLooks
{
    public static IKitLook For(KitSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Look switch
        {
            KitRowLook.Table => new KitTableLook(),
            KitRowLook.Schedule => new KitScheduleLook(),
            KitRowLook.Check => new KitCheckLook(),
            _ => new KitRichLook(),
        };
    }
}
