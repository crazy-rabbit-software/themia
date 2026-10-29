using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6733] A name on no card, straight over the wallpaper, as Windows' desktop draws its icons' names:
/// the family's <see cref="ColourRole.OnWallpaper"/> over its <see cref="ColourRole.OnWallpaperShadow"/>
/// cast <see cref="FamilyShape.TextShadowOffset"/> down and to the right, so it reads on a light or a
/// busy wallpaper as on a dark one. Drawn as two texts, not a composition shadow, so a render
/// (<c>RenderTargetBitmap</c>) shows what the desktop shows. Centred lines, wrapped on whole words,
/// cut with an ellipsis.
/// </summary>
public sealed partial class WallpaperLabel : Grid
{
    private readonly TextBlock _shadow;
    private readonly TextBlock _text;

    public WallpaperLabel(TypeStep step)
    {
        // The shadow is the text moved, not spaced: a translation, so the label's box is the text's own
        // and no margin outside the family's spacing steps appears in a render walk ([S-6731]).
        _shadow = Make(step, ColourRole.OnWallpaperShadow);
        _shadow.RenderTransform = new TranslateTransform { X = FamilyShape.TextShadowOffset, Y = FamilyShape.TextShadowOffset };
        _shadow.IsHitTestVisible = false;
        _text = Make(step, ColourRole.OnWallpaper);
        Children.Add(_shadow);
        Children.Add(_text);
    }

    /// <summary>The name.</summary>
    public string Text
    {
        get => _text.Text;
        set
        {
            _text.Text = value;
            _shadow.Text = value;
        }
    }

    /// <summary>At most this many lines (the last cut with an ellipsis).</summary>
    public int MaxLines
    {
        get => _text.MaxLines;
        set
        {
            _text.MaxLines = value;
            _shadow.MaxLines = value;
        }
    }

    /// <summary>The width the lines wrap at.</summary>
    public double TextMaxWidth
    {
        get => _text.MaxWidth;
        set
        {
            _text.MaxWidth = value;
            _shadow.MaxWidth = value;
        }
    }

    /// <summary>Whether the text is cut (for the full name on hover or selection, [S-6627]).</summary>
    public bool IsTrimmed => _text.IsTextTrimmed;

    private static TextBlock Make(TypeStep step, ColourRole role)
    {
        var text = Family.Text(step, role);
        text.TextAlignment = TextAlignment.Center;
        text.TextWrapping = TextWrapping.WrapWholeWords;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        return text;
    }
}
