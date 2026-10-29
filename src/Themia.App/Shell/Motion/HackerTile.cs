using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Motion;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;
using Windows.UI;

namespace Themia.App.Shell.Motion;

/// <summary>
/// [S-1007] One widget's hacker tile: an opaque green-phosphor panel that covers the live widget,
/// churns, then fades to reveal it. Built from shapes and text, moved by compositor loops — no
/// canvas redrawn per frame. The panel is the widget's own child, so it needs no screen geometry.
/// <para>
/// The palette is the style's identity, like the warp's ice-blue stars: deliberately literal and
/// not the app accent, as in the original. Nothing else in the app uses these colours.
/// </para>
/// </summary>
internal static class HackerTile
{
    private static readonly Color Phosphor = ShellColours.Phosphor;
    private static readonly Color PhosphorDim = ShellColours.PhosphorDim;
    private static readonly Color Screen = ShellColours.PhosphorScreen;
    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas");

    /// <summary>
    /// The tile for the widget at <paramref name="index"/>, sized to its painted surface. Its loops
    /// are started by the caller once the tile is in the tree, outside the show's batch (a loop never
    /// ends, and the batch must).
    /// </summary>
    public static Built Build(int index, double width, double height, double cornerRadius)
    {
        var variant = HackerIntro.VariantFor(index);
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);
        var stage = new Canvas
        {
            Width = w,
            Height = h,
            Clip = new RectangleGeometry { Rect = new Rect(0, 0, w, h) },
        };

        var loops = new List<Action<MotionKit>>();

        switch (variant.Panel)
        {
            case HackerPanel.Console:
                BuildConsole(stage, loops, index, w, h, variant.Hex);
                break;
            case HackerPanel.Molecule:
                BuildMolecule(stage, loops, w, h);
                break;
            case HackerPanel.Camera:
                BuildCamera(stage, loops, w, h);
                break;
            case HackerPanel.Radar:
                BuildRadar(stage, loops, w, h);
                break;
            case HackerPanel.Wave:
                BuildWave(stage, loops, w, h);
                break;
        }

        // On the screen colour, so a console's lines scroll under the tag rather than through it.
        var tag = new Border
        {
            Background = new SolidColorBrush(Screen),
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            Child = Text(variant.Tag, Phosphor),
        };
        stage.Children.Add(tag);
        tag.Loaded += (_, _) => Canvas.SetLeft(tag, Math.Max(0, w - tag.ActualWidth - 4));
        Canvas.SetTop(tag, 4);

        var tile = new Border
        {
            Background = new SolidColorBrush(Screen),
            CornerRadius = new CornerRadius(cornerRadius),
            BorderBrush = new SolidColorBrush(PhosphorDim),
            BorderThickness = new Thickness(1),
            Child = stage,
        };

