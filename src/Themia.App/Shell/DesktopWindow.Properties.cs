using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Geometry;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-3707]..[S-3710] The selected widgets' properties strip on this display: what it offers, where
/// it stands against the selection, and stepping aside while the selection is dragged or resized.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3710] A drag or a resize is under way: the strip is away until it ends.</summary>
    private bool _propertiesHeld;

    private PropertiesStrip BuildProperties()
    {
        var strip = new PropertiesStrip(PropertiesCommands());

        // Its real width arrives with layout (a title box, words in the current language): place it again.
        strip.SizeChanged += (_, _) => PlaceProperties();
        return strip;
    }

    /// <summary>What this display's strip does: everything goes through the host, as one undo step each.</summary>
    private PropertiesCommands PropertiesCommands() => new(
        SetHeader: _host.SetSelectedHeader,
        SetSurface: _host.SetSelectedSurface,
        SetTitle: _host.SetTitle,
        ReleaseFocus: () => _root.Focus(FocusState.Programmatic),
        SetStyle: _host.SetSelectedStyle,
        OpenSettings: id => _host.ShowConfig(new Config.ConfigTarget.Widget(id)));

    /// <summary>[S-3701] The strip shows what is selected in the view this display shows, and nothing otherwise.</summary>
    private void SyncProperties()
    {
        _properties.ShowSelection(_host.QuickSetFor(CurrentViewId));
        PlaceProperties();
    }

    /// <summary>[S-3710] Put the strip away while a drag or resize runs, and back where the widgets landed.</summary>
    private void HoldProperties(bool held)
    {
        if (_propertiesHeld == held)
        {
            return;
        }

        _propertiesHeld = held;
        PlaceProperties();
    }

    /// <summary>
    /// [S-3708]/[S-3710] Show the strip against the selection's box, above it or below it, inside the
    /// work area and clear of the edit bar and the view bar; or hide it: outside edit mode, while
    /// placing, while held, with nothing selected here.
    /// </summary>
    private void PlaceProperties()
    {
        if (_closed)
        {
            return;
        }

        var metrics = Metrics;
        if (!EditMode || _host.Placing is not null || _propertiesHeld || !_properties.Offer.Shows
            || !metrics.IsUsable || SelectionBox(metrics) is not { } target)
        {
            _properties.Visibility = Visibility.Collapsed;
            return;
        }

        _properties.Visibility = Visibility.Visible;
        _properties.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _properties.DesiredSize;

        var area = GridGeometry.AllowedPixels(metrics);
        var avoid = new List<PixelRect> { ToolbarRect(area) };
        if (ViewBarBox() is { } bar)
        {
            avoid.Add(bar);
        }

        var spot = StripPlacement.Place(target, size.Width, size.Height, area, avoid);
        Canvas.SetLeft(_properties, Math.Round(spot.X));
        Canvas.SetTop(_properties, Math.Round(spot.Y));
    }

    /// <summary>[S-3709] The painted box around every selected card this display shows, where it is now.</summary>
    private PixelRect? SelectionBox(ScreenMetrics metrics)
    {
        var selection = _host.Selection;
        if (selection.IsEmpty || !string.Equals(selection.ViewId, CurrentViewId, StringComparison.Ordinal))
        {
            return null;
        }

        return StripPlacement.Bounds(_cards
            .Where(c => c.Visibility == Visibility.Visible && selection.Contains(c.Model.Id))
            .Select(c => GridGeometry.Inset(GridGeometry.ToPixels(c.LiveRect, metrics), Design.WidgetGapPx)));
    }
}
