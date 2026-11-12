using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Geometry;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-11707] The add page's flyout, Karpo's (karpo-app, docs/widgets.md §593): exactly as wide as the edit
/// bar, flush with its sides, a flyout's gap under it, no beak (there is no one entry to point at: a
/// right-click and a key open it too); the page's ground, rim, radius and shadow (<see cref="EditBar.Frame"/>);
/// as tall as the page has been while open, up to 70 % of the work area, scrolling inside. Under its
/// ground a stage the pictures' cards stand on while they are drawn ([S-11710]).
/// </summary>
internal sealed partial class AddPageFlyout : Grid
{
    private readonly Border _ground = new();
    private readonly Canvas _stage = new() { IsHitTestVisible = false };

    /// <summary>The tallest the page has wanted since it opened: it grows, and never jumps back.</summary>
    private double _tallest;

    public AddPageFlyout(AddPage page, AddPageCommands commands)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(commands);
        EditBar.Frame(_ground);
        Pictures = new AddPictures(_stage);
        View = new AddPageView(page, commands, Pictures);
        Children.Add(_stage);
        Children.Add(_ground);
        Children.Add(View);

        // A press on the page is the page's: never the surface's (a rubber band, a deselect).
        PointerPressed += (_, e) => e.Handled = true;
        Unloaded += (_, _) => Pictures.Dispose();
    }

    public AddPageView View { get; }

    public AddPictures Pictures { get; }

    /// <summary>[S-11707] Stand under <paramref name="bar"/>, inside <paramref name="area"/>.</summary>
    public void Place(PixelRect bar, PixelRect area)
    {
        _tallest = Math.Max(_tallest, View.ContentHeight(bar.Width));
        var spot = FlyoutPlacement.Under(bar, area, _tallest);
        Width = spot.Width;
        Height = spot.Height;
        Canvas.SetLeft(this, spot.X);
        Canvas.SetTop(this, spot.Y);
    }
}
