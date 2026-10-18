using Microsoft.UI.Xaml;
using Themia.App.Widgets.Meters;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1167] What a meter kind may add to its card besides the reading: a header title (a drive's
/// name, a host's label), commands on the right-click menu (Refresh, Open in Explorer) and an
/// element at the right of the header (a status dot). The view only passes them through; each kind
/// decides in its own <see cref="MeterPart"/>.
/// </summary>
public sealed partial class MeterView : IWidgetContextActions, IWidgetHeaderContent
{
    private IWidgetSurface? _surface;
    private string? _title;

    /// <summary>Called from <c>Bind</c>: the card's surface, and the title a kind offered before it was there.</summary>
    private void BindTitle(IWidgetSurface surface)
    {
        _surface = surface;

        if (_title is not null)
        {
            surface.SetHeaderTitle(_title);
        }
    }

    /// <summary>Only on a change: the card relayouts its header for a new title.</summary>
    public void SetTitle(string? title)
    {
        if (title == _title)
        {
            return;
        }

        _title = title;
        _surface?.SetHeaderTitle(title);
    }

    /// <summary>[S-2510] A read outside the cadence (Refresh). Not while detached.</summary>
    public void SampleNow()
    {
        if (!_detached)
        {
            _watch?.Refresh();
        }
    }

    public IReadOnlyList<WidgetAction> ContextActions => _part.ContextActions(this);

    public FrameworkElement? HeaderTrailing => _part.HeaderTrailing;
}
