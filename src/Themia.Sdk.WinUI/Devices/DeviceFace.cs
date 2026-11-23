using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Devices;

/// <summary>
/// [S-16637] One device's widget face (a Drive, a USB stick, a network drive, a Bluetooth device): the device item
/// ([S-16630]) as the Drive widget drew its bar face, the name the Header widget's over it ([S-12800]): its icon and its
/// reading at 28 centred over the bar that spans the card (owner, 2026-11-20, of a reading that fills one axis: "Centred
/// is fine"), its line under the reading, the bar 8 under that; one block, planned at its natural box and scaled up as
/// one (<see cref="ReadingFit"/>, WR-7), down to its line at 9 (WR-14). A device without a reading keeps the face: "--",
/// the empty bar and why (WR-10). The line stands in the box of its widest forms (<see cref="Hold"/>, WR-15).
/// </summary>
public sealed partial class DeviceFace : Grid
{
    private readonly ReadingFit _fit = new();
    private readonly SpreadStack _figure = new() { Justify = true };
    private readonly ReadoutBlock _readout = new();
    private readonly DeviceIconSlot _icon;
    private readonly ChartBar _bar = new() { Margin = new Thickness(0, FamilySpace.S2, 0, 0) };
    private IReadOnlyList<string> _held = [];

    public DeviceFace(IWidgetHost host)
    {
        _icon = new DeviceIconSlot(host) { Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0) };
        _readout.Use(FamilyType.Big28);
        _figure.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Children = { _icon, _readout } });
        _figure.Children.Add(_bar);
        _fit.Parts.Add(_figure);

        // [S-14301] The bar runs across the content, the reading and its line over it from the start.
        _fit.Span(_figure);
        Children.Add(_fit);
    }

    /// <summary>[S-16637] Show <paramref name="item"/>: its icon, its reading (or "--"), its line and its bar.</summary>
    public void Show(DeviceItem item, bool stock)
    {
        ArgumentNullException.ThrowIfNull(item);
        var reading = item.Reading ?? DeviceReading.None();
        _readout.Show(reading.Value, reading.Unit, item.Line, valueRole: item.Absent || item.Reading is null ? ColourRole.TextDisabled : ColourRole.TextPrimary);
        var picture = _icon.ShowsPicture;
        _icon.Show(item.Icon, stock);
        if (picture != _icon.ShowsPicture)
        {
            // The icon's ink is read another way now (a picture's or a glyph's) at the same size: the block is fitted again.
            _fit.Refit();
        }

        _bar.Show(Math.Clamp(item.Fill, 0, 1));
        _bar.Role(item.FillRole);
    }

    /// <summary>
    /// [S-15750] WR-15: the line held to the widest of <paramref name="forms"/> (a drive's space line in every unit pair, its
    /// figures as 8s), so the block keeps its size and place as the reading changes; none: sized by what it shows.
    /// </summary>
    public void Hold(IReadOnlyList<string> forms)
    {
        ArgumentNullException.ThrowIfNull(forms);
        if (forms.SequenceEqual(_held))
        {
            return;
        }

        _held = forms;
        var line = _readout.UnderText;
        var widest = 0.0;
        foreach (var form in forms)
        {
            var probe = new TextBlock { Text = form, FontSize = line.FontSize, FontFamily = line.FontFamily, FontWeight = line.FontWeight, CharacterSpacing = line.CharacterSpacing };
            probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, probe.DesiredSize.Width);
        }

        line.MinWidth = Math.Ceiling(widest);

        // The line's box is its held template's: the place every form stands in (WR-15's holds).
        if (forms.Count > 0)
        {
            HoldMark.Mark(line);
        }
    }
}
