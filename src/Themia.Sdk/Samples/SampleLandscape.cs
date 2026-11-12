namespace Themia.Sdk.Samples;

/// <summary>A colour as 0..255 channels, for mixing.</summary>
internal readonly record struct Rgb(double R, double G, double B)
{
    public static Rgb Of(int hex) => new((hex >> 16) & 0xFF, (hex >> 8) & 0xFF, hex & 0xFF);

    public Rgb To(Rgb other, double t) => new(R + ((other.R - R) * t), G + ((other.G - G) * t), B + ((other.B - B) * t));

    public Rgb Plus(Rgb other, double k) => new(R + (other.R * k), G + (other.G * k), B + (other.B * k));

    public Rgb Times(double k) => new(R * k, G * k, B * k);
}

/// <summary>Where a layer's top edge is at <c>x</c>, as <c>v</c> (0 the picture's top, 1 its bottom).</summary>
internal delegate double Profile(double x);

/// <summary>A layer's 2-D outline: the signed distance from (x, v) to it, negative inside, in picture heights.</summary>
internal delegate double Outline(double x, double v);

/// <summary>Detail on a layer (windows, furrows, a path, flowers): its colour at (x, v) given its flat colour.</summary>
internal delegate Rgb Detail(double x, double v, double pixel, Rgb colour);

/// <summary>
/// One flat layer of a landscape: either a range filled from its <see cref="Top"/> edge down (to
/// <see cref="Base"/>), or an <see cref="Shape"/> between <see cref="ShapeTop"/> and
/// <see cref="ShapeBottom"/>. Its colour runs from <see cref="Colour"/> at the edge to
/// <see cref="Below"/> <see cref="Span"/> lower, then fades into the scene's haze by <see cref="Mist"/>
/// between <see cref="MistFrom"/> and <see cref="MistTo"/> (mist lying in the valleys).
/// </summary>
internal sealed record Layer(int Colour, int Below = -1, double Span = 0.3)
{
    public Profile? Top { get; init; }

    public Outline? Shape { get; init; }

    public double ShapeTop { get; init; }

    public double ShapeBottom { get; init; } = 1;

    public double Base { get; init; } = double.PositiveInfinity;

    public double Mist { get; init; }

    public double MistFrom { get; init; }

    public double MistTo { get; init; } = 1;

    public Detail? Detail { get; init; }
}

/// <summary>A sun or a moon: a disc of <see cref="Colour"/> in a glow of <see cref="Glow"/>.</summary>
internal sealed record Disc(double X, double Y, double Radius, int Colour, int Glow, double GlowStrength, double GlowRadius);

/// <summary>A flat-bottomed cloud: puffs (x, v, radius) cut at <see cref="Bottom"/>.</summary>
internal sealed record Cloud(double Bottom, int Light, int Shade, double Alpha, (double X, double Y, double R)[] Puffs);

/// <summary>Water from <see cref="Line"/> down, mirroring the scene behind it.</summary>
internal sealed record Water(double Line, int Far, int Near, double Mirror = 0.7, double Ripple = 0.08, double Wobble = 0.004);

/// <summary>A curtain of northern lights along <c>v = Base + Amp sin(x Freq + Phase)</c>.</summary>
internal sealed record Aurora(double Base, double Amp, double Freq, double Phase, double Height, int Low, int High, double Strength);

/// <summary>A light (a street lamp's glow), added over everything.</summary>
internal sealed record Glow(double X, double Y, double Radius, int Colour, double Strength);

/// <summary>A whole picture: a sky, its lights, the layers behind the water, the water, the layers in front.</summary>
internal sealed record Landscape(int SkyTop, int SkyMid, int SkyHorizon, double MidAt, double HorizonAt, int Haze)
{
    public Disc[] Discs { get; init; } = [];

    public Cloud[] Clouds { get; init; } = [];

    public Aurora[] Auroras { get; init; } = [];

