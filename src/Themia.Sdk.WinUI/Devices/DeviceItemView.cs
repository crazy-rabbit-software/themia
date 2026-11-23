using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Devices;

/// <summary>
/// [S-16630] [S-16631] One device item, the device family's one face (owner, 2026-11-23: "create a base design that all of
/// those widgets can use. it should look very similar to how todays drive widget looks like"): the Drive widget's bar face
/// with its name. In the card's inset: the device's icon at the reading's line (36, the drive's own picture or the kind's
/// glyph at 28), 8, its reading at 28 (held to "100%", so nothing beside it moves as it changes, WR-15), 12, its name
/// (Body, primary, cut with its ellipsis); under the reading its secondary line (Caption, secondary); 8 under that the bar
/// (the Chart block's 4 track) across the item. A device without a reading keeps the face: "--" in the disabled role, the
/// empty track, the line saying why (WR-10). The item is its box edge to edge (WR-3): the list lights it under the pointer.
/// </summary>
public sealed partial class DeviceItemView : Grid
{
    /// <summary>
    /// [S-16631] The reading's column: as wide as its widest form ("100%"), the reading standing at its start, so every
    /// item's name starts at one place and none moves as its reading changes (WR-15).
    /// </summary>
    private static double ReadingWidth
    {
        get
        {
            if (_readingWidth is { } known)
            {
                return known;
            }

            var probe = new ReadoutBlock();
            probe.Use(FamilyType.Big28);
            probe.Show(FigureTemplate.Of("100"), "%");
            probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            _readingWidth = Math.Ceiling(probe.DesiredSize.Width);
            return _readingWidth.Value;
        }
    }

    private static double? _readingWidth;

    private readonly DeviceIconSlot _icon;
    private readonly ReadoutBlock _readout = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = Family.Text(FamilyType.Body, ColourRole.TextPrimary);
    private readonly TextBlock _line = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly ChartBar _bar = new() { Margin = new Thickness(0, FamilySpace.S2, 0, 0) };

    public DeviceItemView(IWidgetHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Padding = new Thickness(FamilySpace.Inset, FamilySpace.ContentTop, FamilySpace.Inset, 0);
        Background = Family.Brush(ColourRole.Clear);

        // [S-12831] An item of its widget: the layout dump and the item check see its box.
        ItemMark.Mark(this);

        _icon = new DeviceIconSlot(host);
        _icon.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);

        _readout.Use(FamilyType.Big28);
        _readout.HorizontalAlignment = HorizontalAlignment.Left;

        foreach (var text in new[] { _name, _line })
        {
            text.TextWrapping = TextWrapping.NoWrap;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.MaxLines = 1;
        }

        _name.VerticalAlignment = VerticalAlignment.Center;
        _name.Margin = new Thickness(FamilySpace.S3, 0, 0, 0);

        // The parts from the item's top: its box (the hover band) runs to the item's foot, its content keeps its own lines.
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(FamilyType.Big28.LineHeight) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(FamilyType.Caption.LineHeight) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Place(_icon, 0, 0);
        Place(_readout, 0, 1);
        Place(_name, 0, 2);
        Place(_line, 1, 1, 2);
        Place(_bar, 2, 0, 3);
    }

    /// <summary>The device it shows; null before the first.</summary>
    public DeviceItem? Item { get; private set; }

    /// <summary>
    /// [S-16631] Show <paramref name="item"/>; what is the same as before is not touched (a drive's picture is asked once).
    /// <paramref name="stock"/>: a drive other than this PC's system drive is drawn with its kind's stock icon (a render's
    /// drives are not this PC's).
    /// </summary>
    public void Show(DeviceItem item, bool stock)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;

        _icon.Show(item.Icon, stock);
        if (item.Reading is { } reading)
        {
            _readout.Visibility = Visibility.Visible;
            ColumnDefinitions[1].Width = new GridLength(ReadingWidth);
            _readout.Show(reading.Value, reading.Unit, valueRole: reading.Absent ? ColourRole.TextDisabled : ColourRole.TextPrimary);
            _name.Margin = new Thickness(FamilySpace.S3, 0, 0, 0);
        }
        else
        {
            // A device whose reading the widget does not draw: its name stands where the reading would.
            _readout.Visibility = Visibility.Collapsed;
            ColumnDefinitions[1].Width = GridLength.Auto;
            _name.Margin = new Thickness(0);
        }

        _name.Text = item.Name;
        _line.Text = item.Line;
        _bar.Show(Math.Clamp(item.Fill, 0, 1));
        _bar.Role(item.FillRole);
        ToolTipService.SetToolTip(this, item.Tooltip);
    }

    private void Place(FrameworkElement element, int row, int column, int span = 1)
    {
        SetRow(element, row);
        SetColumn(element, column);
        SetColumnSpan(element, span);
        Children.Add(element);
    }
}
