using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4657] A drive's picture, as This PC draws it, on the family's icon sizes ([S-6704]: 16, 32, 48):
/// the shell's own icon for a local drive that is there (a disk, a stick, a disc), asked only of a
/// drive that is there and cached per drive and medium; the kind's glyph (<see cref="DriveRules.Glyph"/>,
/// secondary) until it is in, for a network drive, whose share is never asked for a picture (that is a
/// question to the server), and where the caller wants no picture (a render's fixed drives, which are
/// not this PC's).
/// </summary>
public sealed partial class DriveIcon : Grid
{
    private readonly FontIcon _glyph = Family.Glyph(string.Empty, FamilyShape.IconLarge, ColourRole.TextSecondary);
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private string? _key;

    public DriveIcon()
    {
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_glyph);
        Children.Add(_image);
    }

    /// <summary>
    /// Show <paramref name="drive"/> at <paramref name="size"/> (a family icon size); without
    /// <paramref name="picture"/> the kind's glyph alone.
    /// </summary>
    public void Show(DriveEntry drive, double size, bool picture = true)
    {
        ArgumentNullException.ThrowIfNull(drive);
        Width = size;
        Height = size;
        _glyph.FontSize = size;
        _glyph.Glyph = DriveRules.Glyph(drive);

        // The shell's picture for a local drive that is there; the key changes with the medium.
        var key = picture && drive.Kind != VolumeKind.Network && drive.State == DriveState.Ready
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

/// <summary>
/// [S-4653] A drive's capacity bar: the Chart block's bar ([S-6717], a 4 track) filled to the used
/// share in its role (<see cref="DriveRules.Role"/>); a drive without figures has none.
/// </summary>
public static class DriveBar
{
    public static ChartBar Create() => new();

    public static void Show(ChartBar bar, DriveEntry drive)
    {
        ArgumentNullException.ThrowIfNull(bar);
        ArgumentNullException.ThrowIfNull(drive);
        bar.Show(drive.UsedFraction ?? 0);
        bar.Role(DriveRules.Role(drive));
        bar.Visibility = drive.UsedFraction is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
