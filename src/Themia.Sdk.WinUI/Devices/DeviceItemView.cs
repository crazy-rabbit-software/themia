using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Devices;

/// <summary>
/// [S-16630] [S-16631] One device item, the device family's one face (owner, 2026-11-23: "from left to right: icon, then
/// title and subtitle, then right aligned percentage, under all of them the progress bar"). In the card's inset: the
/// device's icon (36: a drive's own picture or the kind's glyph at 28), 8, its name (Body, primary, left-aligned, cut with
/// its ellipsis) with its details right under it (Caption, secondary), 12, its percentage at 28 at the item's end; 12 under
/// that row the bar (the Chart block's 4 track) across the item, the inset under it. A part the device has none of, or the
/// user left out ([S-16670]), takes no room and is not drawn ("it should also not show them in the design, not just grey
/// them out"): the others close up. The item is its box edge to edge (WR-3): the list lights it under the pointer.
/// </summary>
public sealed partial class DeviceItemView : Grid
{
    private readonly DeviceIconSlot _icon;
    private readonly StackPanel _texts = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = Family.Text(FamilyType.Body, ColourRole.TextPrimary);
    private readonly TextBlock _line = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly ReadoutBlock _readout = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly ChartBar _bar = new() { Margin = new Thickness(0, FamilySpace.S3, 0, 0) };

    public DeviceItemView(IWidgetHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Padding = new Thickness(FamilySpace.Inset, FamilySpace.ContentTop, FamilySpace.Inset, 0);
        Background = Family.Brush(ColourRole.Clear);

        // [S-12831] An item of its widget: the layout dump and the item check see its box.
        ItemMark.Mark(this);

        _icon = new DeviceIconSlot(host) { Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0) };
        _readout.Use(FamilyType.Big28);
        _readout.Margin = new Thickness(FamilySpace.S3, 0, 0, 0);
        foreach (var text in new[] { _name, _line })
        {
            text.TextWrapping = TextWrapping.NoWrap;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.MaxLines = 1;
            _texts.Children.Add(text);
        }

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(FamilyType.Big28.LineHeight) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Place(_icon, 0, 0);
        Place(_texts, 0, 1);
        Place(_readout, 0, 2);
        Place(_bar, 1, 0, 3);
    }

    /// <summary>The device it shows; null before the first.</summary>
    public DeviceItem? Item { get; private set; }

    /// <summary>
    /// [S-16631] Show <paramref name="item"/> without the parts <paramref name="hidden"/> leaves out; what is the same as
    /// before is not touched (a drive's picture is asked once). <paramref name="stock"/>: a drive other than this PC's system
    /// drive is drawn with its kind's stock icon (a render's drives are not this PC's).
    /// </summary>
    public void Show(DeviceItem item, IReadOnlyCollection<DevicePart> hidden, bool stock)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(hidden);
        Item = item;

        _icon.Visibility = Shown(!hidden.Contains(DevicePart.Icon));
        _icon.Show(item.Icon, stock);

        _name.Text = item.Name;
        _name.Visibility = Shown(item.Name.Length > 0);
        _line.Text = item.Line;
        _line.Visibility = Shown(item.Line.Length > 0 && (!hidden.Contains(DevicePart.Details) || item.Name.Length == 0));

        if (item.Reading is { } reading && !hidden.Contains(DevicePart.Reading))
        {
            _readout.Visibility = Visibility.Visible;
            _readout.Show(reading.Value, reading.Unit);
        }
        else
        {
            _readout.Visibility = Visibility.Collapsed;
        }

        if (item.Fill is { } fill && !hidden.Contains(DevicePart.Bar))
        {
            _bar.Visibility = Visibility.Visible;
            _bar.Show(Math.Clamp(fill, 0, 1));
            _bar.Role(item.FillRole);
        }
        else
        {
            _bar.Visibility = Visibility.Collapsed;
        }

        ToolTipService.SetToolTip(this, item.Tooltip);
    }

    private static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

    private void Place(FrameworkElement element, int row, int column, int span = 1)
    {
        SetRow(element, row);
        SetColumn(element, column);
        SetColumnSpan(element, span);
        Children.Add(element);
    }
}
