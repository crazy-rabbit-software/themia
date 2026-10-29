using System.Globalization;
using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.Core.Diagnostics;
using Themia.Core.Motion;

namespace Themia.App.Shell.Motion;

/// <summary>
/// [S-1005] The full-screen hyperspace overlay: a black veil and a star field, under the widgets.
/// <para>
/// The stars are compositor sprites driven by expressions, not a canvas redrawn per frame: each star
/// is fixed at birth, and where it is follows in closed form from two animated numbers — the warp
/// level and the travel it has accumulated (<see cref="Hyperspace.Distance"/>). The UI thread builds
/// the field once and never touches it again; the compositor moves a hundred stars for free.
/// </para>
/// </summary>
internal sealed class HyperspaceOverlay : IDisposable
{
    private const string Scope = "motion";

    /// <summary>The field's seed: every show draws the same sky, like a repeatable render.</summary>
    private const int Seed = 1977;

    /// <summary>How finely the warp's level and travel are sampled; the compositor interpolates between.</summary>
    private const double SampleMs = 25;

    private readonly Grid _root;
    private readonly Rectangle _veil;
    private readonly Canvas _field;
    private readonly double _width;
    private readonly double _height;
    private readonly double _totalMs;
    private readonly IntroSchedule _schedule;
    private ContainerVisual? _container;
    private CompositionPropertySet? _warp;
    private readonly List<CompositionObject> _owned = [];

    /// <summary>[S-1760] The overlay plays the warp's natural timeline at the schedule's scale, ending with the show.</summary>
    public HyperspaceOverlay(double width, double height, IntroSchedule schedule)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _schedule = schedule;
        _totalMs = Math.Max(1, schedule.TotalMs);

