using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2433] [S-441] [S-3965] An app's icon leading a row (the details' processes, the mixer's apps, the screen time's
/// apps): <c>fallback</c> (a glyph, a dot) at once and the file's own icon over it once it is extracted, asked only
/// when the row shows another file than before (rows are reused by position and lists reorder), and again when the
/// theme of <c>themed</c> (the row) changes (a packaged app's logo is chosen per theme). A row that has moved on while an icon loaded keeps its own.
/// </summary>
public sealed class AppIconLead(UIElement fallback, Func<FrameworkElement> themed, Func<int>? pixels = null)
{
    private string? _path;
    private LogoTheme _theme;

    /// <summary>The icon; the row lays it over <c>fallback</c> and sizes it.</summary>
    public Image Image { get; } = new() { Stretch = Stretch.Uniform };

    /// <summary>The row shows <paramref name="path"/>'s app now; null: the fallback alone.</summary>
    public void Show(string? path)
    {
        if (path is null)
        {
            _path = null;
            Image.Source = null;
            fallback.Visibility = Visibility.Visible;
            return;
        }

        if (string.Equals(_path, path, StringComparison.OrdinalIgnoreCase) && _theme == AppIconSource.ThemeOf(themed()))
        {
            return;
        }

        _path = path;
        Image.Source = null;
        fallback.Visibility = Visibility.Visible;
        _ = LoadAsync(path);
    }

    /// <summary>[S-3965] The theme changed: a packaged app's logo is asked for again.</summary>
    public void ThemeChanged()
    {
        if (_path is { } path && _theme != AppIconSource.ThemeOf(themed()))
        {
            _ = LoadAsync(path);
        }
    }

    private async Task LoadAsync(string path)
    {
        var theme = AppIconSource.ThemeOf(themed());
        _theme = theme;
        var image = pixels is { } size
            ? await AppIconSource.GetAsync(path, size(), theme).ConfigureAwait(true)
            : await AppIconSource.GetAsync(path, theme).ConfigureAwait(true);

        if (image is null || !string.Equals(_path, path, StringComparison.OrdinalIgnoreCase) || _theme != theme)
        {
            return;
        }

        Image.Source = image;
        fallback.Visibility = Visibility.Collapsed;
    }
}
