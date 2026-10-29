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
/// tool beside them. The header keeps undo and full screen ([S-651]); the bar is the rung a larger
/// tile adds, so choosing an ink no longer means opening settings. Picking an ink or a nib also
/// leaves the eraser, as the original did. Its ladder is <see cref="ScratchpadLayout.Bar"/>.
/// <para>
/// The bar is a row of 32 tools on the card's bottom inset ([S-6716]), each a subtle button (the
/// subtle fills on hover and press, no border): an ink is a 16 dot in its own colour (a data colour,
/// [S-6705]), a nib a dot of its width in the secondary role, the chosen one ringed by a 24 ring of
/// stroke 2 in the accent (selected); the eraser on is the accent's fill with its glyph on it.
/// </para>
/// </summary>
internal sealed partial class ScratchpadView
{
    /// <summary>The ring round the chosen ink or nib.</summary>
    private const double ChoiceRing = FamilySpace.S6;

    private readonly StackPanel _bar = new() { Orientation = Orientation.Horizontal, Spacing = ScratchpadLayout.GroupGap };
    private readonly StackPanel _inks = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly StackPanel _nibs = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly Border _barEraser;
    private readonly Grid _barRow = new();
    private readonly List<(ColorPreset Ink, Ellipse Ring)> _inkRings = [];
    private readonly List<(double Width, Ellipse Ring)> _nibRings = [];
    private readonly List<(Border Button, Func<string> Tip)> _barTips = [];

    private WidgetArea _area;

    /// <summary>[S-663] The card's word on how much room there is: the bar's ladder.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        LayBar();
    }

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

        _bar.Children.Add(_inks);
        _bar.Children.Add(_nibs);
        _bar.Children.Add(_barEraser);

        // [S-6716] On the card's bottom inset, from its start.
        _barRow.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
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
            Tip(button, tip());
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

    /// <summary>
    /// One of the pad's tools: a 32 subtle button ([S-6704]), the subtle fills on hover and press
    /// (a control's, as the header's buttons, never the item layer's), no border. A tap runs it.
    /// </summary>
    private static Border Tool(FrameworkElement content, string tooltip, Action run)
    {
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;

        var button = new Border
        {
            Child = content,
            Width = ScratchpadLayout.Tool,
            Height = ScratchpadLayout.Tool,
            CornerRadius = Family.Radius,
            Background = Family.Brush(ColourRole.Clear),
        };

        Tip(button, tooltip);
        button.PointerEntered += (_, _) => Rest(button, ColourRole.Subtle);
        button.PointerExited += (_, _) => Rest(button, ColourRole.Clear);
        button.PointerCanceled += (_, _) => Rest(button, ColourRole.Clear);
        button.PointerPressed += (_, _) => Rest(button, ColourRole.SubtlePressed);
        button.Tapped += (_, args) =>
        {
            args.Handled = true;
            if (button.Tag is not false)
            {
                run();
            }
        };

        // Two quick taps on a tool are two taps, not the card's double-click into edit mode.
        button.DoubleTapped += (_, args) => args.Handled = true;
        return button;
    }

    /// <summary>A glyph tool (the eraser, undo, clear, the way back from full screen).</summary>
    private static Border GlyphTool(string glyph, string tooltip, Action run)
        => Tool(Family.Glyph(glyph, FamilyShape.Icon, ColourRole.TextPrimary), tooltip, run);

    /// <summary>A tool's fill at rest or under the pointer; a tool that is on keeps the accent.</summary>
    private static void Rest(Border button, ColourRole role)
    {
        if (!IsOn(button))
        {
            button.Background = Family.Brush(role);
        }
    }

    private static bool IsOn(Border button) => ReferenceEquals(button.Background, Family.Brush(ColourRole.Accent));

    /// <summary>A toggled tool: on is the accent's fill with the glyph on it ([S-6706]).</summary>
    private static void On(Border button, bool on)
    {
        button.Background = Family.Brush(on ? ColourRole.Accent : ColourRole.Clear);
        if (button.Child is FontIcon glyph)
        {
            glyph.Foreground = Family.Brush(on ? ColourRole.TextOnAccent : ColourRole.TextPrimary);
        }
    }

    /// <summary>A tool with nothing to do now: its glyph in the disabled role (else <paramref name="rest"/>), a tap ignored.</summary>
    private static void Enable(Border button, bool enabled, ColourRole rest = ColourRole.TextPrimary)
    {
        button.Tag = enabled;
        if (button.Child is FontIcon glyph)
        {
            glyph.Foreground = Family.Brush(enabled ? rest : ColourRole.TextDisabled);
        }
    }

    private static void Tip(FrameworkElement element, string tip)
    {
        ToolTipService.SetToolTip(element, tip);
        AutomationProperties.SetName(element, tip);
    }

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

        var plan = ScratchpadLayout.Bar(_area, _config.ShowTools, InkPalette.Inks.Count, InkPalette.Widths.Count);
        _nibs.Visibility = Shown(plan.Nibs);
        _barRow.Visibility = Shown(plan.Bar);

        // A hidden eraser must not leave the pad in erase mode with no way out of it.
        if (!plan.Bar)
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

        On(_barEraser, _eraserOn);
    }

    private static Visibility Shown(bool shown)
        => shown ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}
