using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.System;
using CP = Themia.Core.Settings.ColourPicker;

namespace Themia.App.Shell;

/// <summary>
/// [S-1574] The colour picker's spectrum as Karpo's mixer draws it (§523, §670): as wide as the mixer and
/// 96 tall (WinUI's own spectrum keeps itself square), the hue that holds with white across it from its
/// left and black up from its foot — saturation across, value up (<see cref="CP.SpectrumAt"/>) — and a
/// white ring with a dark hairline where the colour stands. A press puts the colour under the pointer and
/// holds it while dragged; letting go (or the keys leaving it) keeps it (<see cref="Kept"/>). The arrows
/// move it a step, Page Up and Page Down the value ten. Left to right in every language.
/// </summary>
internal sealed partial class SpectrumBox : ContentControl
{
    private const double Ring = 14;

    /// <summary>The spectrum's own ends (its white across, its black up) and the ring's: what the colour is mixed from, not a look.</summary>
    private static readonly Windows.UI.Color White = HexBrush.Colour(new RgbColor(255, 255, 255));
    private static readonly Windows.UI.Color Black = HexBrush.Colour(new RgbColor(0, 0, 0));
    private const double KeyStep = 0.01;
    private const double PageStep = 0.1;

    private readonly Border _hue = new();
    private readonly Ellipse _ring = new()
    {
        Width = Ring,
        Height = Ring,
        StrokeThickness = FamilyShape.Stroke,
        Stroke = new SolidColorBrush(White),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,
    };

    private readonly Ellipse _rim = new()
    {
        Width = Ring + 2,
        Height = Ring + 2,
        StrokeThickness = FamilyShape.Hairline,
        Stroke = new SolidColorBrush(Black),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,
    };

    private bool _pressed;

    public SpectrumBox(double width, double height)
    {
        Width = width;
        Height = height;
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        var white = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        white.GradientStops.Add(new GradientStop { Offset = 0, Color = White });
        white.GradientStops.Add(new GradientStop { Offset = 1, Color = HexBrush.Colour(new RgbColor(255, 255, 255), 0) });
        var black = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        black.GradientStops.Add(new GradientStop { Offset = 0, Color = HexBrush.Colour(new RgbColor(0, 0, 0), 0) });
        black.GradientStops.Add(new GradientStop { Offset = 1, Color = Black });

        var radius = new CornerRadius(ShellMetrics.Page.CardRadius);
        _hue.CornerRadius = radius;
        Content = new Grid
        {
            Background = Fluent.Clear(),
            Children =
            {
                _hue,
                new Border { Background = white, CornerRadius = radius },
                new Border { Background = black, CornerRadius = radius, BorderThickness = new Thickness(FamilyShape.Hairline), BorderBrush = ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Gray) },
                _rim,
                _ring,
            },
        };

        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += (_, _) => Release();
        KeyDown += OnKey;
        LostFocus += (_, _) => Kept?.Invoke();
        Place();
    }

    /// <summary>The hue the spectrum is drawn at, degrees.</summary>
    public double Hue { get; private set; }

    public double Saturation { get; private set; }

    public double Value { get; private set; } = 1;

    /// <summary>The colour under the ring, as it moves.</summary>
    public event Action? Changed;

    /// <summary>Let go (the pointer released, the keys gone elsewhere): the colour is kept.</summary>
    public event Action? Kept;

    public (byte R, byte G, byte B) Colour => CP.FromHsv(Hue, Saturation, Value);

    /// <summary>Stand at <paramref name="hue"/>, <paramref name="saturation"/>, <paramref name="value"/>; says nothing.</summary>
    public void Show(double hue, double saturation, double value)
    {
        Hue = hue;
        Saturation = Math.Clamp(saturation, 0, 1);
        Value = Math.Clamp(value, 0, 1);
        Place();
    }

    private void Place()
    {
        var (r, g, b) = CP.FromHsv(Hue, 1, 1);
        _hue.Background = new SolidColorBrush(HexBrush.Colour(new RgbColor(r, g, b)));
        var x = (Saturation * Width) - (Ring / 2);
        var y = ((1 - Value) * Height) - (Ring / 2);
        _ring.Margin = new Thickness(x, y, 0, 0);
        _rim.Margin = new Thickness(x - 1, y - 1, 0, 0);
    }

    private void Move(Point at)
    {
        (Saturation, Value) = CP.SpectrumAt(at.X, at.Y, Width, Height);
        Place();
        Changed?.Invoke();
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        _pressed = CapturePointer(e.Pointer);
        Focus(FocusState.Pointer);
        Move(e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pressed)
        {
            Move(e.GetCurrentPoint(this).Position);
            e.Handled = true;
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pressed)
        {
            ReleasePointerCapture(e.Pointer);
            Release();
            e.Handled = true;
        }
    }

    private void Release()
    {
        if (_pressed)
        {
            _pressed = false;
            Kept?.Invoke();
        }
    }

    private void OnKey(object sender, KeyRoutedEventArgs e)
    {
        var (ds, dv) = e.Key switch
        {
            VirtualKey.Left => (-KeyStep, 0d),
            VirtualKey.Right => (KeyStep, 0d),
            VirtualKey.Up => (0d, KeyStep),
            VirtualKey.Down => (0d, -KeyStep),
            VirtualKey.PageUp => (0d, PageStep),
            VirtualKey.PageDown => (0d, -PageStep),
            _ => (0d, 0d),
        };

        if (ds == 0 && dv == 0)
        {
            if (e.Key is VirtualKey.Enter or VirtualKey.Space)
            {
                Kept?.Invoke();
                e.Handled = true;
            }

            return;
        }

        Saturation = Math.Clamp(Saturation + ds, 0, 1);
        Value = Math.Clamp(Value + dv, 0, 1);
        Place();
        Changed?.Invoke();
        e.Handled = true;
    }
}