    public double Stars { get; init; }

    public Layer[] Back { get; init; } = [];

    public Water? Water { get; init; }

    public Layer[] Front { get; init; } = [];

    public Glow[] Glows { get; init; } = [];

    public double Grain { get; init; } = 3;

    public int Seed { get; init; }
}

/// <summary>
/// Paints a <see cref="Landscape"/> to BGRA pixels. Coordinates are in picture heights: <c>v</c> runs
/// 0..1 down, <c>x</c> is 0 at the middle, so a square shows -0.5..0.5 and a wide picture more of the
/// same scene. Range edges are anti-aliased by four samples per column with exact vertical cover,
/// outlines by their signed distance, and fine detail fades out before it would alias.
/// </summary>
internal static class LandscapePainter
{
    private const int Sub = 4;

    public static byte[] Paint(Landscape scene, int width, int height)
    {
        var h = (double)height;
        var back = scene.Back.Select(layer => Prepare(layer, width, h)).ToArray();
        var front = scene.Front.Select(layer => Prepare(layer, width, h)).ToArray();
        var painter = new Frame(scene, back, h);
        var pixels = new byte[width * height * 4];

        // Rows are independent, so they are painted in parallel: the same bytes whatever the order.
        Parallel.For(0, height, j =>
        {
            var v = (j + 0.5) / h;
            for (var i = 0; i < width; i++)
            {
                var x = (i + 0.5 - (width / 2.0)) / h;
                var c = painter.Upper(i, x, j);

                if (scene.Water is { } water)
                {
                    var cover = Math.Clamp(j + 1 - (water.Line * h), 0, 1);
                    if (cover > 0)
                    {
                        c = c.To(painter.Water(water, i, x, j), cover);
                    }
                }

                foreach (var layer in front)
                {
                    c = painter.Over(c, layer, i, x, j);
                }

                foreach (var glow in scene.Glows)
                {
                    var d = Math.Sqrt(((x - glow.X) * (x - glow.X)) + ((v - glow.Y) * (v - glow.Y)));
                    var k = glow.Strength * Math.Exp(-(d / glow.Radius) * (d / glow.Radius));
                    var core = Math.Clamp(((glow.Radius * 0.12) - d) * h + 0.5, 0, 1);
                    c = c.Plus(Rgb.Of(glow.Colour), k).To(new Rgb(255, 246, 225), core);
                }

                var grain = (Shapes.Hash(i, j, scene.Seed + 991) - 0.5) * scene.Grain * 2;
                var at = ((j * width) + i) * 4;
                pixels[at] = (byte)Math.Clamp(c.B + grain + 0.5, 0, 255);
                pixels[at + 1] = (byte)Math.Clamp(c.G + grain + 0.5, 0, 255);
                pixels[at + 2] = (byte)Math.Clamp(c.R + grain + 0.5, 0, 255);
                pixels[at + 3] = 255;
            }
        });

        return pixels;
    }

    /// <summary>A layer with its edge sampled per column, in pixels.</summary>
    private sealed record Prepared(Layer Layer, double[] Edges, double[] Low, double[] High, double[] Ridge, double BaseRow);

    private static Prepared Prepare(Layer layer, int width, double h)
    {
        var edges = new double[width * Sub];
        var low = new double[width];
        var high = new double[width];
        var ridge = new double[width];
        if (layer.Top is { } top)
        {
            for (var i = 0; i < width; i++)
            {
                double min = double.MaxValue, max = double.MinValue, sum = 0;
                for (var s = 0; s < Sub; s++)
                {
                    var x = (i + ((s + 0.5) / Sub) - (width / 2.0)) / h;
                    var e = top(x) * h;
                    edges[(i * Sub) + s] = e;
                    min = Math.Min(min, e);
                    max = Math.Max(max, e);
                    sum += e;
                }

                low[i] = min;
                high[i] = max;
                ridge[i] = sum / Sub / h;
            }

            // The colour runs down from a smoothed edge, so a tree or a roof does not streak the layer below it.
            var reach = Math.Max(1, (int)(0.05 * h));
            var sums = new double[width + 1];
            for (var i = 0; i < width; i++)
            {
                sums[i + 1] = sums[i] + ridge[i];
            }

            for (var i = 0; i < width; i++)
            {
                int from = Math.Max(0, i - reach), to = Math.Min(width, i + reach + 1);
                ridge[i] = (sums[to] - sums[from]) / (to - from);
            }
        }

        return new Prepared(layer, edges, low, high, ridge, layer.Base * h);
    }

