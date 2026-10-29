using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6716] The Controls block: one row of controls at the card's bottom inset, 8 apart — at most one
/// main action in the accent (<see cref="Main"/>, or <see cref="MainIcon"/> for a transport's Play),
/// standard buttons (<see cref="Button"/>), 32 icon buttons (<see cref="Icon"/>), a slider
/// (<see cref="Slider"/>, its rail the Track role, its value the accent) that takes the room left and
/// an on/off switch (<see cref="Switch"/>). On a narrow card the icon buttons give way from the last,
/// then the standard buttons, then the slider (<see cref="ControlsRules"/>); the main action and the
/// switch stay.
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

    /// <summary>The one main action as a 32 × 32 icon button in the accent (a transport's Play), its tooltip its name.</summary>
    public Button MainIcon(string glyph, string tooltip, Action run) => Add(ControlKind.Main, IconButton(glyph, tooltip, run, main: true));

    /// <summary>
    /// An on/off switch (Windows' toggle switch, 40 wide, no words of its own: its name is its
    /// tooltip). <paramref name="changed"/> runs on the person's toggle only, never on <see cref="Set"/>.
    /// </summary>
    public ToggleSwitch Switch(bool on, string name, Action<bool> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var toggle = new ToggleSwitch
        {
            IsOn = on,
            OnContent = string.Empty,
            OffContent = string.Empty,
            MinWidth = 0,
            Width = ControlsRules.SwitchWidth,

            // The template's own row is taller than the Controls row: the row stays 32 (its track is 20).
            MinHeight = 0,
            Height = FamilyShape.Button,
            VerticalAlignment = VerticalAlignment.Center,
            AllowFocusOnInteraction = false,
            IsTabStop = false,
        };

        ToolTipService.SetToolTip(toggle, name);
        AutomationProperties.SetName(toggle, name);
        toggle.Toggled += (_, _) =>
        {
            if (!_setting.Contains(toggle))
            {
                changed(toggle.IsOn);
            }
        };

        return Add(ControlKind.Switch, toggle);
    }

    private readonly HashSet<ToggleSwitch> _setting = [];

    /// <summary>Show <paramref name="on"/> on a switch of this row without running its handler (a state read back, not a person's toggle).</summary>
    public void Set(ToggleSwitch toggle, bool on)
    {
        ArgumentNullException.ThrowIfNull(toggle);
        if (toggle.IsOn == on)
        {
            return;
        }

        _setting.Add(toggle);
        try
        {
            toggle.IsOn = on;
        }
        finally
        {
            _setting.Remove(toggle);
        }
    }

    /// <summary>A text button's new label (Start → Pause), its automation name with it.</summary>
    public static void Relabel(Button button, string label)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (button.Content is TextBlock text && text.Text != label)
        {
            text.Text = label;
            AutomationProperties.SetName(button, label);
        }
    }

    /// <summary>An icon button's new glyph and name (Play → Pause).</summary>
    public static void Reglyph(Button button, string glyph, string tooltip)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (button.Content is FontIcon icon && icon.Glyph != glyph)
        {
            icon.Glyph = glyph;
            ToolTipService.SetToolTip(button, tooltip);
            AutomationProperties.SetName(button, tooltip);
        }
    }

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

        if (part.Kind == ControlKind.Switch)
        {
            return ControlsRules.SwitchWidth;
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

    /// <summary>
    /// [S-6716] An icon button: 32 × 32, a 16 glyph, the one radius, its tooltip its name; the
    /// <paramref name="main"/> action's in the accent with the glyph on it. Takes no focus.
    /// </summary>
    public static Button IconButton(string glyph, string tooltip, Action run, bool main = false)
    {
        ArgumentNullException.ThrowIfNull(run);
        var button = new Button
        {
            // The main action's glyph names no brush: it takes the accent style's on-accent foreground.
            Content = main ? UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = FamilyShape.Icon, Width = FamilyShape.Icon, Height = FamilyShape.Icon, VerticalAlignment = VerticalAlignment.Center }) : Family.Glyph(glyph, FamilyShape.Icon, ColourRole.TextPrimary),
            Width = FamilyShape.IconButton,
            Height = FamilyShape.IconButton,
            Padding = new Thickness(0),
            CornerRadius = Family.Radius,
            VerticalAlignment = VerticalAlignment.Center,
            AllowFocusOnInteraction = false,
        };

        if (main)
        {
            FluentLayout.ApplyStyle(button, "AccentButtonStyle");
            button.Width = FamilyShape.IconButton;
            button.Height = FamilyShape.IconButton;
            button.Padding = new Thickness(0);
            button.CornerRadius = Family.Radius;
        }

        ToolTipService.SetToolTip(button, tooltip);
        AutomationProperties.SetName(button, tooltip);
        button.Click += (_, _) => run();
        return button;
    }
}