        return new Built(tile, loops);
    }

    /// <summary>A built tile and the loops that animate its panel.</summary>
    internal sealed record Built(Border Tile, IReadOnlyList<Action<MotionKit>> Loops)
    {
        public void StartLoops(MotionKit kit)
        {
            foreach (var loop in Loops)
            {
                loop(kit);
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // Panels
    // -----------------------------------------------------------------------------------------

    /// <summary>A log or a hex dump, twice the tile's height, scrolling up through it.</summary>
    private static void BuildConsole(Canvas stage, List<Action<MotionKit>> loops, int seed, double w, double h, bool hex)
    {
        var lineHeight = FamilyType.Caption.LineHeight;
        var count = (int)Math.Ceiling(h / lineHeight) * 2;
        var lines = HackerIntro.ConsoleLines(seed, count, hex);
        var text = new TextBlock
        {
            FontFamily = Mono,
            FontSize = FamilyType.Caption.FontSize,
            LineHeight = lineHeight,
            Foreground = new SolidColorBrush(Phosphor),
            Text = string.Join('\n', lines),
            TextWrapping = TextWrapping.NoWrap,
        };

        Canvas.SetLeft(text, 6);
        Canvas.SetTop(text, 4);
        stage.Children.Add(text);

        var travel = count / 2 * lineHeight;
        loops.Add(kit => Start(kit, text, "Translation", kit.Path([Vector2.Zero, new Vector2(0, (float)-travel)], Math.Max(600, travel * 6), linear: true)));
    }

    /// <summary>A wireframe protein: a double helix of atoms and bonds, turning.</summary>
    private static void BuildMolecule(Canvas stage, List<Action<MotionKit>> loops, double w, double h)
    {
        var size = Math.Min(w, h) * 0.8;
        var spin = new Canvas { Width = size, Height = size };
        Canvas.SetLeft(spin, (w - size) / 2);
        Canvas.SetTop(spin, (h - size) / 2);

        const int Atoms = 9;
        var r = size / 2;
        Point? previousA = null, previousB = null;

        for (var i = 0; i < Atoms; i++)
        {
            var t = (double)i / (Atoms - 1);
            var y = size * (0.1 + (0.8 * t));
            var phase = t * Math.PI * 2;
            var a = new Point(r + (Math.Sin(phase) * r * 0.6), y);
            var b = new Point(r - (Math.Sin(phase) * r * 0.6), y);

            spin.Children.Add(Bond(a, b, PhosphorDim));
            if (previousA is { } pa && previousB is { } pb)
            {
                spin.Children.Add(Bond(pa, a, Phosphor));
                spin.Children.Add(Bond(pb, b, Phosphor));
            }

            spin.Children.Add(Atom(a, 4));
            spin.Children.Add(Atom(b, 3));
            previousA = a;
            previousB = b;
        }

        stage.Children.Add(spin);
        loops.Add(kit => Spin(kit, spin, size, 4000));
    }

    /// <summary>A CCTV feed: scan lines, a tracking reticle and a blinking REC.</summary>
    private static void BuildCamera(Canvas stage, List<Action<MotionKit>> loops, double w, double h)
    {
        for (var y = 0.0; y < h; y += 4)
        {
            stage.Children.Add(new Rectangle { Width = w, Height = 1, Fill = new SolidColorBrush(PhosphorDim), Opacity = 0.25 });
            Canvas.SetTop(stage.Children[^1], y);
        }

        var box = Math.Max(16, Math.Min(w, h) * 0.3);
        var reticle = new Canvas { Width = box, Height = box };
        reticle.Children.Add(new Rectangle { Width = box, Height = box, Stroke = new SolidColorBrush(Phosphor), StrokeThickness = 1 });
        reticle.Children.Add(Bond(new Point(box / 2, -6), new Point(box / 2, box + 6), Phosphor));
        reticle.Children.Add(Bond(new Point(-6, box / 2), new Point(box + 6, box / 2), Phosphor));
        Canvas.SetLeft(reticle, (w - box) / 2);
        Canvas.SetTop(reticle, (h - box) / 2);
        stage.Children.Add(reticle);

        var rec = Text("● REC", Phosphor);
        Canvas.SetLeft(rec, 6);
        Canvas.SetTop(rec, 4);
        stage.Children.Add(rec);

        var dx = Math.Max(0, (w - box) / 2 - 6);
        var dy = Math.Max(0, (h - box) / 2 - 6);
        Vector2[] track =
        [
            new(0, 0), new((float)(dx * 0.7), (float)(-dy * 0.5)), new((float)(-dx * 0.6), (float)(-dy * 0.2)),
            new((float)(-dx * 0.3), (float)(dy * 0.6)), new((float)(dx * 0.5), (float)(dy * 0.3)), new(0, 0),
        ];

        loops.Add(kit =>
        {
            Start(kit, reticle, "Translation", kit.Path(track, 2400));
            Start(kit, rec, "Opacity", kit.Loop(1, 0.2, 700));
        });
    }

    /// <summary>A radar scope: rings, a sweeping arm and a wireframe polygon turning the other way.</summary>
    private static void BuildRadar(Canvas stage, List<Action<MotionKit>> loops, double w, double h)
    {
        var size = Math.Min(w, h) * 0.85;
        var centre = new Point(w / 2, h / 2);

        foreach (var f in new[] { 1.0, 0.66, 0.33 })
        {
            var ring = new Ellipse { Width = size * f, Height = size * f, Stroke = new SolidColorBrush(PhosphorDim), StrokeThickness = 1 };
            Canvas.SetLeft(ring, centre.X - (size * f / 2));
            Canvas.SetTop(ring, centre.Y - (size * f / 2));
            stage.Children.Add(ring);
        }

        stage.Children.Add(Bond(new Point(centre.X - (size / 2), centre.Y), new Point(centre.X + (size / 2), centre.Y), PhosphorDim));
        stage.Children.Add(Bond(new Point(centre.X, centre.Y - (size / 2)), new Point(centre.X, centre.Y + (size / 2)), PhosphorDim));

        var sweep = new Canvas { Width = size, Height = size };
        sweep.Children.Add(new Line { X1 = size / 2, Y1 = size / 2, X2 = size, Y2 = size / 2, Stroke = new SolidColorBrush(Phosphor), StrokeThickness = FamilyShape.Stroke });
        Canvas.SetLeft(sweep, centre.X - (size / 2));
        Canvas.SetTop(sweep, centre.Y - (size / 2));
        stage.Children.Add(sweep);

        var shapeSize = size * 0.4;
        var shape = new Canvas { Width = shapeSize, Height = shapeSize };
        var polygon = new Polygon { Stroke = new SolidColorBrush(Phosphor), StrokeThickness = 1 };
        for (var i = 0; i < 5; i++)
        {
            var a = (i * Math.PI * 2 / 5) - (Math.PI / 2);
            polygon.Points.Add(new Point((shapeSize / 2) + (Math.Cos(a) * shapeSize / 2), (shapeSize / 2) + (Math.Sin(a) * shapeSize / 2)));
        }

        shape.Children.Add(polygon);
        Canvas.SetLeft(shape, centre.X - (shapeSize / 2));
        Canvas.SetTop(shape, centre.Y - (shapeSize / 2));
        stage.Children.Add(shape);

        loops.Add(kit =>
        {
            Spin(kit, sweep, size, 1600);
            Spin(kit, shape, shapeSize, -5000);
        });
    }

    /// <summary>An oscilloscope: a sine twice the tile's width, scrolling through it seamlessly.</summary>
    private static void BuildWave(Canvas stage, List<Action<MotionKit>> loops, double w, double h)
    {
        var line = new Polyline { Stroke = new SolidColorBrush(Phosphor), StrokeThickness = FamilyShape.Stroke };
        var period = Math.Max(24, w / 2);
        for (var x = 0.0; x <= (w * 2) + period; x += 3)
        {
            var y = (h / 2) + (Math.Sin(x / period * Math.PI * 2) * h * 0.25) + (Math.Sin(x / period * Math.PI * 6) * h * 0.06);
            line.Points.Add(new Point(x, y));
        }

        stage.Children.Add(Bond(new Point(0, h / 2), new Point(w, h / 2), PhosphorDim));
        stage.Children.Add(line);

        // One period per loop: the curve is the same shifted by a period, so the wrap is invisible.
        loops.Add(kit => Start(kit, line, "Translation", kit.Path([Vector2.Zero, new Vector2((float)-period, 0)], 900, linear: true)));
    }

    // -----------------------------------------------------------------------------------------
    // Pieces
    // -----------------------------------------------------------------------------------------

    private static TextBlock Text(string text, Color colour) => new()
    {
        Text = text,
        FontFamily = Mono,
        FontSize = FamilyType.Caption.FontSize,
        Foreground = new SolidColorBrush(colour),
    };

    private static Line Bond(Point a, Point b, Color colour) => new()
    {
        X1 = a.X,
        Y1 = a.Y,
        X2 = b.X,
        Y2 = b.Y,
        Stroke = new SolidColorBrush(colour),
        StrokeThickness = 1,
    };

    private static Ellipse Atom(Point at, double radius)
    {
        var atom = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = new SolidColorBrush(Phosphor) };
        Canvas.SetLeft(atom, at.X - radius);
        Canvas.SetTop(atom, at.Y - radius);
        return atom;
    }

    /// <summary>Turn an element about its centre forever; a negative period turns it the other way.</summary>
    private static void Spin(MotionKit kit, UIElement element, double size, double periodMs)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3((float)(size / 2), (float)(size / 2), 0);
        visual.StartAnimation("RotationAngleInDegrees", kit.Loop(0, periodMs < 0 ? -360 : 360, Math.Abs(periodMs)));
    }

    private static void Start(MotionKit kit, UIElement element, string property, Microsoft.UI.Composition.CompositionAnimation animation)
    {
        if (property.StartsWith("Translation", StringComparison.Ordinal))
        {
            ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        }

        ElementCompositionPreview.GetElementVisual(element).StartAnimation(property, animation);
    }
}