    private sealed class Frame(Landscape scene, Prepared[] back, double h)
    {
        private readonly Rgb _top = Rgb.Of(scene.SkyTop);
        private readonly Rgb _mid = Rgb.Of(scene.SkyMid);
        private readonly Rgb _horizon = Rgb.Of(scene.SkyHorizon);
        private readonly Rgb _haze = Rgb.Of(scene.Haze);

        /// <summary>The sky and the layers behind the water, for the pixel row starting at <paramref name="row"/> (may be fractional: a reflection).</summary>
        public Rgb Upper(int i, double x, double row)
        {
            var c = Sky(x, (row + 0.5) / h);
            foreach (var layer in back)
            {
                c = Over(c, layer, i, x, row);
            }

            return c;
        }

        public Rgb Water(Water water, int i, double x, int j)
        {
            var v = (j + 0.5) / h;
            var depth = Math.Clamp((v - water.Line) / (1 - water.Line), 0, 1);
            var below = v - water.Line;
            var wobble = water.Wobble * Math.Min(1, below * 8) * Math.Sin((x * 23) + (7 * Math.Sin(v * 61)));
            var mirrored = (2 * water.Line * h) - (j + 1) + (wobble * h);
            var reflection = Upper(i, x, mirrored);
            var tint = Rgb.Of(water.Far).To(Rgb.Of(water.Near), depth);
            var c = reflection.To(tint, 1 - (water.Mirror * (1 - (depth * 0.55))));

            // Ripples: horizontal streaks closer together towards the horizon, gone before they alias.
            const double k = 26;
            var phase = (k * Math.Log(below + 0.012)) + (0.8 * Math.Sin(x * 7));
            var perPixel = k / (below + 0.012) / h;
            var fade = Math.Clamp((1.6 - perPixel) / 0.8, 0, 1);
            return c.Times(1 + (water.Ripple * Math.Sin(phase) * fade));
        }

        public Rgb Over(Rgb c, Prepared prepared, int i, double x, double row)
        {
            var layer = prepared.Layer;
            double cover;
            if (layer.Shape is { } shape)
            {
                var v0 = (row + 0.5) / h;
                if (v0 < layer.ShapeTop - (2 / h) || v0 > layer.ShapeBottom + (2 / h))
                {
                    return c;
                }

                cover = Math.Clamp(0.5 - (shape(x, v0) * h), 0, 1);
            }
            else
            {
                cover = Cover(prepared, i, row);
            }

            if (cover <= 0)
            {
                return c;
            }

            var v = (row + 0.5) / h;
            var colour = Rgb.Of(layer.Colour);
            if (layer.Below >= 0)
            {
                var ridge = layer.Shape is null ? prepared.Ridge[i] : layer.ShapeTop;
                colour = colour.To(Rgb.Of(layer.Below), Math.Clamp((v - ridge) / layer.Span, 0, 1));
            }

            if (layer.Detail is { } detail)
            {
                colour = detail(x, v, 1 / h, colour);
            }

            if (layer.Mist > 0)
            {
                colour = colour.To(_haze, layer.Mist * Shapes.Smooth(layer.MistFrom, layer.MistTo, v));
            }

            return c.To(colour, cover);
        }

