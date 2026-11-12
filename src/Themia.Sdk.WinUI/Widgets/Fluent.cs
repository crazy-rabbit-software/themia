using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// The Windows 11 design language, as the few values and helpers a widget actually needs.
/// <para>
/// This exists because the port had inherited the original web app's look — arbitrary font sizes,
/// arbitrary opacities standing in for text colours, 6px and 7px paddings, a divider between every
/// row — and the result read as a web dashboard that happened to be running on Windows. Fluent is
/// specific: a type ramp, a 4px spacing grid, two corner radii, named text and fill colours, and a
/// list row of a known height. Matching it is mostly a matter of stopping inventing values.
/// </para>
/// </summary>
public static class Fluent
{
    // ---- spacing ([S-6703]: the family's scale, FamilySpace, is the one place these are set) ----

    /// <summary>The 4px grid Fluent lays out on. Every gap a widget uses is one of these.</summary>
    public const double Space1 = FamilySpace.S1;

    public const double Space2 = FamilySpace.S2;

    public const double Space3 = FamilySpace.S3;

    public const double Space4 = FamilySpace.S4;

    public const double Space6 = FamilySpace.S6;

    // ---- shape -----------------------------------------------------------------------------

    /// <summary>Fluent's control radius. Buttons, text fields, row highlights.</summary>
    public const double ControlRadius = FamilyShape.Radius;

    // ---- type ------------------------------------------------------------------------------

    /// <summary>Apply a ramp step to a text block: size, line height and weight together.</summary>
    public static void Apply(this TextBlock text, TypeStep step)
    {
        text.FontSize = step.FontSize;
        text.LineHeight = step.LineHeight;
        text.FontWeight = step.SemiBold ? FontWeights.SemiBold : FontWeights.Normal;
    }

    /// <summary>A text block on a ramp step, in one of the named text colours.</summary>
    public static TextBlock Text(TypeStep step, string colorKey = "TextFillColorPrimaryBrush")
    {
        var text = new TextBlock
        {
            Foreground = ThemeBrush.Of(colorKey, Colors.White),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };

        text.Apply(step);
        return text;
    }

    /// <summary>A 1px rule. Every tick, hairline and divider in the app is this thickness.</summary>
    public const double Hairline = FamilyShape.Hairline;

    /// <summary>The icon size Windows pairs with body text.</summary>
    public const double IconSize = FamilyShape.Icon;

    /// <summary>Fluent draws a chart line at 2px: 1px reads as a hairline artefact, 3px as a border.</summary>
    public const double ChartStroke = FamilyShape.Stroke;

    /// <summary>
    /// The alpha of an area wash under a chart line.
    /// <para>
    /// The one place an opacity is legitimate rather than a colour stand-in: it is applied to the
    /// <i>shape</i>, never to the shared accent brush, which is the same object every other accent
    /// surface in the app paints with.
    /// </para>
    /// </summary>
    public const double ChartWash = FamilyShape.Wash;

    // ---- pieces ----------------------------------------------------------------------------

    /// <summary>A 16px icon in one of the named text colours.</summary>
    public static FontIcon Icon(string glyph, string colorKey = "TextFillColorSecondaryBrush", double size = IconSize)
        => UiLanguage.Mirrored(new FontIcon
        {
            Glyph = glyph,
            FontSize = size,
            Foreground = ThemeBrush.Of(colorKey, Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center,
        });

    /// <summary>
    /// Light a row while the pointer is over it, and reliably put it back.
    /// <para>
    /// Four events, not one: <c>PointerExited</c> alone misses the cases where the pointer leaves
    /// without one — the list scrolls under it, the widget is torn down mid-hover, a drag takes the
    /// capture — each of which strands a row lit. Dispose the result to unhook; a view's
    /// <c>Detach</c> must, or the handlers outlive the row.
    /// </para>
    /// </summary>
    public static IDisposable Hover(Border row) => new HoverHandler(row);

    private sealed class HoverHandler : IDisposable
    {
        private readonly Border _row;
        private bool _disposed;

        public HoverHandler(Border row)
        {
            _row = row;
            _row.PointerEntered += OnEntered;
            _row.PointerExited += OnGone;
            _row.PointerCaptureLost += OnGone;
            _row.PointerCanceled += OnGone;
        }

        private void OnEntered(object sender, PointerRoutedEventArgs args) => ItemFill.Paint(_row, ItemRole.Row, ItemState.Hover);

        private void OnGone(object sender, PointerRoutedEventArgs args) => ItemFill.Paint(_row, ItemRole.Row);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _row.PointerEntered -= OnEntered;
            _row.PointerExited -= OnGone;
            _row.PointerCaptureLost -= OnGone;
            _row.PointerCanceled -= OnGone;
            ItemFill.Paint(_row, ItemRole.Row);
        }
    }

    /// <summary>
    /// A clear fill that still takes the pointer, for a surface that is not an item (a list's
    /// scroll content, a whole view's hit area): never the item layer's, which would fill it.
    /// </summary>
    public static Brush Clear() => ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);

    /// <summary>
    /// The theme brush named <paramref name="key"/> (a <c>…Brush</c> resource), for parts that may
    /// name no colour themselves (the kit's, [S-7066]); the theme has every key they use.
    /// </summary>
    public static Brush Brush(string key) => ThemeBrush.Of(key, Colors.Gray);
}
