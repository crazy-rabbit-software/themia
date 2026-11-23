using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Devices;

/// <summary>
/// [S-16637] One device's widget face (a Drive, a USB stick, a network drive, a Bluetooth device): the device item
/// ([S-16631]) as one block, planned at its natural box and scaled up as one (<see cref="ReadingFit"/>, WR-7), down to its
/// details at 9 (WR-14): left to right its icon, its name with its details right under it, its percentage at the end, the
/// bar under them all (owner, 2026-11-23: "from left to right: icon, then title and subtitle, then right aligned
/// percentage, under all of them the progress bar"). A part the device has none of, or the user left out, takes no room
/// and is not drawn ([S-16670]). The details stand in the box of their widest forms (<see cref="Hold"/>, WR-15).
/// </summary>
public sealed partial class DeviceFace : Grid
{
    private readonly ReadingFit _fit = new();
    private readonly SpreadStack _figure = new() { Justify = true };
    private readonly Grid _row = new();
    private readonly StackPanel _texts = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = Family.Text(FamilyType.Body, ColourRole.TextPrimary);
    private readonly TextBlock _line = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly ReadoutBlock _readout = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly DeviceIconSlot _icon;
    private readonly ChartBar _bar = new() { Margin = new Thickness(0, FamilySpace.S3, 0, 0) };
    private IReadOnlyList<string> _held = [];
    private string? _glyph;

    public DeviceFace(IWidgetHost host)
    {
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

        _row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _row.Height = FamilyType.Big28.LineHeight;
        Grid.SetColumn(_texts, 1);
        Grid.SetColumn(_readout, 2);
        _row.Children.Add(_icon);
        _row.Children.Add(_texts);
        _row.Children.Add(_readout);

        _figure.Children.Add(_row);
        _figure.Children.Add(_bar);
        _fit.Parts.Add(_figure);

        // [S-14301] The row and the bar run across the content from the start.
        _fit.Span(_figure);
        Children.Add(_fit);
    }

    /// <summary>[S-16637] Show <paramref name="item"/> without the parts <paramref name="hidden"/> leaves out.</summary>
    public void Show(DeviceItem item, IReadOnlyCollection<DevicePart> hidden, bool stock)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(hidden);
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

        _icon.Visibility = Shown(!hidden.Contains(DevicePart.Icon));
        var was = (_icon.ShowsPicture, _glyph);
        _icon.Show(item.Icon, stock);
        _glyph = item.Icon.Glyph;
        if (was != (_icon.ShowsPicture, _glyph))
        {
            // The icon's ink is another at the same size (a picture's or a glyph's, another glyph's): the block is fitted again.
            _fit.Refit();
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

        _fit.Refit();
    }

    private static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

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
        var line = _line;
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