        private static double Cover(Prepared prepared, int i, double row)
        {
            if (row + 1 <= prepared.Low[i] || row >= prepared.BaseRow)
            {
                return 0;
            }

            if (row >= prepared.High[i] && row + 1 <= prepared.BaseRow)
            {
                return 1;
            }

            var sum = 0.0;
            for (var s = 0; s < Sub; s++)
            {
                var edge = prepared.Edges[(i * Sub) + s];
                sum += Math.Clamp(Math.Min(row + 1, prepared.BaseRow) - Math.Max(row, edge), 0, 1);
            }

            return sum / Sub;
        }

        private Rgb Sky(double x, double v)
        {
            var c = v < scene.MidAt
                ? _top.To(_mid, Shapes.Ease(Math.Clamp(v / scene.MidAt, 0, 1)))
                : _mid.To(_horizon, Shapes.Ease(Math.Clamp((v - scene.MidAt) / (scene.HorizonAt - scene.MidAt), 0, 1)));

            if (scene.Stars > 0)
            {
                c = Stars(c, x, v);
            }

            foreach (var aurora in scene.Auroras)
            {
                var line = aurora.Base + (aurora.Amp * Math.Sin((x * aurora.Freq) + aurora.Phase)) + (aurora.Amp * 0.35 * Math.Sin((x * aurora.Freq * 2.7) + 1.3));
                var dv = v - line;
                var profile = dv > 0 ? Math.Exp(-(dv / 0.035) * (dv / 0.035)) : Math.Exp(dv / aurora.Height);
                const double rayFreq = 34;
                var rayFade = Math.Clamp((1.6 - (rayFreq / h)) / 0.8, 0, 1);
                var ray = Math.Sin((x * rayFreq) + (4 * Math.Sin(x * 5.3)));
                var rays = 1 - (0.35 * rayFade * (1 - (ray * ray)));
                var colour = Rgb.Of(aurora.Low).To(Rgb.Of(aurora.High), Math.Clamp(-dv / (aurora.Height * 1.6), 0, 1));
                c = c.Plus(colour, aurora.Strength * profile * rays);
            }

            foreach (var disc in scene.Discs)
            {
                var d = Math.Sqrt(((x - disc.X) * (x - disc.X)) + ((v - disc.Y) * (v - disc.Y)));
                var outside = Math.Max(0, d - disc.Radius);
                var glow = disc.GlowStrength * Math.Exp(-outside / disc.GlowRadius);
                c = c.To(Rgb.Of(disc.Glow), Math.Min(1, glow));
                c = c.To(Rgb.Of(disc.Colour), Math.Clamp(0.5 - ((d - disc.Radius) * h), 0, 1));
            }

            foreach (var cloud in scene.Clouds)
            {
                if (v > cloud.Bottom + (2 / h))
                {
                    continue;
                }

                var sdf = double.MaxValue;
                foreach (var (px, py, r) in cloud.Puffs)
                {
                    sdf = Math.Min(sdf, Math.Sqrt(((x - px) * (x - px)) + ((v - py) * (v - py))) - r);
                }

                sdf = Math.Max(sdf, v - cloud.Bottom);
                var cover = Math.Clamp(0.5 - (sdf * h), 0, 1);
                if (cover > 0)
                {
                    var colour = Rgb.Of(cloud.Light).To(Rgb.Of(cloud.Shade), Shapes.Smooth(cloud.Bottom - 0.07, cloud.Bottom, v));
                    c = c.To(colour, cover * cloud.Alpha);
                }
            }

            return c;
        }

