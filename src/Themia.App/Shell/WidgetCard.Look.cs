using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell;

/// <summary>
/// [S-2133] [S-2137] [S-2140] The card's share of the look tokens (<see cref="LookTokens"/>) past its fill, border and
/// glass: the accent mixed into its default card and the grain over its material.
/// The rest of the tokens reach the card through the family (its text levels, type, tracks and rows) and the item layer
/// (its surfaces); the glass's through <see cref="LiquidGlass.For(bool, LookTokens, RgbColor?)"/>.
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>[S-2140] The grain: one picture shared by every card, drawn unscaled from the card's corner, over the material and under the content.</summary>
    private readonly Border _grain = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    /// <summary>[S-2140] The grain's picture, made once (<see cref="LookRules.Grain"/>). UI thread only.</summary>
    private static ImageBrush? _grainBrush;

    private static ImageBrush GrainBrush()
    {
        if (_grainBrush is null)
        {
            var size = LookRules.GrainSize;
            var bitmap = new WriteableBitmap(size, size);
            LookRules.Grain(size).CopyTo(bitmap.PixelBuffer);
            bitmap.Invalidate();
            _grainBrush = new ImageBrush
            {
                ImageSource = bitmap,
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
            };
        }

        return _grainBrush;
    }

    /// <summary>
    /// [S-2133] [S-2140] After the fill and the glass are painted: a default solid card mixed toward the accent by its
    /// share (<see cref="LookRules.AccentShare"/>; glass takes it in its tint), and the grain at the design's strength.
    /// </summary>
    private void ApplyLook(CornerRadius radius, CardFill fill)
    {
        var tokens = _host.Design.Tokens ?? new LookTokens();
        var share = LookRules.AccentShare(tokens);
        if (!Glass && share > 0 && fill.Kind == FillKind.Default && fill.Painted && fill.Colour is { } card)
        {
            var mixed = LookRules.AccentTinted(card, DesignApplier.Accent, share);
            _fill.Background = DesignApplier.UserBrush(HexColor.From(mixed.R, mixed.G, mixed.B));
        }

        var grain = LookTokens.Number("grain")!.Clamp(tokens.Grain);
        var shows = grain > 0 && fill.Painted && (!_hidden || _host.EditMode);
        _grain.Visibility = shows ? Visibility.Visible : Visibility.Collapsed;
        if (shows)
        {
            _grain.CornerRadius = radius;
            _grain.Background = GrainBrush();
            _grain.Opacity = grain;
        }
        else
        {
            _grain.Background = null;
        }
    }
}
