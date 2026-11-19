using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Licensing;
using Themia.Core.Localization;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using A = Themia.Sdk.Kit.ShellMetrics.AddPage;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>
/// [S-13602] The add page's preview pane, beside its tiles: the tile pointed at or on the keyboard (else the
/// first shown) as the thing itself, large (<see cref="AddPictures"/>: real cards on sample data; the tile's
/// glyph until it is drawn), its name, what it is, and the free plan's limit where placing it would pass it.
/// After Karpo's add page (karpo-app, docs/widgets.md §593), whose pick under the pointer shows itself as a
/// ghost where it goes: Themia's widget has no place until it is placed, so it shows here. Nothing in it
/// acts; the tiles say everything to a screen reader, so the pane is not read out twice.
/// </summary>
internal sealed partial class AddPreview : Grid
{
    private readonly Grid _box;
    private readonly FontIcon _glyph;
    private readonly TextBlock _name;
    private readonly TextBlock _caption;
    private readonly TextBlock _about;
    private readonly TextBlock _limit;
    private Image? _image;

    public AddPreview()
    {
        Background = ShellBrush.Of(ShellRole.CardFill);
        BorderBrush = ShellBrush.Of(ShellRole.CardStroke);
        BorderThickness = new Thickness(FamilyShape.Hairline);
        CornerRadius = new CornerRadius(P.CardRadius);
        Padding = new Thickness(A.PreviewPad);
        VerticalAlignment = VerticalAlignment.Top;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);

        _glyph = Fluent.Icon(Glyphs.Apps, "TextFillColorTertiaryBrush", A.PreviewGlyph);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        _box = new Grid { Height = A.PreviewPicture, Children = { _glyph } };
        _box.SizeChanged += (_, _) => Fit();

        _name = Fluent.Text(FamilyType.BodyStrong);
        _name.TextWrapping = TextWrapping.Wrap;
        _name.Margin = new Thickness(0, A.PreviewPad, 0, 0);

        _caption = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
        _caption.TextWrapping = TextWrapping.Wrap;

        _about = Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush");
        _about.TextWrapping = TextWrapping.Wrap;
        _about.Margin = new Thickness(0, Fluent.Space1, 0, 0);

        _limit = Fluent.Text(FamilyType.Caption);
        _limit.Foreground = ShellBrush.Of(ShellRole.AccentText);
        _limit.TextWrapping = TextWrapping.Wrap;
        _limit.Margin = new Thickness(0, Fluent.Space2, 0, 0);

        Children.Add(new StackPanel { Children = { _box, _name, _caption, _about, _limit } });
        Clear();
    }

    /// <summary>The tile shown, by its key; null with none.</summary>
    public string? Key { get; private set; }

    /// <summary>[S-13602] Show <paramref name="tile"/>: its glyph until its picture comes (<see cref="ShowPicture"/>).</summary>
    public void Show(AddTile tile, bool overLimit)
    {
        ArgumentNullException.ThrowIfNull(tile);
        Key = tile.Key;
        var words = AddTileWords.Of(tile);
        Opacity = 1;
        DropImage();
        _glyph.Glyph = words.Glyph;
        _glyph.Visibility = Visibility.Visible;
        _name.Text = words.Name;
        Line(_caption, words.Caption == words.About ? string.Empty : words.Caption);
        Line(_about, words.About);
        Line(_limit, overLimit ? Str.AddMenu_OverLimit.Format(("count", LocaleFormat.Integer(LicenseRules.FreeWidgetLimit))) : string.Empty);
    }

    /// <summary>[S-13602] Nothing to show (nothing matched): the pane keeps its place, empty.</summary>
    public void Clear()
    {
        Key = null;
        DropImage();
        _glyph.Visibility = Visibility.Collapsed;
        _name.Text = string.Empty;
        Line(_caption, string.Empty);
        Line(_about, string.Empty);
        Line(_limit, string.Empty);
        Opacity = 0;
    }

    /// <summary>[S-13602] The shown tile's picture, fitted into the box, scaled down, never up, centred.</summary>
    public void ShowPicture(AddPicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        DropImage();
        _image = new Image
        {
            Source = picture.Image,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = picture,
        };
        _glyph.Visibility = Visibility.Collapsed;
        _box.Children.Add(_image);
        Fit();
    }

    private void Fit()
    {
        if (_image is not { Tag: AddPicture picture } image)
        {
            return;
        }

        var width = _box.ActualWidth > 0 ? _box.ActualWidth : A.Preview - (2 * A.PreviewPad);
        var scale = AddPageLayout.Fit(picture.Width, picture.Height, width, A.PreviewPicture);
        image.Width = Math.Round(picture.Width * scale);
        image.Height = Math.Round(picture.Height * scale);
    }

    private void DropImage()
    {
        if (_image is not null)
        {
            _box.Children.Remove(_image);
            _image = null;
        }
    }

    private static void Line(TextBlock line, string text)
    {
        line.Text = text;
        line.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>[S-11702] [S-13601] What a tile says: its glyph, its name, its caption line and, for the preview, what it is.</summary>
internal readonly record struct AddTileWords(string Glyph, string Name, string Caption, string About)
{
    public static AddTileWords Of(AddTile tile) => tile switch
    {
        AddWidgetTile one => Widget(one.Kind),
        AddGroupTile group => new(group.Group.Glyph, group.Group.Name.Text(), Str.Groups_Add_Count.Plural(group.Group.Widgets.Count), group.Group.About.Text()),
        AddItemTile item => new(ItemGlyph(item.Item.Kind), item.Item.Name, Str.Desktop_Add_Folder.Text(), string.Empty),
        _ => new(Glyphs.Apps, string.Empty, string.Empty, string.Empty),
    };

    private static AddTileWords Widget(WidgetId kind)
    {
        var descriptor = ModuleCatalog.Of(kind);
        var about = descriptor.Describe();
        return new(descriptor.Glyph, descriptor.TileName, about, about);
    }

    /// <summary>[S-5523] A desktop item's glyph: what it is, as Explorer's own icons say it.</summary>
    private static string ItemGlyph(DesktopItemKind kind) => kind switch
    {
        DesktopItemKind.Folder => Glyphs.Folder,
        DesktopItemKind.Shortcut => Glyphs.Link,
        DesktopItemKind.ShellItem => Glyphs.Display,
        _ => Glyphs.File,
    };
}