        private Rgb Stars(Rgb c, double x, double v)
        {
            const double cell = 0.03;
            var gx = (int)Math.Floor(x / cell);
            var gy = (int)Math.Floor(v / cell);
            if (Shapes.Hash(gx, gy, scene.Seed + 71) > 0.42)
            {
                return c;
            }

            var sx = (gx + 0.15 + (0.7 * Shapes.Hash(gx, gy, scene.Seed + 72))) * cell;
            var sy = (gy + 0.15 + (0.7 * Shapes.Hash(gx, gy, scene.Seed + 73))) * cell;
            var bright = Shapes.Hash(gx, gy, scene.Seed + 74);
            bright = 0.25 + (0.75 * bright * bright);
            var radius = Math.Max(0.55, 0.0011 * h);
            var d = Math.Sqrt(((x - sx) * (x - sx)) + ((v - sy) * (v - sy))) * h;
            var cover = Math.Clamp(radius + 0.5 - d, 0, 1);
            var k = scene.Stars * bright * cover * Math.Clamp(h / 300, 0.35, 1) * (1 - Shapes.Smooth(0.3, 0.62, v));
            return c.To(new Rgb(250, 248, 240), k);
        }
    }
}

/// <summary>Deterministic noise and the outlines the sample pictures are built from.</summary>
internal static class Shapes
{
    /// <summary>A number in 0..1 for (a, b) and a seed, the same on every run.</summary>
    public static double Hash(int a, int b, int seed)
    {
        unchecked
        {
            var n = ((uint)a * 374761393u) + ((uint)b * 668265263u) + ((uint)seed * 2246822519u);
            n = (n ^ (n >> 13)) * 1274126177u;
            n ^= n >> 16;
            return n / 4294967296.0;
        }
    }

    public static double Ease(double t) => t * t * (3 - (2 * t));

    public static double Smooth(double from, double to, double t) => Ease(Math.Clamp((t - from) / (to - from), 0, 1));

    /// <summary>Smooth value noise in 0..1.</summary>
    public static double Noise(double x, int seed)
    {
        var i = (int)Math.Floor(x);
        var f = x - i;
        var a = Hash(i, 0, seed);
        var b = Hash(i + 1, 0, seed);
        return a + ((b - a) * Ease(f));
    }

    /// <summary>Rolling noise in 0..1, <paramref name="octaves"/> layers each half as strong.</summary>
    public static double Fbm(double x, int seed, int octaves = 3)
    {
        double sum = 0, amp = 1, total = 0, f = 1;
        for (var o = 0; o < octaves; o++)
        {
            sum += amp * Noise(x * f, seed + (o * 101));
            total += amp;
            amp *= 0.5;
            f *= 2.03;
        }

        return sum / total;
    }

    /// <summary>Peaked noise in 0..1: sharp crests, rounded valleys.</summary>
    public static double Ridged(double x, int seed, int octaves = 4)
    {
        double sum = 0, amp = 1, total = 0, f = 1;
        for (var o = 0; o < octaves; o++)
        {
            var n = 1 - Math.Abs((2 * Noise(x * f, seed + (o * 131))) - 1);
            sum += amp * n * Math.Sqrt(n);
            total += amp;
            amp *= 0.32;
            f *= 2.1;
        }

        return sum / total;
    }

    public static Profile Hills(double low, double rise, double scale, int seed, int octaves = 3) =>
        x => low - (rise * Fbm((x / scale) + 17.3, seed, octaves));

    public static Profile Mountains(double low, double rise, double scale, int seed) =>
        x => low - (rise * Ridged((x / scale) + 5.1, seed));

    /// <summary>A line through the given (x, v) points, straight between them, level beyond them.</summary>
    public static Profile Through(params (double X, double V)[] points) => x =>
    {
        if (x <= points[0].X)
        {
            return points[0].V;
        }

        for (var k = 1; k < points.Length; k++)
        {
            if (x <= points[k].X)
            {
                var t = (x - points[k - 1].X) / (points[k].X - points[k - 1].X);
                return points[k - 1].V + ((points[k].V - points[k - 1].V) * Ease(t));
            }
        }

        return points[^1].V;
    };

