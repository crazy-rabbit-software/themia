using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12808] A one-readout view's block in its room (<see cref="BlockFit"/>): its parts (a reading and its caption,
/// its controls) are planned together in the room the kind's natural box gives them (a <see cref="SpreadStack"/>,
/// laid out once at that size) and the plan is drawn as one, scaled up until its ink fills the room's height or its
/// width stops it, centred on its ink in the height a width-bound plan leaves; across, where its parts stand.
/// The sides' and the foot's insets are this panel's margin (or its view's padding), so they never scale.
/// </summary>
public sealed partial class ReadingFit : Panel
{
    private readonly SpreadStack _plan = new();
    private readonly ScaleTransform _scale = new();
    private Size _natural;

    public ReadingFit()
    {
        Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _plan.RenderTransform = _scale;
        Children.Add(_plan);
    }

    /// <summary>The block's parts, top to bottom: at the natural box their first ink on its top, their last on its foot.</summary>
    public UIElementCollection Parts => _plan.Children;

    /// <summary>The least gap between two parts (a family token).</summary>
    public double Spacing
    {
        get => _plan.Spacing;
        set => _plan.Spacing = value;
    }

    /// <summary>
    /// Plan the block in the room <paramref name="config"/>'s natural box gives it: the card's natural box less the
    /// card's top inset, this panel's margin and the <paramref name="padding"/> its view stands it in.
    /// </summary>
    public void Hold(WidgetConfig config, Thickness padding = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var box = ModuleCatalog.Of(config.Kind).SizingOf(config).Natural;
        var natural = new Size(
            Math.Max(0, box.Width - Margin.Left - Margin.Right - padding.Left - padding.Right),
            Math.Max(0, box.Height - FamilySpace.ContentTop - Margin.Top - Margin.Bottom - padding.Top - padding.Bottom));
        if (natural != _natural)
        {
            _natural = natural;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Always the natural room, so a layout pass never measures the plan at another size (no cycle); the plan's
        // height is its parts', so a part that grows (a reading shown) asks for this panel's layout again.
        var room = _natural.Width > 0 && _natural.Height > 0 ? _natural : availableSize;
        _plan.Measure(room);
        return new Size(Math.Min(room.Width, availableSize.Width), Math.Min(room.Height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!(_natural.Width > 0) || !(_natural.Height > 0))
        {
            _scale.ScaleX = 1;
            _scale.ScaleY = 1;
            _plan.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

        // The plan in its natural room, to read where its ink stands; then the same plan placed and drawn scaled.
        _plan.Arrange(new Rect(0, 0, _natural.Width, _natural.Height));
        var (inkTop, inkBottom) = Ink();
        var lay = BlockFit.Lay(finalSize.Width, finalSize.Height, _natural.Width, inkTop, inkBottom);
        _scale.ScaleX = lay.Scale;
        _scale.ScaleY = lay.Scale;
        var drawn = _natural.Width * lay.Scale;
        var left = Across() switch
        {
            HorizontalAlignment.Center => (finalSize.Width - drawn) / 2,
            HorizontalAlignment.Right => finalSize.Width - drawn,
            _ => 0,
        };

        _plan.Arrange(new Rect(left, lay.Top, _natural.Width, _natural.Height));
        return finalSize;
    }

    /// <summary>Where the plan's ink runs, as laid in its natural room: its first part's first ink to its last part's last.</summary>
    private (double Top, double Bottom) Ink()
    {
        var laid = _plan.Laid;
        if (laid.Count == 0)
        {
            return (0, _natural.Height);
        }

        return (laid[0].Slot.Start + PartInk.Of(laid[0].Part).Over, laid[^1].Slot.End - PartInk.Of(laid[^1].Part).Under);
    }

    /// <summary>Where the block stands across a room wider than it: centred or at the end where its aligned parts all say so, else at the start.</summary>
    private HorizontalAlignment Across()
    {
        var shown = _plan.Children.OfType<FrameworkElement>()
            .Where(c => c.Visibility == Visibility.Visible && c.HorizontalAlignment != HorizontalAlignment.Stretch)
            .Select(c => c.HorizontalAlignment).Distinct().ToList();
        return shown.Count == 1 && shown[0] is HorizontalAlignment.Center or HorizontalAlignment.Right ? shown[0] : HorizontalAlignment.Left;
    }
}
