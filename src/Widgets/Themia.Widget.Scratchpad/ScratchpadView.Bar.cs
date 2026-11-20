using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Scratchpad;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1497] The ink bar along the foot of the pad: the original's toolbar of preset inks and three
/// nibs ([S-02] of the original), shown once the pad has room for it, and [S-1498] the eraser as a
/// tool beside them, then undo and full screen ([S-651], [S-12481]: no card header keeps them); the bar is the rung a larger
/// tile adds, so choosing an ink no longer means opening settings. Picking an ink or a nib also
/// leaves the eraser, as the original did. Its ladder is <see cref="ScratchpadLayout.Bar"/>.
/// <para>
/// The bar is a row of 28 tools on the card's bottom inset ([S-6716]), each a subtle button (the
/// subtle fills on hover and press, no border): an ink is a 16 dot in its own colour (a data colour,
/// [S-6705]), a nib a dot of its width in the secondary role, the chosen one ringed by a 24 ring of
/// stroke 2 in the accent (selected); the eraser on is the accent's fill with its glyph on it.
/// </para>
/// </summary>
internal sealed partial class ScratchpadView
{
    /// <summary>The ring round the chosen ink or nib.</summary>
    private const double ChoiceRing = FamilySpace.S6;

    /// <summary>
    /// [S-14267] The bar: the inks, the nibs and the eraser from its start, undo and full screen at its end (its spare room
    /// between them), so it runs across the card, its first and last glyphs on the inset.
    /// </summary>
    private readonly Grid _bar = new();
    private readonly StackPanel _inks = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly StackPanel _nibs = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly Border _barEraser;
    private readonly Grid _barRow = new();
    private readonly List<(ColorPreset Ink, Ellipse Ring)> _inkRings = [];
    private readonly List<(double Width, Ellipse Ring)> _nibRings = [];
    private readonly List<(Border Button, Func<string> Tip)> _barTips = [];

    private void BuildBar()
    {
        foreach (var ink in InkPalette.Inks)
        {
            var (button, mark) = Choice(InkBrush(ink.Hex), FamilyShape.Icon, ink.Name, () => PickInk(ink));
            _barTips.Add((button, () => ink.Name));
            _inkRings.Add((ink, mark));
            _inks.Children.Add(button);
        }

        foreach (var width in InkPalette.Widths)
        {
            // A nib is drawn a little over its width, so the finest still reads as a dot.
            var (button, mark) = Choice(Family.Brush(ColourRole.TextSecondary), width + FamilyShape.Stroke, NibTip(width), () => PickNib(width));
            _barTips.Add((button, () => NibTip(width)));
            _nibRings.Add((width, mark));
            _nibs.Children.Add(button);
        }

        var commands = new StackPanel { Orientation = Orientation.Horizontal, Children = { _undoButton, _fullButton } };
        var gap = new Thickness(ScratchpadLayout.GroupGap, 0, 0, 0);
        _nibs.Margin = gap;
        _barEraser.Margin = gap;
        commands.Margin = gap;
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
        {
            _bar.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        }

        FrameworkElement[] parts = [_inks, _nibs, _barEraser, commands];
        int[] columns = [0, 1, 2, 4];
        for (var i = 0; i < parts.Length; i++)
        {
            Grid.SetColumn(parts[i], columns[i]);
            _bar.Children.Add(parts[i]);
        }

        // [S-6716] [S-14267] On the card's bottom inset, across it: the first and last glyphs on the side insets.
        _barRow.Margin = new Thickness(FamilySpace.Inset - ScratchpadLayout.Overhang, 0, FamilySpace.Inset - ScratchpadLayout.Overhang, FamilySpace.Inset);
        _barRow.Visibility = Visibility.Collapsed;
        _barRow.Children.Add(_bar);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SetRow(_barRow, 1);
        Children.Add(_barRow);
    }

    private static string NibTip(double width)
        => ScratchpadStr.WidgetsC_Scratchpad_PenSizeTip.Format(("size", LocaleFormat.Number(width, 0)));

    /// <summary>[S-1110] The bar's tooltips again, in the language now current.</summary>
    private void ApplyBarTips()
    {
        foreach (var (button, tip) in _barTips)
        {
            ToolLine.Retip(button, tip());
        }
    }

    /// <summary>A round swatch or nib, with the accent's ring that marks the current choice.</summary>
    private static (Border Button, Ellipse Ring) Choice(Brush fill, double diameter, string tooltip, Action run)
    {
        var ring = new Ellipse
        {
            Width = ChoiceRing,
            Height = ChoiceRing,
            Stroke = Family.Brush(ColourRole.Accent),
            StrokeThickness = FamilyShape.Stroke,
            Visibility = Visibility.Collapsed,
        };

        var dot = new Ellipse { Width = diameter, Height = diameter, Fill = fill };
        var mark = new Grid { Width = ChoiceRing, Height = ChoiceRing, Children = { ring, dot } };
        return (Tool(mark, tooltip, run), ring);
    }

    /// <summary>One of the pad's tools: a 32 tool button ([S-6704], <see cref="ToolLine.Tool"/>).</summary>
    private static Border Tool(FrameworkElement content, string tooltip, Action run) => ToolLine.Tool(content, ScratchpadLayout.Tool, tooltip, run);

    /// <summary>A glyph tool (the eraser, undo, clear, the way back from full screen).</summary>
    private static Border GlyphTool(string glyph, string tooltip, Action run)
        => Tool(Family.Glyph(glyph, FamilyShape.Icon, ColourRole.TextPrimary), tooltip, run);

    private void PickInk(ColorPreset ink)
    {
        _config.PenColor = ink.Hex;

        // Choosing an ink is choosing to draw.
        SetEraser(false);
        UpdateBar();
        _surface?.RequestSave();
    }

    private void PickNib(double width)
    {
        _config.PenWidth = width;

        // Choosing a nib is choosing to draw, as an ink is.
        SetEraser(false);
        UpdateBar();
        _surface?.RequestSave();
    }

    /// <summary>Climb the bar's ladder for the current area. Called on a resize and a config change.</summary>
    private void LayBar()
    {
        if (IsFullScreen)
        {
            // [S-1499] Full screen shows the whole bar on its own toolbar.
            _nibs.Visibility = Visibility.Visible;
            UpdateBar();
            return;
        }

        // [S-11205] The bar is the Ink bar setting's, whole or not at all; the card is never narrower than it.
        _nibs.Visibility = Shown(_config.ShowTools);
        _barRow.Visibility = Shown(_config.ShowTools);

        // A hidden eraser must not leave the pad in erase mode with no way out of it.
        if (!_config.ShowTools)
        {
            SetEraser(false);
        }

        UpdateBar();
    }

    private void UpdateBar()
    {
        var nearest = InkPalette.NearestWidth(_config.EffectivePenWidth);

        foreach (var (ink, ring) in _inkRings)
        {
            ring.Visibility = Shown(InkPalette.Matches(ink, _config.EffectivePenColor));
        }

        foreach (var (width, ring) in _nibRings)
        {
            ring.Visibility = Shown(width == nearest);
        }

        ToolLine.SetOn(_barEraser, _eraserOn);
    }

    private static Visibility Shown(bool shown)
        => shown ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}