    /// <summary><paramref name="ground"/> with rough noise added.</summary>
    public static Profile Rough(Profile ground, double amount, double scale, int seed) =>
        x => ground(x) - (amount * (Ridged(x / scale, seed, 3) - 0.5));

    /// <summary>Firs standing on <paramref name="ground"/>, between <paramref name="from"/> and <paramref name="to"/>.</summary>
    public static Profile Firs(Profile ground, double height, double spacing, int seed, double density = 1, double from = double.MinValue, double to = double.MaxValue) => x =>
    {
        var top = ground(x);
        var cell = (int)Math.Floor(x / spacing);
        for (var n = cell - 4; n <= cell + 4; n++)
        {
            var cx = (n + Hash(n, 2, seed)) * spacing;
            if (Hash(n, 1, seed) > density || cx < from || cx > to)
            {
                continue;
            }

            var th = height * (0.55 + (0.45 * Hash(n, 3, seed)));
            var half = th * 0.27;
            var dx = Math.Abs(x - cx);
            if (dx < half)
            {
                var t = 1 - (dx / half);

                // Tiers: the outline steps in a little three times on the way up.
                var tier = (t * 3.4) - Math.Floor(t * 3.4);
                top = Math.Min(top, ground(cx) - (th * Math.Pow(t, 1.25)) + (th * 0.05 * tier * (1 - t)));
            }
        }

        return top;
    };

    /// <summary>Round crowns standing on <paramref name="ground"/>.</summary>
    public static Profile Crowns(Profile ground, double radius, double spacing, int seed, double density = 1, double from = double.MinValue, double to = double.MaxValue) => x =>
    {
        var top = ground(x);
        var cell = (int)Math.Floor(x / spacing);
        for (var n = cell - 4; n <= cell + 4; n++)
        {
            var cx = (n + Hash(n, 2, seed)) * spacing;
            if (Hash(n, 1, seed) > density || cx < from || cx > to)
            {
                continue;
            }

            var r = radius * (0.65 + (0.6 * Hash(n, 3, seed)));
            var dx = x - cx;
            if (Math.Abs(dx) < r)
            {
                top = Math.Min(top, ground(x) + (r * 0.2) - Math.Sqrt((r * r) - (dx * dx)));
            }
        }

        return top;
    };

    public static double Circle(double x, double v, double cx, double cy, double r) =>
        Math.Sqrt(((x - cx) * (x - cx)) + ((v - cy) * (v - cy))) - r;

    public static double Box(double x, double v, double cx, double cy, double halfWidth, double halfHeight)
    {
        var dx = Math.Abs(x - cx) - halfWidth;
        var dy = Math.Abs(v - cy) - halfHeight;
        var outside = Math.Sqrt((Math.Max(dx, 0) * Math.Max(dx, 0)) + (Math.Max(dy, 0) * Math.Max(dy, 0)));
        return outside + Math.Min(Math.Max(dx, dy), 0);
    }

    /// <summary>How much of a pixel a box covers, by its signed distance.</summary>
    public static double BoxCover(double x, double v, double cx, double cy, double halfWidth, double halfHeight, double pixel) =>
        Math.Clamp(0.5 - (Box(x, v, cx, cy, halfWidth, halfHeight) / pixel), 0, 1);

    /// <summary>How strongly a pattern of <paramref name="perPixel"/> radians per pixel may show before it aliases.</summary>
    public static double Fade(double perPixel) => Math.Clamp((1.5 - perPixel) / 0.7, 0, 1);
}

/// <summary>A row of houses along <c>v = ground</c>: gables, flat roofs, chimneys and one church tower.</summary>
internal sealed class Town
{
    private readonly List<(double X0, double X1, int Kind, double Wall, double Roof)> _houses = [];
    private readonly double _ground;

