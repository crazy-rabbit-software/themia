using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.App.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-3581] The pictures a menu line shows in its icon column instead of a glyph, drawn from the
/// files beside <c>Themia.exe</c> at the column's size: Themia's mark (decoded logically, so a 16-DIP
/// picture is 32 pixels at 200 % and sharp) and the studio's (its SVG rasterised for the display).
/// Each is named for a screen reader.
/// </summary>
internal static class MenuPictures
{
    private const string Scope = "menu";

    /// <summary>The picture for <paramref name="picture"/>, <see cref="Fluent.IconSize"/> square.</summary>
    public static UIElement Create(MenuPicture picture)
    {
        var (file, name) = picture switch
        {
            MenuPicture.ThemiaLogo => ("themia-logo.png", Str.Shell_About_ThemiaLogo),
            MenuPicture.Lometics => ("lometics.svg", Str.Shell_About_LometicsLogo),
            _ => throw new ArgumentOutOfRangeException(nameof(picture), picture, null),
        };

        var uri = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", file));
        ImageSource source;
        if (file.EndsWith(".svg", StringComparison.Ordinal))
        {
            // Four times the column, so it is drawn down to every display scale up to 400 %.
            var svg = new SvgImageSource(uri) { RasterizePixelWidth = Fluent.IconSize * 4, RasterizePixelHeight = Fluent.IconSize * 4 };
            svg.OpenFailed += (_, args) => Log.Error(Scope, $"the menu picture {file} did not load: {args.Status}");
            source = svg;
        }
        else
        {
            var bitmap = new BitmapImage { DecodePixelType = DecodePixelType.Logical, DecodePixelWidth = (int)Fluent.IconSize, UriSource = uri };
            bitmap.ImageFailed += (_, args) => Log.Error(Scope, $"the menu picture {file} did not load: {args.ErrorMessage}");
            source = bitmap;
        }

        var image = new Image
        {
            Width = Fluent.IconSize,
            Height = Fluent.IconSize,
            Source = source,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(image, name.Text());
        return image;
    }
}
