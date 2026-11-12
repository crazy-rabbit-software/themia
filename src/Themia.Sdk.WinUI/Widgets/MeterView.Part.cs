using Microsoft.UI.Xaml;
using Themia.App.Widgets.Meters;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1167] What a meter kind may add to its card besides the reading: a header title (a drive's
/// name), the header's label ([S-2260]: the adapter, the host), commands on the right-click menu (Refresh, Open in Explorer) and an
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

    /// <summary>
    /// The header's trailing parts: the kind's own (Ping's problem glyph) when it has them, else the
    /// view's, so the label of <see cref="SetAbout"/> always has a header to stand in.
    /// </summary>
    public FrameworkElement? HeaderTrailing => Trailing;

    private HeaderParts? _own;

    private FrameworkElement Trailing => _part.HeaderTrailing ?? (_own ??= new HeaderParts());

    /// <summary>[S-2260] Which one the meter reads (an adapter's name, a host) as the header's label beside its name; null for none.</summary>
    public void SetAbout(string? about)
    {
        if (Trailing is HeaderParts parts)
        {
            parts.SetAbout(about);
        }
    }
}