    public Town(double ground, double scale, int seed, double towerAt = double.NaN, double towerHeight = 3)
    {
        _ground = ground;
        var x = -3.0;
        var n = 0;
        while (x < 3)
        {
            if (!double.IsNaN(towerAt) && towerAt >= x && towerAt < x + (scale * 1.5))
            {
                var w = scale * 0.55;
                _houses.Add((x, x + w, 3, scale * towerHeight, scale * towerHeight * 0.55));
                x += w;
                towerAt = double.NaN;
                continue;
            }

            var width = scale * (0.7 + (0.9 * Shapes.Hash(n, 1, seed)));
            var pick = Shapes.Hash(n, 4, seed);
            var kind = pick < 0.5 ? 0 : pick < 0.8 ? 1 : 2;
            var wall = scale * (0.7 + (1.0 * Shapes.Hash(n, 5, seed)));
            var roof = width * (0.35 + (0.25 * Shapes.Hash(n, 6, seed)));
            _houses.Add((x, x + width, kind, wall, kind == 1 ? 0 : roof));
            x += width;
            n++;
        }
    }

    public Profile Top => x =>
    {
        var (x0, x1, kind, wall, roof) = At(x);
        var f = (x - x0) / (x1 - x0);
        var eave = _ground - wall;
        return kind switch
        {
            // A gable with a chimney a quarter of the way along.
            0 => f is > 0.22 and < 0.3 ? Math.Min(eave - (roof * (1 - Math.Abs((2 * f) - 1))), eave - (roof * 0.78)) : eave - (roof * (1 - Math.Abs((2 * f) - 1))),

            // A flat roof with a low parapet at each end.
            1 => f is < 0.06 or > 0.94 ? eave - 0.006 : eave,

            // A steep hipped roof.
            2 => eave - (roof * 1.2 * Math.Min(1, (1 - Math.Abs((2 * f) - 1)) * 2.2)),

            // The church: a tower, a belfry and a spire.
            _ => f is < 0.12 or > 0.88 ? eave + (wall * 0.35) : eave - (roof * Math.Pow(1 - Math.Abs((2 * f) - 1), 1.3)),
        };
    };

    /// <summary>
    /// The houses' faces: roofs in <paramref name="roof"/> (or the layer's colour when negative) and
    /// windows on the walls, a grid of each house's own, <paramref name="lit"/> of them lit in
    /// <paramref name="light"/> and the rest <paramref name="dark"/>.
    /// </summary>
    public Detail Faces(int roof, double size, int light, int dark, double lit, int seed) => (x, v, pixel, colour) =>
    {
        var (x0, x1, kind, wall, _) = At(x);
        var eave = _ground - wall;
        if (roof >= 0)
        {
            colour = colour.To(Rgb.Of(roof), Math.Clamp(((eave - v) / pixel) + 0.5, 0, 1));
        }

        var top = eave + (size * (kind == 3 ? 4 : 1.2));
        var stepX = size * 2.4;
        var stepY = size * 3.2;
        var columns = Math.Max(1, (int)Math.Floor((x1 - x0) / stepX));
        var margin = ((x1 - x0) - (columns * stepX)) / 2;
        var col = (int)Math.Floor((x - x0 - margin) / stepX);
        var row = (int)Math.Floor((v - top) / stepY);
        if (col < 0 || col >= columns || row < 0 || v > _ground - (size * 1.5))
        {
            return colour;
        }

        var cx = x0 + margin + ((col + 0.5) * stepX);
        var cy = top + ((row + 0.5) * stepY);
        var cover = Shapes.BoxCover(x, v, cx, cy, size * 0.5, size * 0.8, pixel);
        var house = (int)Math.Round(x0 * 1000);
        var on = Shapes.Hash(house + col, row, seed) < lit;
        return colour.To(Rgb.Of(on ? light : dark), cover);
    };

    private (double X0, double X1, int Kind, double Wall, double Roof) At(double x)
    {
        int lo = 0, hi = _houses.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (_houses[mid].X0 <= x)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return _houses[lo];
    }
}
