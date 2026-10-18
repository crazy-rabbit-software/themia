using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Scratchpad;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1497] The ink bar along the foot of the pad: the original's toolbar of preset inks and three
/// nibs ([S-02] of the original), shown once the pad has room for it, and [S-1498] the eraser as a
/// tool beside them. The header keeps undo, clear and full screen ([S-651]), and the eraser while
/// the bar is not shown; the bar is the rung a larger tile adds, so choosing an ink no longer means
/// opening settings. Picking an ink or a nib also leaves the eraser, as the original did.
/// <para>
/// Its ladder: nothing on a tile Compact either way (the pad is the widget); the six inks and the
/// eraser from a Regular × Regular tile; the three nibs between them when the width holds all.
/// Nothing more on a larger tile (no stroke count): the rest is pad.
/// </para>
/// </summary>
internal sealed partial class ScratchpadView
{
    /// <summary>One bar button: a 28px target, the Fluent compact control height.</summary>
    private const double BarButton = 28;

    /// <summary>The pad never gives up more than this to the bar: a bar over a sliver is a toolbar, not a pad.</summary>
    private const double PadFloor = 64;

    private readonly StackPanel _bar = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1 };
    private readonly StackPanel _inks = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly StackPanel _nibs = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly Border _nibGap = new() { Width = Fluent.Space2 };
    private readonly ToggleButton _barEraser = ToggleCommand(Glyphs.EraseTool, ScratchpadStr.WidgetsC_Scratchpad_EraseStrokes.Text());
    private readonly Grid _barRow = new();
    private readonly List<(Ink Ink, Ellipse Ring)> _inkRings = [];
    private readonly List<(double Width, Ellipse Ring)> _nibRings = [];
    private readonly List<(Button Button, RoutedEventHandler Handler)> _barHandlers = [];
    private readonly List<(ToggleButton Button, RoutedEventHandler Handler)> _barToggles = [];
    private readonly List<(Button Button, Func<string> Tip)> _barTips = [];

    private WidgetArea _area;

    /// <summary>[S-651] The header has room for the eraser too (it shows there while the bar does not).</summary>
    private bool _eraserFits;

    /// <summary>[S-663] The card's word on how much room there is: header tools and the bar.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        ShedTools(area.Width);
        LayBar();
    }

    private void BuildBar()
    {
        var ring = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
        var nib = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray);

        foreach (var ink in InkPalette.Inks)
        {
            var (button, mark) = BarChoice(InkBrush(ink.Hex), 14, ring, ink.Name.Text());
            _barTips.Add((button, () => ink.Name.Text()));
            _inkRings.Add((ink, mark));
            _inks.Children.Add(button);
            OnBarClick(button, () => PickInk(ink));
        }

        foreach (var width in InkPalette.Widths)
        {
            var (button, mark) = BarChoice(nib, width + 2, ring, NibTip(width));
            _barTips.Add((button, () => NibTip(width)));
            _nibRings.Add((width, mark));
            _nibs.Children.Add(button);
            OnBarClick(button, () => PickNib(width));
        }

        // [S-1498] The eraser is a tool beside the nibs; it and the header's are one (OnEraserToggled).
        _barEraser.Width = BarButton;
        _barEraser.Height = BarButton;
        _barEraser.Padding = new Thickness(0);
        RoutedEventHandler onBarEraser = (_, _) => _eraser.IsChecked = _barEraser.IsChecked;
        _barEraser.Checked += onBarEraser;
        _barEraser.Unchecked += onBarEraser;
        _barToggles.Add((_barEraser, onBarEraser));

        _bar.Children.Add(_inks);
        _bar.Children.Add(_nibGap);
        _bar.Children.Add(_nibs);
        _bar.Children.Add(new Border { Width = Fluent.Space2 });
        _bar.Children.Add(_barEraser);

        _barRow.Margin = new Thickness(Fluent.Space2, 0, Fluent.Space3, Fluent.Space2);
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
            ToolTipService.SetToolTip(button, tip());
        }
    }

    /// <summary>A round swatch or nib with a hairline ring that marks the current choice.</summary>
    private static (Button Button, Ellipse Ring) BarChoice(Brush fill, double diameter, Brush ringBrush, string tooltip)
    {
        var ring = new Ellipse
        {
            Width = 20,
            Height = 20,
            Stroke = ringBrush,
            StrokeThickness = 1.5,
            Visibility = Visibility.Collapsed,
        };

        var dot = new Ellipse { Width = diameter, Height = diameter, Fill = fill };

        var button = CommandButton(ScratchpadModule.Glyph, tooltip);
        button.Width = BarButton;
        button.Height = BarButton;
        button.Padding = new Thickness(0);
        button.Content = new Grid { Width = 20, Height = 20, Children = { ring, dot } };

        return (button, ring);
    }

    private void OnBarClick(Button button, Action action)
    {
        RoutedEventHandler handler = (_, _) => action();
        button.Click += handler;
        _barHandlers.Add((button, handler));
    }

    private void DetachBar()
    {
        foreach (var (button, handler) in _barHandlers)
        {
            button.Click -= handler;
        }

        _barHandlers.Clear();

        foreach (var (button, handler) in _barToggles)
        {
            button.Checked -= handler;
            button.Unchecked -= handler;
        }

        _barToggles.Clear();
    }

    private void PickInk(Ink ink)
    {
        _config.PenColor = ink.Hex;

        // Choosing an ink is choosing to draw.
        _eraser.IsChecked = false;
        UpdateBar();
        _surface?.RequestSave();
    }

    private void PickNib(double width)
    {
        _config.PenWidth = width;

        // Choosing a nib is choosing to draw, as an ink is.
        _eraser.IsChecked = false;
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
            _nibGap.Visibility = Visibility.Visible;
            _eraser.Visibility = Visibility.Collapsed;
            UpdateBar();
            return;
        }

        // The bar's own run: the inks, a gap and the eraser (the stack's spacing between the three);
        // the nibs add a gap and themselves, two more spacings.
        var inksWidth = (InkPalette.Inks.Count * BarButton) + Fluent.Space2 + BarButton + (2 * Fluent.Space1);
        var nibsWidth = (InkPalette.Widths.Count * BarButton) + Fluent.Space2 + (2 * Fluent.Space1);
        var width = _area.Width - Fluent.Space2 - Fluent.Space3; // the row's margins

        var room = new Disclosure(_area.Deflate(Fluent.Space2, 0), Fluent.Space2);
        room.Reserve(PadFloor);

        var show = _config.ShowTools
            && _area.AtLeast(SizeClass.Regular, SizeClass.Regular)
            && width >= inksWidth
            && room.Take(BarButton, inksWidth);

        _nibs.Visibility = Shown(width >= inksWidth + nibsWidth);
        _nibGap.Visibility = _nibs.Visibility;
        _barRow.Visibility = Shown(show);

        // [S-1498] One eraser on screen: in the bar where it shows, else in the header where it fits.
        var headerEraser = !show && _eraserFits;
        _eraser.Visibility = Shown(headerEraser);

        // A hidden eraser must not leave the pad in erase mode with no way out of it.
        if (!show && !headerEraser && _eraser.IsChecked == true)
        {
            _eraser.IsChecked = false;
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
    }

    private static Visibility Shown(bool shown)
        => shown ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}
