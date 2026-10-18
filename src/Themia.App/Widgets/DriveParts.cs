using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4657] A drive's picture, as This PC draws it: the shell's own icon for a local drive (a disk, a
/// stick, a disc), asked only of a drive that is there and cached per drive and medium; the kind's
/// glyph until it is in and for a network drive, whose share is never asked for a picture (that is a
/// question to the server). A mapped drive Windows cannot reach wears Explorer's red cross at its foot.
/// </summary>
internal sealed partial class DriveIcon : Grid
{
    private readonly FontIcon _glyph = new();
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly Grid _badge = new();
    private string? _key;

    public DriveIcon()
    {
        _glyph.Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.VerticalAlignment = VerticalAlignment.Center;

        // Explorer's disconnected drive: a red disc with a cross, in the theme's critical colour and
        // the surface's own base under the cross, so it reads in either theme.
        _badge.HorizontalAlignment = HorizontalAlignment.Right;
        _badge.VerticalAlignment = VerticalAlignment.Bottom;
        _badge.Children.Add(new Ellipse { Fill = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.Red) });
        _badge.Children.Add(new FontIcon
        {
            Glyph = Glyphs.Dismiss,
            Foreground = ThemeBrush.Of("SolidBackgroundFillColorBaseBrush", Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        _badge.Visibility = Visibility.Collapsed;

        Children.Add(_glyph);
        Children.Add(_image);
        Children.Add(_badge);
    }

    /// <summary>Show <paramref name="drive"/> at <paramref name="size"/> DIPs.</summary>
    public void Show(DriveEntry drive, double size)
    {
        Width = size;
        Height = size;
        _glyph.FontSize = Math.Round(size * 0.8);
        _glyph.Glyph = drive.Kind switch
        {
            VolumeKind.Network => Glyphs.NetworkDrive,
            VolumeKind.Removable => Glyphs.Usb,
            VolumeKind.Optical => Glyphs.Disc,
            _ => Glyphs.Disk,
        };

        var badge = Math.Max(8, Math.Round(size * 0.45));
        _badge.Width = badge;
        _badge.Height = badge;
        ((FontIcon)_badge.Children[1]).FontSize = Math.Max(6, Math.Round(badge * 0.55));
        _badge.Visibility = drive.State == DriveState.Disconnected ? Visibility.Visible : Visibility.Collapsed;

        // The shell's picture for a local drive that is there; the key changes with the medium.
        var key = drive.Kind != VolumeKind.Network && drive.State == DriveState.Ready
            ? $"{drive.Root}|{drive.Label}|{drive.Total}|{Math.Round(size)}"
            : null;

        if (key == _key)
        {
            return;
        }

        _key = key;
        _image.Source = null;
        _glyph.Visibility = Visibility.Visible;

        if (key is not null)
        {
            _ = LoadAsync(drive, size, key);
        }
    }

    private async Task LoadAsync(DriveEntry drive, double size, string key)
    {
        var scale = XamlRoot?.RasterizationScale ?? 2;
        var image = await AppIconSource.GetAsync(drive.Root, (int)Math.Ceiling(size * Math.Max(1, scale)), AppIconSource.ThemeOf(this), key).ConfigureAwait(true);

        // A drive that changed while this was extracted keeps its own picture.
        if (image is null || _key != key)
        {
            return;
        }

        _image.Source = image;
        _glyph.Visibility = Visibility.Collapsed;
    }
}

/// <summary>[S-4653] A drive's capacity bar: a slider-style track ([S-837]) filled in the load's colour.</summary>
internal static class DriveBar
{
    public static ProgressBar Create() => new() { Minimum = 0, Maximum = 1 };

    public static void Show(ProgressBar bar, DriveEntry drive, double thickness)
    {
        bar.Height = thickness;
        bar.MinHeight = thickness;
        bar.Value = drive.UsedFraction ?? 0;
        bar.Foreground = ThemeBrush.Of(DriveRules.Pressure(drive) switch
        {
            MeterPressure.Critical => "SystemFillColorCriticalBrush",
            MeterPressure.Elevated => "SystemFillColorCautionBrush",
            _ => "AccentFillColorDefaultBrush",
        }, Colors.DodgerBlue);
        bar.Visibility = drive.UsedFraction is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
