using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Devices;

/// <summary>
/// [S-16631] [S-4657] [S-15165] A device item's icon at the reading's line (36): a drive's own picture as This PC draws it
/// (the system icon of its root, asked once per drive and medium), its kind's stock icon where the drive is a render's
/// (not this PC's system drive) or not there; a device's glyph at 28 in its role. The glyph stands until a picture is in.
/// </summary>
public sealed partial class DeviceIconSlot : Grid
{
    private readonly IWidgetHost _host;
    private readonly IconSlot _slot = new(FamilyType.Big28.LineHeight);
    private readonly FontIcon _glyph = Family.Glyph(string.Empty, FamilyType.Big28.FontSize);
    private string? _key;

    public DeviceIconSlot(IWidgetHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        Width = FamilyType.Big28.LineHeight;
        Height = FamilyType.Big28.LineHeight;
        Children.Add(_glyph);
        Children.Add(_slot);
        _slot.Visibility = Visibility.Collapsed;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>
    /// [S-16637] Whether it draws a drive's picture (the ink of a face is its reading's then, as beside any picture); a
    /// device's glyph alone is measured as a glyph.
    /// </summary>
    public bool ShowsPicture => _key is not null;

    /// <summary>
    /// Show <paramref name="icon"/>; what is the same as before is not asked again. <paramref name="stock"/>: a drive other
    /// than this PC's system drive wears its kind's stock icon (a render's drives are not this PC's).
    /// </summary>
    public void Show(DeviceIcon icon, bool stock)
    {
        ArgumentNullException.ThrowIfNull(icon);
        _glyph.Glyph = icon.Glyph;
        _glyph.Foreground = Family.Brush(icon.Role);
        // [S-16631] A drive that is not there and of no known kind has no picture (the shell gives none): its kind's glyph.
        if (icon.Drive is not { } drive || (drive.State == DriveState.Missing && DriveRules.StockIcon(drive) is null))
        {
            if (_key is not null)
            {
                _key = null;
                _slot.Reset(null);
                _slot.Visibility = Visibility.Collapsed;
                _glyph.Visibility = Visibility.Visible;
            }

            return;
        }

        // [S-15165] A render's drive that is not this PC's system drive wears its kind's stock icon (the shell's answer for a
        // letter this PC may not have changes from run to run); a drive that is not there too, as the shell gives none.
        var stocked = (stock && !string.Equals(drive.Root, SystemDrive.Root, StringComparison.OrdinalIgnoreCase)) || drive.State != DriveState.Ready;
        var stockIcon = stocked ? DriveRules.StockIcon(drive) : null;
        var key = stocked ? $"{drive.Root}|{stockIcon}" : $"{drive.Root}|{drive.Label}|{drive.Total}";
        if (key == _key)
        {
            return;
        }

        _key = key;
        _slot.Reset(null);
        _slot.Visibility = Visibility.Visible;
        _glyph.Visibility = Visibility.Visible;
        const int system = 0x4;
        var pixels = (int)Math.Ceiling(FamilyType.Big28.LineHeight * 4 * (XamlRoot?.RasterizationScale ?? 1));

        // An icon is decoration: the slot's load logs a failure and leaves the glyph standing.
        _ = LoadAsync(() => stockIcon is { } held
            ? _host.FileIcons.DriveIconAsync(held, pixels)
            : _host.FileIcons.IconAsync(new FolderEntry(drive.Letter, drive.Root, true, 0, default, false, system), pixels), key);
    }

    /// <summary>The picture laid in when it comes (the newest ask's): the glyph steps back for it.</summary>
    private async Task LoadAsync(Func<Task<Microsoft.UI.Xaml.Media.ImageSource?>> load, string key)
    {
        if (await _slot.LoadAsync(load).ConfigureAwait(true) && key == _key && _slot.Source is not null)
        {
            _glyph.Visibility = Visibility.Collapsed;
        }
    }
}
