using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6716] The Controls block: one row of controls at the card's bottom inset, 8 apart — at most one
/// main action in the accent (<see cref="Main"/>), standard buttons (<see cref="Button"/>), 32 icon
/// buttons (<see cref="Icon"/>) and a slider (<see cref="Slider"/>, its rail the Track role, its
/// value the accent) that takes the room left. On a narrow card the icon buttons give way from the
/// last, then the standard buttons, then the slider (<see cref="ControlsRules"/>); the main action stays.
/// No control takes focus: the surface does not activate outside edit mode ([S-800]).
/// </summary>
public sealed partial class ControlsBlock : Grid
{
    private readonly List<(ControlKind Kind, FrameworkElement Element)> _parts = [];

    public ControlsBlock()
    {
        VerticalAlignment = VerticalAlignment.Bottom;
        ColumnSpacing = ControlsRules.Gap;
    }

    /// <summary>Fold to the width the layout offers, in the measure pass, so the first frame is already right.</summary>
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            Fold(availableSize.Width);
        }

        return base.MeasureOverride(availableSize);
    }

    /// <summary>The one main action: a text button in the accent.</summary>
    public Button Main(string label, Action run) => Add(ControlKind.Main, TextButton(label, main: true, run));

    /// <summary>A standard text button.</summary>
    public Button Button(string label, Action run) => Add(ControlKind.Button, TextButton(label, main: false, run));

    /// <summary>A 32 × 32 icon button with a 16 glyph, its tooltip its name.</summary>
    public Button Icon(string glyph, string tooltip, Action run) => Add(ControlKind.Icon, IconButton(glyph, tooltip, run));

    /// <summary>A slider from <paramref name="minimum"/> to <paramref name="maximum"/>, taking the room left.</summary>
    public Slider Slider(double minimum, double maximum, double value, Action<double> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            VerticalAlignment = VerticalAlignment.Center,
            IsThumbToolTipEnabled = false,
            AllowFocusOnInteraction = false,
        };

        // The rail and the value are roles, not the template's literals.
        foreach (var state in new[] { string.Empty, "PointerOver", "Pressed" })
        {
            slider.Resources["SliderTrackFill" + state] = Family.Brush(ColourRole.Track);
            slider.Resources["SliderTrackValueFill" + state] = Family.Brush(ColourRole.Accent);
        }

        slider.ValueChanged += (_, args) => changed(args.NewValue);
        return Add(ControlKind.Slider, slider);
    }

    private T Add<T>(ControlKind kind, T element)
        where T : FrameworkElement
    {
        ControlsRules.Check([.. _parts.Select(p => new ControlPart(p.Kind, 0)), new ControlPart(kind, 0)]);
        ColumnDefinitions.Add(new ColumnDefinition { Width = kind == ControlKind.Slider ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        SetColumn(element, _parts.Count);
        _parts.Add((kind, element));
        Children.Add(element);
        return element;
    }

    private void Fold(double width)
    {
        var parts = _parts.Select(p => new ControlPart(p.Kind, PartWidth(p))).ToList();
        var shown = ControlsRules.Shown(width, parts);
        for (var i = 0; i < _parts.Count; i++)
        {
            var visibility = shown[i] ? Visibility.Visible : Visibility.Collapsed;
            if (_parts[i].Element.Visibility != visibility)
            {
                _parts[i].Element.Visibility = visibility;
                ColumnDefinitions[i].Width = !shown[i] ? new GridLength(0)
                    : _parts[i].Kind == ControlKind.Slider ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            }
        }
    }

    private readonly Dictionary<FrameworkElement, double> _widths = [];

    private double PartWidth((ControlKind Kind, FrameworkElement Element) part)
    {
        if (part.Kind == ControlKind.Slider)
        {
            return ControlsRules.MinSlider;
        }

        if (part.Element.Visibility == Visibility.Visible)
        {
            part.Element.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            _widths[part.Element] = part.Element.DesiredSize.Width;
        }

        return _widths.TryGetValue(part.Element, out var width) ? width : 0;
    }

    /// <summary>
    /// [S-6716] A text button: 32 tall, 12 either side, Body text, the one radius; the main action in
    /// the accent. Takes no focus.
    /// </summary>
    public static Button TextButton(string label, bool main, Action run)
    {
        ArgumentNullException.ThrowIfNull(run);
        // A text of its own on the Body step, so its line is the token's, not the template's; it takes
        // the button's foreground (the accent style's on-accent text) since it names none.
        var text = new TextBlock { Text = label, TextWrapping = TextWrapping.NoWrap };
        Family.Step(text, FamilyType.Body);
        var button = new Button
        {
            Content = text,
            Height = FamilyShape.Button,
            Padding = new Thickness(FamilyShape.ButtonPadding, 0, FamilyShape.ButtonPadding, 0),
            CornerRadius = Family.Radius,
            FontSize = FamilyType.Body.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            AllowFocusOnInteraction = false,
        };

        if (main)
        {
            FluentLayout.ApplyStyle(button, "AccentButtonStyle");
            button.Height = FamilyShape.Button;
            button.Padding = new Thickness(FamilyShape.ButtonPadding, 0, FamilyShape.ButtonPadding, 0);
            button.CornerRadius = Family.Radius;
        }

        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => run();
        return button;
    }

    /// <summary>[S-6716] An icon button: 32 × 32, a 16 glyph, the one radius, its tooltip its name. Takes no focus.</summary>
    public static Button IconButton(string glyph, string tooltip, Action run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var button = new Button
        {
            Content = Family.Glyph(glyph, FamilyShape.Icon, ColourRole.TextPrimary),
            Width = FamilyShape.IconButton,
            Height = FamilyShape.IconButton,
            Padding = new Thickness(0),
            CornerRadius = Family.Radius,
            VerticalAlignment = VerticalAlignment.Center,
            AllowFocusOnInteraction = false,
        };

        ToolTipService.SetToolTip(button, tooltip);
        AutomationProperties.SetName(button, tooltip);
        button.Click += (_, _) => run();
        return button;
    }
}
