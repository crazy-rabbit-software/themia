using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Astronomy;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Widgets;

// [S-3100] The parts the Sun and Moon widgets share: one list row and one moon drawing, so the two
// views stay thin and cannot drift apart.

/// <summary>
/// The moon at its current phase, drawn the same way by the Moon widget's disc and its row glyph. [S-169]
/// <para>
/// The lit limb comes from <see cref="MoonDisc"/>, so the large disc and the glyph in the row
/// list are the same drawing and cannot disagree about how much of the moon is lit.
/// The unlit part keeps a hairline rim, so a thin crescent still reads as a moon rather than as
/// a stray mark, and below the horizon the whole disc steps down a named grey rather than being
/// faded with an opacity.
/// </para>
/// </summary>
public sealed class SkyMoonDisc
{
    private readonly Ellipse _rim = new();
    private readonly Polygon _lit = new();

    private readonly Brush _litUp = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _litDown = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _rimUp = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _rimDown = ThemeBrush.Of("TextFillColorDisabledBrush", Colors.DimGray);

    private double _size;
    private double _phase;
    private bool _southern;

    public SkyMoonDisc()
    {
        _rim.StrokeThickness = Fluent.Hairline;
        _rim.Fill = null;
        _rim.Stroke = _rimUp;
        _lit.Fill = _litUp;

        Root = new Grid { Children = { _rim, _lit } };
        Resize(Fluent.IconSize);
    }

    public Grid Root { get; }

    public void Resize(double size)
    {
        if (Math.Abs(size - _size) < 0.5)
        {
            return;
        }

        _size = size;
        Root.Width = size;
        Root.Height = size;
        _rim.Width = size - 1;
        _rim.Height = size - 1;
        Rebuild();
    }

    public void Update(double phase, bool southern, bool up)
    {
        _lit.Fill = up ? _litUp : _litDown;
        _rim.Stroke = up ? _rimUp : _rimDown;

        if (Math.Abs(phase - _phase) < 0.0005 && southern == _southern)
        {
            return;
        }

        _phase = phase;
        _southern = southern;
        Rebuild();
    }

    private void Rebuild()
    {
        var radius = Math.Max(1, (_size - 1) / 2);
        var centre = _size / 2;
        var points = new PointCollection();

        foreach (var point in MoonDisc.LitLimb(centre, centre, radius, _phase, _southern))
        {
            points.Add(new Point(point.X, point.Y));
        }

        _lit.Points = points;
    }
}

/// <summary>
/// One line of the Sun or Moon widget's readout, in the Windows 11 list-row shape: a 16px glyph,
/// what it is, the time remaining while it is next, and the value. The next event's row is emphasised in the accent
/// text colour rather than banner-ed.
/// </summary>
public sealed class SkyEventRow
{
    private readonly FrameworkElement _glyph;
    private readonly TextBlock _label = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private readonly TextBlock _countdown = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly TextBlock _value = Fluent.Text(FluentType.BodyStrong);

    private readonly Brush _primary = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _secondary = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray);

    // The accent tuned for text. AccentFillColorDefaultBrush is a fill and is not contrast-safe
    // under small type; this is the key Windows uses when the accent has to be read.
    private readonly Brush _accentText = ThemeBrush.Of("AccentTextFillColorPrimaryBrush", Colors.DodgerBlue);

    public SkyEventRow(FrameworkElement glyph)
    {
        // A Fluent.Icon when it is a glyph; the moon draws its own disc at the same size.
        _glyph = glyph;
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        _glyph.Margin = new Thickness(0, 0, Fluent.Space3, 0);

        _label.VerticalAlignment = VerticalAlignment.Center;

        _countdown.VerticalAlignment = VerticalAlignment.Center;
        _countdown.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        _countdown.TextTrimming = TextTrimming.Clip;
        _countdown.Visibility = Visibility.Collapsed;

        _value.HorizontalAlignment = HorizontalAlignment.Right;
        _value.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        Grid.SetColumn(_glyph, 0);
        Grid.SetColumn(_label, 1);
        Grid.SetColumn(_countdown, 2);
        Grid.SetColumn(_value, 3);

        grid.Children.Add(_glyph);
        grid.Children.Add(_label);
        grid.Children.Add(_countdown);
        grid.Children.Add(_value);

        Root = Fluent.Row(grid);
    }

    public Border Root { get; }

    public bool AllowCountdown { get; set; } = true;

    public void SetDensity(bool compact) => Fluent.SetDensity(Root, compact);

    /// <summary>
    /// [S-1109] The width the label, its note and the value share (the row less its padding and
    /// glyph). Long words are a language's, not a size's, so the row decides from the words it
    /// has: the note goes first, then the label takes its short form.
    /// </summary>
    public double Room { get; set; } = double.PositiveInfinity;

    private string _fullLabel = string.Empty;
    private string? _shortLabel;

    /// <summary>The label, and a shorter one ("Aufgang" for "Sonnenaufgang") for a narrow row.</summary>
    public void SetLabel(string label, string? shortLabel = null)
    {
        _fullLabel = label;
        _shortLabel = shortLabel;
        _label.Text = label;
    }

    public void Set(string value, string? countdown)
    {
        _value.Text = value;

        var isNext = countdown is not null;
        _countdown.Text = countdown ?? string.Empty;

        var step = isNext ? FluentType.BodyStrong : FluentType.Body;
        _label.Apply(step);
        _label.Foreground = isNext ? _accentText : _secondary;
        _value.Foreground = isNext ? _accentText : _primary;
        Fit(step, isNext && AllowCountdown);
    }

    /// <summary>Pick the label's form and whether the note shows, from their estimated widths.</summary>
    private void Fit(TypeStep labelStep, bool noteAllowed)
    {
        var value = FluentType.EstimateWidth(FluentType.BodyStrong, _value.Text.Length) + Fluent.Space2;
        var label = FluentType.EstimateWidth(labelStep, _fullLabel.Length);
        var shortLabel = _shortLabel ?? string.Empty;
        var useShort = shortLabel.Length > 0 && label + value > Room;

        if (useShort)
        {
            label = FluentType.EstimateWidth(labelStep, shortLabel.Length);
        }

        _label.Text = useShort ? shortLabel : _fullLabel;

        var note = _countdown.Text.Length == 0
            ? 0
            : Fluent.Space2 + FluentType.EstimateWidth(FluentType.Caption, _countdown.Text.Length);

        _countdown.Visibility = noteAllowed && note > 0 && label + note + value <= Room
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>[S-1490] Whether a plain row's note ("+2m 14s", "34°") has the width to show.</summary>
    public bool AllowNote { get; set; } = true;

    /// <summary>A row that is never "next": its value, and a quiet note beside the label.</summary>
    public void SetPlain(string value, string? note)
    {
        _value.Text = value;
        _countdown.Text = note ?? string.Empty;

        _label.Apply(FluentType.Body);
        _label.Foreground = _secondary;
        _value.Foreground = _primary;
        Fit(FluentType.Body, note is not null && AllowNote);
    }
}
