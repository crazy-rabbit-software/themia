using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

public sealed partial class WidgetInstance
{
    /// <summary>
    /// Geometry is validated against a unit screen: the stored model is fractional, so the invariants (on-screen,
    /// non-inverted, finite) hold independently of any real monitor.
    /// </summary>
    private static readonly ScreenMetrics UnitScreen = new(1920, 1080);

    /// <summary>
    /// [S-44] Bring a read widget back inside its invariants, each line naming it: its config's own ranges, its style,
    /// its fills, its background image, its item text colour, its scale, its alignment and its rect. Its config and its
    /// id, unique in its view, are the view's (<see cref="ViewState.Repair"/>).
    /// </summary>
    public IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        // The config's own invariants: ranges and values a hand edit made unusable.
        foreach (var repair in Config.Repair())
        {
            repairs.Add($"widget '{Id}': {repair}");
        }

        // [S-6004] A style the kind does not have: its first style. One rule for every styled kind.
        if (WidgetStyles.Repair(Config) is { } style)
        {
            repairs.Add($"widget '{Id}': {style}");
        }

        // [S-6561] A widget's own fill choices: repaired like the app-wide ones; absent follows them.
        Background ??= new FillChoice();
        ItemBackground ??= new FillChoice();
        foreach (var repair in Background.Repair("background").Concat(ItemBackground.Repair("item background")))
        {
            repairs.Add($"widget '{Id}': {repair}");
        }

        // [S-6591] A widget's background image: a picture file's full path, or none. A file that is
        // not there now is kept: the card draws no image until it is back ([S-6590]).
        var (image, imageRepair) = Themia.Core.Widgets.BackgroundImage.Repair(BackgroundImage);
        BackgroundImage = image.Length == 0 ? null : image;
        if (imageRepair is not null)
        {
            repairs.Add($"widget '{Id}': {imageRepair}");
        }

        // [S-6543] A widget's own item text colour: a colour, or empty to follow the app-wide one.
        var (ownText, ownTextRepair) = ItemLook.RepairTextColour(ItemTextColor);
        ItemTextColor = ownText;
        if (ownTextRepair is not null)
        {
            repairs.Add($"widget '{Id}': {ownTextRepair}");
        }

        // [S-6533] A scale outside 50–200 % or off the 5 % grain: the nearest one it may hold.
        if (WidgetScale.Repair(this) is { } scale)
        {
            repairs.Add($"widget '{Id}': {scale}");
        }

        // [S-6585] An alignment outside the set, or on a kind with nothing to align: the kind's default.
        if (ContentAlignment.Repair(this) is { } align)
        {
            repairs.Add($"widget '{Id}': {align}");
        }

        var settled = GridGeometry.Clamp(Rect, UnitScreen);
        if (settled != Rect)
        {
            Rect = settled;
            repairs.Add($"widget '{Id}' was off-screen or below the size floor");
        }

        return repairs;
    }
}