        // Pure black space, the style's identity rather than a theme colour: the warp blacks the
        // desktop out completely and the widgets crash into it as it lifts.
        _veil = new Rectangle { Fill = new SolidColorBrush(ShellColours.Space) };
        _field = new Canvas();
        _root = new Grid
        {
            IsHitTestVisible = false,
            Children = { _veil, _field },
        };
    }

    /// <summary>The element to put under the widget layer.</summary>
    public FrameworkElement Element => _root;

    /// <summary>
    /// Start the show's finite animations: the warp's level, travel and clock, the veil lifting across
    /// the deceleration, and the overlay's own fade at the end. Call inside the show's batch, so its
    /// end is the show's end.
    /// </summary>
    public void StartTimeline(MotionKit kit)
    {
        var compositor = kit.Compositor;
        // Sampled on the warp's own clock, every SampleMs of real time, then laid onto the real one.
        var scale = Math.Max(1e-6, _schedule.TimeScale);
        var samples = Hyperspace.Samples(SampleMs / scale, _totalMs / scale);

        _warp = compositor.CreatePropertySet();
        _warp.InsertScalar("W", 0);
        _warp.InsertScalar("D", 0);
        _warp.InsertScalar("T", 0);
        _owned.Add(_warp);

        _warp.StartAnimation("W", kit.Sampled([.. samples.Select(s => (s.Ms * scale, s.Level))], _totalMs));
        _warp.StartAnimation("D", kit.Sampled([.. samples.Select(s => (s.Ms * scale, s.Travel))], _totalMs));
        _warp.StartAnimation("T", kit.Sampled([(0, 0), (_totalMs, _totalMs)], _totalMs));

        ElementCompositionPreview.GetElementVisual(_veil).StartAnimation("Opacity", kit.FadeOut(_schedule.Scaled(Hyperspace.JumpMs), _schedule.Scaled(Hyperspace.DecelMs)));
        var fade = _schedule.Scaled(Hyperspace.FadeMs);
        ElementCompositionPreview.GetElementVisual(_root).StartAnimation("Opacity", kit.FadeOut(_totalMs - fade, fade));
    }

    /// <summary>
    /// Build the star field and bind every star to the warp. Expressions never end, so this runs
    /// outside the batch; the field goes with <see cref="Dispose"/>.
    /// </summary>
    public void StartField(MotionKit kit)
    {
        if (_warp is null)
        {
            Log.Warn(Scope, "the star field was started before its timeline; drawing the veil only");
            return;
        }

        var compositor = kit.Compositor;
        var radius = Hyperspace.Radius(_width, _height);
        var (low, span) = Hyperspace.LogWindow(radius);
        var cx = _width / 2;
        var cy = _height * Hyperspace.CentreYFraction;

        _container = compositor.CreateContainerVisual();
        _owned.Add(_container);

        // One closed-form star, written once: the same formula as Hyperspace.Distance / Angle /
        // Streak / Thickness / Alpha, which the tests pin.
        const string X = "(L0 - Lo + Rate * p.D)";
        var lives = $"Floor({X} / Span)";
        var dist = $"(Pow(2.718281828, Lo + {X} - {lives} * Span) - Core)";
        var angle = $"(A0 + {lives} * Turn)";
        var depth = $"({dist} / R)";

        var offset = $"Vector3(Cx + Cos({angle}) * {dist}, Cy + Sin({angle}) * {dist}, 0)";
        var scale = $"Vector3(Max(0.7, Min(Max(0.7, Speed * p.W * Trail * (0.1 + {depth})), {dist})), (0.5 + {depth} * 1.1) * (1 + p.W * 0.6), 1)";
        var alpha = $"Clamp((0.3 + {depth} * 0.55) * (0.5 + p.W * 0.5) * (0.75 + 0.25 * Sin(p.T * 0.004 * Speed + Phase)), 0, 1)";

        foreach (var star in Hyperspace.Stars(Hyperspace.StarCount, Seed, radius))
        {
            var (r, g, b) = Hyperspace.Colour(star, radius);
            var brush = compositor.CreateColorBrush(ShellColours.Star(r, g, b));
            var sprite = compositor.CreateSpriteVisual();
            sprite.Brush = brush;
            sprite.Size = new Vector2(1, 1);

            // The head sits at the offset and the streak reaches back toward the core: scale and
            // rotation pivot on the head.
            sprite.AnchorPoint = new Vector2(1, 0.5f);
            sprite.CenterPoint = new Vector3(1, 0.5f, 0);
            _container.Children.InsertAtTop(sprite);
            _owned.Add(brush);
            _owned.Add(sprite);

            Bind(compositor, sprite, "Offset", offset, star, low, span, radius, cx, cy);
            Bind(compositor, sprite, "RotationAngle", angle, star, low, span, radius, cx, cy);
            Bind(compositor, sprite, "Scale", scale, star, low, span, radius, cx, cy);
            Bind(compositor, sprite, "Opacity", alpha, star, low, span, radius, cx, cy);
        }

        ElementCompositionPreview.SetElementChildVisual(_field, _container);
    }

    private void Bind(Compositor compositor, Visual sprite, string property, string expression, Star star,
        double low, double span, double radius, double cx, double cy)
    {
        var animation = compositor.CreateExpressionAnimation(expression);
        animation.SetReferenceParameter("p", _warp);
        animation.SetScalarParameter("L0", (float)star.Log0);
        animation.SetScalarParameter("Rate", (float)star.Rate);
        animation.SetScalarParameter("A0", (float)star.Angle);
        animation.SetScalarParameter("Speed", (float)star.Speed);
        animation.SetScalarParameter("Phase", (float)star.Phase);
        animation.SetScalarParameter("Lo", (float)low);
        animation.SetScalarParameter("Span", (float)span);
        animation.SetScalarParameter("Core", (float)(Hyperspace.CoreOffset * radius));
        animation.SetScalarParameter("R", (float)radius);
        animation.SetScalarParameter("Cx", (float)cx);
        animation.SetScalarParameter("Cy", (float)cy);
        animation.SetScalarParameter("Turn", (float)Hyperspace.RebirthTurn);
        animation.SetScalarParameter("Trail", (float)Hyperspace.TrailFactor);
        sprite.StartAnimation(property, animation);
        _owned.Add(animation);
    }

    public void Dispose()
    {
        try
        {
            ElementCompositionPreview.SetElementChildVisual(_field, null);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"could not detach the star field ({ex.Message})");
        }

        foreach (var owned in _owned)
        {
            owned.Dispose();
        }

        _owned.Clear();
        _container = null;
        _warp = null;
    }

    /// <summary>For a log line: the field's size, so a report can say what was drawn.</summary>
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"hyperspace {_width:0}x{_height:0} for {_totalMs:0} ms");
}
