using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Items;

/// <summary>
/// [S-16947] [S-16631] [S-4657] [S-15165] An item's lead in its slot (36 in a List item, the reading's line; 16 in a compact row
/// and a table; 32 in a column and a card): a drive's own picture as This PC draws it (the system icon of its root, asked once
/// per drive and medium; its kind's stock icon where the drive is a render's or not there), a person's monogram in its
/// ring, a picture only the widget draws (an app's icon, through <see cref="Own"/>), else the glyph in its role. The glyph
/// stands until a picture is in.
/// </summary>
public sealed partial class ItemLeadSlot : Grid
{
    private readonly IWidgetHost _host;
    private readonly IconSlot _slot;
    private readonly FontIcon _glyph;
    private readonly double _side;
    private Border? _monogram;
    private FrameworkElement? _own;
    private string? _key;

    /// <param name="side">The slot's side.</param>
    /// <param name="glyph">The glyph's size in it.</param>
    public ItemLeadSlot(IWidgetHost host, double side, double glyph)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _side = side;
        _slot = new IconSlot(side);
        _glyph = Family.Glyph(string.Empty, glyph);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        Width = side;
        Height = side;
        Children.Add(_glyph);
        Children.Add(_slot);
        _slot.Visibility = Visibility.Collapsed;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>A picture only the widget draws for a lead with a key (an app's icon), at the slot's side; null: the glyph.</summary>
    public Func<ItemLead, double, FrameworkElement?>? Own { get; set; }

    /// <summary>
    /// [S-16637] Whether it draws a picture (the ink of a face is its reading's then, as beside any picture); a glyph alone is
    /// measured as a glyph.
    /// </summary>
    public bool ShowsPicture => _key is not null;

    /// <summary>
    /// Show <paramref name="lead"/>; what is the same as before is not asked again. <paramref name="stock"/>: a drive other
    /// than this PC's system drive wears its kind's stock icon (a render's drives are not this PC's).
    /// </summary>
    public void Show(ItemLead lead, bool stock)
    {
        ArgumentNullException.ThrowIfNull(lead);
        _glyph.Glyph = lead.Glyph;
        _glyph.Foreground = Family.Brush(lead.Role);

        // [S-586] A monogram where it reads (a 16 slot holds no two initials: the glyph there).
        if (lead.Monogram is { Length: > 0 } initials && _side >= FamilyShape.IconLarge)
        {
            ShowMonogram(initials);
            return;
        }

        HideMonogram();
        if (lead.Key is { Length: > 0 } key && Own is { } own)
        {
            ShowOwn($"own|{key}", () => own(lead, _side));
            return;
        }

        // [S-16631] A drive that is not there and of no known kind has no picture (the shell gives none): its kind's glyph.
        if (lead.Drive is not { } drive || (drive.State == DriveState.Missing && DriveRules.StockIcon(drive) is null))
        {
            Plain();
            return;
        }

        // [S-15165] A render's drive that is not this PC's system drive wears its kind's stock icon (the shell's answer for a
        // letter this PC may not have changes from run to run); a drive that is not there too, as the shell gives none.
        var stocked = (stock && !string.Equals(drive.Root, SystemDrive.Root, StringComparison.OrdinalIgnoreCase)) || drive.State != DriveState.Ready;
        var stockIcon = stocked ? DriveRules.StockIcon(drive) : null;
        var driveKey = stocked ? $"{drive.Root}|{stockIcon}" : $"{drive.Root}|{drive.Label}|{drive.Total}";
        if (driveKey == _key)
        {
            return;
        }

        DropOwn();
        _key = driveKey;
        _slot.Reset(null);
        _slot.Visibility = Visibility.Visible;
        _glyph.Visibility = Visibility.Visible;
        const int system = 0x4;
        var pixels = (int)Math.Ceiling(_side * 4 * (XamlRoot?.RasterizationScale ?? 1));

        // An icon is decoration: the slot's load logs a failure and leaves the glyph standing.
        _ = LoadAsync(() => stockIcon is { } held
            ? _host.FileIcons.DriveIconAsync(held, pixels)
            : _host.FileIcons.IconAsync(new FolderEntry(drive.Letter, drive.Root, true, 0, default, false, system), pixels), driveKey);
    }

    /// <summary>The glyph alone.</summary>
    private void Plain()
    {
        DropOwn();
        if (_key is not null)
        {
            _key = null;
            _slot.Reset(null);
            _slot.Visibility = Visibility.Collapsed;
        }

        _glyph.Visibility = Visibility.Visible;
    }

    /// <summary>[S-586] A person's monogram: their initials in the secondary role on the subtle fill, round.</summary>
    private void ShowMonogram(string initials)
    {
        Plain();
        _glyph.Visibility = Visibility.Collapsed;
        if (_monogram is null)
        {
            var text = Family.Text(_side >= FamilyShape.IconLarge ? FamilyType.BodyStrong : FamilyType.Caption, ColourRole.TextSecondary);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.TextTrimming = TextTrimming.None;
            _monogram = new Border
            {
                Child = text,
                Width = _side,
                Height = _side,
                CornerRadius = Family.Round(_side, _side),
                Background = Family.Brush(ColourRole.Subtle),
            };
            Children.Add(_monogram);
        }

        ((TextBlock)_monogram.Child).Text = initials;
        _monogram.Visibility = Visibility.Visible;
    }

    private void HideMonogram()
    {
        if (_monogram is not null)
        {
            _monogram.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>The widget's own picture for the lead; the glyph under it until it draws.</summary>
    private void ShowOwn(string key, Func<FrameworkElement?> make)
    {
        if (key == _key && _own is not null)
        {
            return;
        }

        DropOwn();
        _slot.Reset(null);
        _slot.Visibility = Visibility.Collapsed;
        _key = key;
        _glyph.Visibility = Visibility.Visible;
        if (make() is { } own)
        {
            own.Width = _side;
            own.Height = _side;
            own.HorizontalAlignment = HorizontalAlignment.Center;
            own.VerticalAlignment = VerticalAlignment.Center;
            _own = own;
            Children.Add(own);
            _glyph.Visibility = Visibility.Collapsed;
        }
    }

    private void DropOwn()
    {
        if (_own is not null)
        {
            Children.Remove(_own);
            _own = null;
            if (_key?.StartsWith("own|", StringComparison.Ordinal) == true)
            {
                _key = null;
            }
        }
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
