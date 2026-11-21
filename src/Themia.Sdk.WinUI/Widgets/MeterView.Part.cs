using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets.Meters;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1167] What a meter kind may add to its card besides the reading: commands on the right-click menu
/// (Refresh, Open in Explorer) and a mark over the content's top end (Ping's problem glyph, [S-12481]). The
/// view only passes them through; each kind decides in its own <see cref="MeterPart"/>.
/// </summary>
public sealed partial class MeterView : IWidgetContextActions
{
    private IWidgetSurface? _surface;

    /// <summary>[S-2510] A read outside the cadence (Refresh). Not while detached.</summary>
    public void SampleNow()
    {
        if (!_detached)
        {
            _watch?.Refresh();
        }
    }

    public IReadOnlyList<WidgetAction> ContextActions => _part.ContextActions(this);

    /// <summary>[S-12481] The kind's mark over the content's top end, over every row and column, once.</summary>
    private void PlaceMark()
    {
        if (_part.Mark is { Parent: null } mark)
        {
            mark.HorizontalAlignment = HorizontalAlignment.Right;
            mark.VerticalAlignment = VerticalAlignment.Top;
            mark.Margin = new Thickness(0, FamilySpace.S1, FamilySpace.Inset, 0);
            Canvas.SetZIndex(mark, 1);
            SetRowSpan(mark, 8);
            SetColumnSpan(mark, 8);
            Children.Add(mark);
        }
    }
}
