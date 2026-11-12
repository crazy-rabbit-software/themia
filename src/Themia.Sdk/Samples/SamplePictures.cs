using static Themia.Sdk.Samples.Shapes;

namespace Themia.Sdk.Samples;

/// <summary>
/// The sample day ([S-11850], [S-2056]): the pictures of the sample day, drawn in code so no preview shows a
/// picture read from this PC: the music widget's album cover, the gallery's pictures and the folder
/// widget's pictures folder. Each is a flat, poster-like landscape matching its file name (a sky, a
/// sun or moon, ranges receding into haze, water mirroring them, a town's roofs), painted by
/// <see cref="LandscapePainter"/>. Picture <c>n</c> is the same on every run and different from every other.
/// </summary>
public static class SamplePictures
{
    private static readonly (string Name, string Slug, Func<Landscape> Scene)[] Pictures =
    [
        ("Lake at dusk.png", "lake-at-dusk", LakeAtDusk),
        ("Evening light.png", "evening-light", EveningLight),
        ("Forest path.png", "forest-path", ForestPath),
        ("Autumn field.png", "autumn-field", AutumnField),
        ("Harbour fog.png", "harbour-fog", HarbourFog),
        ("Meadow.png", "meadow", Meadow),
        ("Sunset over the hills.png", "sunset-over-the-hills", SunsetOverTheHills),
        ("Old town.png", "old-town", OldTown),
        ("Morning mist.png", "morning-mist", MorningMist),
        ("Coast.png", "coast", Coast),
        ("Quiet street.png", "quiet-street", QuietStreet),
        ("Glacier.png", "glacier", Glacier),
    ];

    /// <summary>The pictures' file names: the pictures folder the gallery and the folder widget show.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Pictures.Select(p => p.Name)];

    /// <summary>
    /// [S-2210] Each picture's stable name, in the order of <see cref="Names"/>: its words in lower case
    /// joined by <c>-</c> (<c>sunset-over-the-hills</c>), what a store scene's <c>sample:</c> wallpaper names.
    /// </summary>
    public static IReadOnlyList<string> Slugs { get; } = [.. Pictures.Select(p => p.Slug)];

    /// <summary>[S-2210] The album cover's name as a wallpaper (<see cref="AlbumCover"/>).</summary>
    public const string CoverSlug = "cover";

    /// <summary>[S-2210] Every name a <c>sample:</c> wallpaper may give: the pictures', then the cover's.</summary>
    public static IReadOnlyList<string> WallpaperSlugs { get; } = [.. Slugs, CoverSlug];

    /// <summary>
    /// [S-2210] The picture named <paramref name="slug"/> (one of <see cref="WallpaperSlugs"/>) as
    /// <paramref name="width"/> × <paramref name="height"/> BGRA pixels, painted at that size; null for a name there is not.
    /// </summary>
    public static byte[]? Wallpaper(string slug, int width, int height)
    {
        if (string.Equals(slug, CoverSlug, StringComparison.Ordinal))
        {
            return AlbumCover(width, height);
        }

        var index = Array.FindIndex(Pictures, p => string.Equals(p.Slug, slug, StringComparison.Ordinal));
        return index < 0 ? null : Pixels(index, width, height);
    }

    /// <summary>How many different pictures there are before they repeat.</summary>
    public static int Count => Pictures.Length;

    /// <summary>Picture <paramref name="index"/> (cycled) as <paramref name="width"/> × <paramref name="height"/> BGRA pixels, opaque, top row first.</summary>
    public static byte[] Pixels(int index, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var scene = Pictures[((index % Count) + Count) % Count].Scene();
        return LandscapePainter.Paint(scene, width, height);
    }

    /// <summary>
    /// The sample track's album cover ("Long Winter", "Northern Lights"): the northern lights over a
    /// snowy lake, as <paramref name="width"/> × <paramref name="height"/> BGRA pixels like <see cref="Pixels"/>.
    /// </summary>
    public static byte[] AlbumCover(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return LandscapePainter.Paint(LongWinter(), width, height);
    }

    private static Landscape LakeAtDusk() => new(0x2C2F5E, 0x9A6E9E, 0xF6B27A, 0.32, 0.56, 0xC99AAE)
    {
        Seed = 1,
        Discs = [new(0.16, 0.548, 0.038, 0xFFE6B8, 0xFBC48A, 0.95, 0.09)],
        Back =
        [
            new(0x8D7BA8, 0x9C88AE) { Top = Mountains(0.565, 0.2, 0.4, 11), Mist = 0.45, MistFrom = 0.42, MistTo = 0.56 },
            new(0x5E5084) { Top = Hills(0.567, 0.05, 0.18, 12) },
            new(0x372F5C) { Top = Firs(_ => 0.568, 0.035, 0.009, 13, 0.85) },
        ],
        Water = new(0.568, 0x6A5A8E, 0x221E42, Mirror: 0.78, Ripple: 0.035),
        Front =
        [
            new(0x17152C) { Top = Firs(Through((-1.2, 0.74), (-0.42, 0.84), (-0.26, 1.3)), 0.42, 0.045, 14, 1, to: -0.27) },
            new(0x17152C) { Top = Firs(Through((0.35, 1.3), (0.6, 0.9), (1.2, 0.82)), 0.3, 0.05, 15, 1, from: 0.55) },
        ],
    };

    private static Landscape EveningLight() => new(0x86A9C8, 0xF0C88E, 0xFCE6B6, 0.3, 0.55, 0xF0CF9A)
    {
        Seed = 2,
        Discs = [new(-0.24, 0.4, 0.042, 0xFFF9E6, 0xFFE0A2, 0.9, 0.13)],
        Back =
        [
            new(0xE6BC86) { Top = Hills(0.57, 0.07, 0.5, 21) },
            new(0xD09A60) { Top = Hills(0.65, 0.1, 0.4, 22), Mist = 0.3, MistFrom = 0.6, MistTo = 0.72 },
            new(0xA86C40, 0x8A5434, 0.2) { Top = Crowns(Hills(0.74, 0.08, 0.35, 23), 0.028, 0.07, 24, 0.55) },
            new(0x5E3B2C, 0x3E251C, 0.2) { Top = Hills(0.88, 0.08, 0.3, 25) },
        ],
    };

    private static Landscape ForestPath()
    {
        const double far = 0.75;
        return new(0xB9D5C2, 0xE5EDD5, 0xF8F1D8, 0.25, 0.55, 0xE3EAD4)
        {
            Seed = 3,
            Discs = [new(0.04, 0.42, 0.034, 0xFFFCEE, 0xFFF1C4, 0.75, 0.11)],
            Back =
            [
                new(0xA9C6B0) { Top = Firs(Hills(0.52, 0.05, 0.4, 31), 0.11, 0.022, 32) },
                new(0x7FA88C) { Top = Firs(Hills(0.6, 0.05, 0.4, 33), 0.15, 0.03, 34), Mist = 0.35, MistFrom = 0.55, MistTo = 0.68 },
                new(0x4F7F62) { Top = Firs(Hills(0.69, 0.04, 0.4, 35), 0.2, 0.04, 36), Mist = 0.3, MistFrom = 0.66, MistTo = 0.76 },
                new(0x6A9455, 0x3C6435, 0.25) { Top = Hills(far, 0.008, 0.5, 37), Detail = Path(far) },
                new(0x24412F) { Top = Firs(_ => 1.08, 0.95, 0.11, 38, 1, to: -0.36) },
                new(0x24412F) { Top = Firs(_ => 1.08, 0.95, 0.11, 39, 1, from: 0.38) },
            ],
        };
    }

    private static Landscape AutumnField() => new(0x8DB8D8, 0xC9DCE6, 0xF4E6C8, 0.3, 0.56, 0xE8DCC6)
    {
        Seed = 4,
        Clouds = [new(0.2, 0xFFFFFF, 0xE4E2EA, 0.85, [(-0.33, 0.175, 0.035), (-0.26, 0.155, 0.05), (-0.18, 0.172, 0.035), (-0.12, 0.185, 0.022)])],
        Back =
        [
            new(0xAEB6C4) { Top = Hills(0.55, 0.06, 0.5, 41) },
            new(0xC0703A, 0xA35A30, 0.06) { Top = Crowns(Hills(0.61, 0.04, 0.4, 42), 0.018, 0.034, 43, 0.85), Mist = 0.15, MistFrom = 0.5, MistTo = 0.62 },
            new(0xE7B04E, 0xC9822E, 0.35) { Top = Hills(0.645, 0.012, 0.6, 44), Detail = Furrows(0.12, 0.6, 0x9E6224) },
        ],
        Front =
        [
            new(0xB5482A, 0x6E2E1E, 0.16)
            {
                Shape = (x, v) => Math.Min(
                    Math.Min(Circle(x, v, 0.3, 0.53, 0.058), Math.Min(Circle(x, v, 0.25, 0.56, 0.042), Circle(x, v, 0.355, 0.565, 0.044))),
                    Box(x, v, 0.302, 0.64, 0.0065, 0.06)),
                ShapeTop = 0.47,
                ShapeBottom = 0.7,
            },
        ],
    };

    private static Landscape HarbourFog() => new(0xA7B4BE, 0xC8D0D3, 0xDDE1DF, 0.3, 0.58, 0xD6DBDA)
    {
        Seed = 5,
        Discs = [new(-0.25, 0.27, 0.034, 0xF8F5EA, 0xECEAE0, 0.5, 0.08)],
        Back =
        [
            new(0xBAC3C6) { Top = Hills(0.53, 0.06, 0.5, 51) },
            new(0x9AA7AE) { Top = new Town(0.588, 0.022, 52, towerAt: 0.42).Top, Mist = 0.35, MistFrom = 0.5, MistTo = 0.59 },
            new(0x5D6C76) { Top = Boats(0.59, (-0.08, 1), (0.27, 0.65)), Base = 0.592 },
        ],
        Water = new(0.59, 0xB0BBBE, 0x7C8D96, Mirror: 0.6, Ripple: 0.03),
        Front =
        [
            new(0x46525A, 0x343E45, 0.2) { Top = Quay(-0.32, 0.73), Mist = 0.15, MistFrom = 0.7, MistTo = 1 },
        ],
    };

    private static Landscape Meadow() => new(0x4E9BDB, 0x8CC2EA, 0xDDEFF7, 0.3, 0.6, 0xCFE5EC)
    {
        Seed = 6,
        Clouds =
        [
            new(0.2, 0xFFFFFF, 0xDCE8F2, 0.95, [(-0.42, 0.18, 0.04), (-0.35, 0.155, 0.055), (-0.27, 0.17, 0.045), (-0.21, 0.185, 0.03)]),
            new(0.31, 0xFFFFFF, 0xDCE8F2, 0.9, [(0.24, 0.29, 0.028), (0.29, 0.275, 0.038), (0.35, 0.29, 0.026)]),
        ],
        Back =
        [
            new(0xA9CBB8) { Top = Hills(0.6, 0.08, 0.6, 61) },
            new(0x6FA460) { Top = Crowns(Hills(0.64, 0.04, 0.5, 62), 0.016, 0.05, 63, 0.55) },
            new(0x7DBB5A, 0x5DA243, 0.2) { Top = Hills(0.74, 0.06, 0.5, 64) },
            new(0x5EAA42, 0x3F8A31, 0.2) { Top = Hills(0.85, 0.05, 0.45, 65), Detail = Flowers(0.6, 66) },
        ],
    };

    private static Landscape SunsetOverTheHills() => new(0x352A5E, 0xC8566E, 0xFFB65C, 0.33, 0.62, 0xF09A72)
    {
        Seed = 7,
        Discs = [new(0.06, 0.57, 0.08, 0xFFF0C4, 0xFFC982, 1.0, 0.1)],
        Back =
        [
            new(0xD47E7A) { Top = Hills(0.63, 0.07, 0.55, 71), Mist = 0.3, MistFrom = 0.58, MistTo = 0.68 },
            new(0xA4526C) { Top = Hills(0.7, 0.09, 0.45, 72), Mist = 0.2, MistFrom = 0.66, MistTo = 0.76 },
            new(0x6E3260) { Top = Crowns(Hills(0.78, 0.09, 0.4, 73), 0.02, 0.05, 74, 0.4) },
            new(0x3B1D44, 0x2A1433, 0.2) { Top = Hills(0.91, 0.1, 0.35, 75) },
        ],
    };

    private static Landscape OldTown()
    {
        var far = new Town(0.64, 0.03, 82, towerAt: -0.3, towerHeight: 3.2);
        var near = new Town(0.9, 0.08, 83, towerAt: 0.1, towerHeight: 3.4);
        return new(0x7FB0D8, 0xB8D3E3, 0xF7E3BE, 0.3, 0.6, 0xE9DCC8)
        {
            Seed = 8,
            Discs = [new(-0.42, 0.3, 0.04, 0xFFF6DD, 0xFFE7B5, 0.6, 0.12)],
            Clouds = [new(0.16, 0xFFFFFF, 0xE8E6EA, 0.85, [(0.32, 0.14, 0.03), (0.37, 0.125, 0.04), (0.43, 0.14, 0.028)])],
            Back =
            [
                new(0xB4C2C9) { Top = Hills(0.58, 0.1, 0.6, 81) },
                new(0xD8C3AD) { Top = far.Top, Detail = far.Faces(0xB98A7E, 0.006, 0xA4908A, 0xA4908A, 0, 84), Mist = 0.35, MistFrom = 0.5, MistTo = 0.66 },
                new(0xE8BE88, 0xD3A271, 0.25) { Top = near.Top, Detail = near.Faces(0xA8462F, 0.014, 0xF6DDA0, 0x5E4236, 0.12, 85) },
                new(0x8E7262, 0x6E5444, 0.1) { Top = _ => 0.905 },
            ],
        };
    }

    private static Landscape MorningMist() => new(0xAFC6DE, 0xDCE1E8, 0xF9E6D6, 0.3, 0.5, 0xF1E8E4)
    {
        Seed = 9,
        Discs = [new(0.22, 0.33, 0.045, 0xFFFCF4, 0xFFF0DD, 0.7, 0.12)],
        Back =
        [
            new(0xBCC6D2) { Top = Firs(Hills(0.45, 0.08, 0.5, 91), 0.05, 0.016, 92), Mist = 0.6, MistFrom = 0.38, MistTo = 0.5 },
            new(0x9EAEBF) { Top = Firs(Hills(0.53, 0.08, 0.45, 93), 0.07, 0.02, 94), Mist = 0.75, MistFrom = 0.46, MistTo = 0.6 },
            new(0x7B90A5) { Top = Firs(Hills(0.63, 0.08, 0.4, 95), 0.09, 0.025, 96), Mist = 0.8, MistFrom = 0.56, MistTo = 0.72 },
            new(0x58708A) { Top = Firs(Hills(0.75, 0.08, 0.35, 97), 0.12, 0.034, 98), Mist = 0.8, MistFrom = 0.69, MistTo = 0.86 },
            new(0x354B62) { Top = Firs(Hills(0.92, 0.07, 0.3, 99), 0.16, 0.045, 100), Mist = 0.4, MistFrom = 0.86, MistTo = 1 },
        ],
    };

    private static Landscape Coast()
    {
        var sand = Through((-1.2, 0.75), (-0.25, 0.8), (0.5, 0.89), (1.2, 0.91));
        return new(0x3A86CF, 0x7DB7E3, 0xD4EBF4, 0.25, 0.5, 0xBBD9E6)
        {
            Seed = 10,
            Clouds =
            [
                new(0.22, 0xFFFFFF, 0xDDEAF2, 0.95, [(-0.45, 0.2, 0.035), (-0.38, 0.18, 0.05), (-0.3, 0.195, 0.04)]),
                new(0.13, 0xFFFFFF, 0xDDEAF2, 0.85, [(0.12, 0.115, 0.022), (0.16, 0.105, 0.03), (0.21, 0.118, 0.02)]),
            ],
            Back =
            [
                new(0xA9C4CF) { Top = Through((-1.2, 0.475), (-0.62, 0.485), (-0.45, 0.51)) },
                new(0x7E9A6A, 0x9A8670, 0.08) { Top = Rough(Through((0.1, 0.53), (0.3, 0.37), (0.55, 0.31), (1.2, 0.28)), 0.03, 0.08, 101), Mist = 0.2, MistFrom = 0.3, MistTo = 0.5 },
            ],
            Water = new(0.5, 0x1F6FA8, 0x3FB3C0, Mirror: 0.3, Ripple: 0.05),
            Front =
            [
                new(0xEBD6A8, 0xDDC08C, 0.15) { Top = sand, Detail = Foam(sand) },
            ],
        };
    }

    private static Landscape QuietStreet()
    {
        var far = new Town(0.7, 0.04, 111, towerAt: -0.28, towerHeight: 3);
        var houses = new Town(0.9, 0.085, 112);
        return new(0x141E45, 0x2E3F78, 0x6F6FA0, 0.35, 0.7, 0x4A4F7E)
        {
            Seed = 11,
            Stars = 0.8,
            Discs = [new(0.33, 0.16, 0.03, 0xF6F2DC, 0xA9B1D6, 0.45, 0.06)],
            Back =
            [
                new(0x2B3666) { Top = far.Top },
                new(0x1C2142) { Top = houses.Top, Detail = houses.Faces(-1, 0.012, 0xF8C46C, 0x262C52, 0.45, 113) },
                new(0x2D3254, 0x22263F, 0.1) { Top = _ => 0.9, Detail = Kerb(0.935, 0x3E4470) },
                new(0x0F1328) { Shape = (x, v) => Math.Min(Lamp(x, v, -0.42), Lamp(x, v, 0.2)), ShapeTop = 0.52, ShapeBottom = 0.92 },
            ],
            Glows = [new(-0.42, 0.555, 0.09, 0xFFC870, 0.55), new(0.2, 0.555, 0.09, 0xFFC870, 0.55)],
        };
    }

    private static Landscape Glacier() => new(0x6FA9D6, 0xA8CEE6, 0xEAF4F8, 0.28, 0.52, 0xD5E6EE)
    {
        Seed = 12,
        Back =
        [
            new(0xA3B9CC) { Top = Mountains(0.56, 0.32, 0.55, 121), Detail = Snow(0.36, 0.03, 0xF3F8FB, 125), Mist = 0.35, MistFrom = 0.42, MistTo = 0.6 },
            new(0x5F7A92, 0x4B6378, 0.15) { Top = Mountains(0.625, 0.2, 0.5, 122), Detail = Snow(0.5, 0.03, 0xE8F1F7, 126, 0.07) },
        ],
        Water = new(0.625, 0x8ACBD6, 0x3E8FA6, Mirror: 0.7, Ripple: 0.03),
        Front =
        [
            new(0xF2F8FB, 0xD0E2EC, 0.01)
            {
                Shape = (x, v) => Math.Min(Box(x, v, -0.3, 0.7, 0.045, 0.004), Math.Min(Box(x, v, 0.26, 0.76, 0.065, 0.006), Box(x, v, 0.04, 0.665, 0.02, 0.003))),
                ShapeTop = 0.65,
                ShapeBottom = 0.78,
            },
            new(0x3D4B58, 0x2C3742, 0.1) { Top = Rough(Through((-1.2, 0.84), (-0.3, 0.9), (0.4, 0.95), (1.2, 0.88)), 0.04, 0.1, 123) },
        ],
    };

    // Square, but a wide card crops it to its middle band: the peaks, the far shore and their
    // reflection sit there, the lights just above.
    private static Landscape LongWinter() => new(0x040B1E, 0x0B2340, 0x1C4C5E, 0.36, 0.58, 0x1E4658)
    {
        Seed = 13,
        Stars = 1,
        Auroras =
        [
            new(0.27, 0.05, 3.1, 0.4, 0.16, 0x58F5A0, 0x7A5CE8, 0.8),
            new(0.17, 0.035, 4.3, 2, 0.09, 0x4FE3C4, 0x5B6BE0, 0.35),
        ],
        Back =
        [
            new(0x2C4D66, 0x1A3044, 0.2) { Top = Mountains(0.57, 0.27, 0.45, 131), Detail = Snow(0.4, 0.02, 0xA9CFD6, 135) },
            new(0x0B1A26) { Top = Firs(_ => 0.585, 0.035, 0.01, 132, 0.9) },
        ],
        Water = new(0.586, 0x12354A, 0x071622, Mirror: 0.82, Ripple: 0.04),
        Front =
        [
            new(0x040A12) { Top = Firs(Through((-0.7, 0.8), (-0.3, 0.9), (-0.15, 1.3)), 0.42, 0.05, 133, 1, to: -0.2) },
            new(0x040A12) { Top = Firs(Through((0.2, 1.3), (0.36, 0.92), (0.7, 0.86)), 0.3, 0.05, 134, 1, from: 0.3) },
        ],
    };

    /// <summary>A path from the trees at <paramref name="far"/> to the bottom, winding as it comes nearer.</summary>
    private static Detail Path(double far) => (x, v, pixel, colour) =>
    {
        var t = (v - far) / (1 - far);
        if (t <= 0)
        {
            return colour;
        }

        var centre = 0.02 + (0.09 * Math.Sin(t * 4.2) * t);
        var half = 0.003 + (0.3 * Math.Pow(t, 1.35));
        var cover = Math.Clamp(0.5 - ((Math.Abs(x - centre) - half) / pixel), 0, 1);
        return colour.To(Rgb.Of(0xE6D6AA).To(Rgb.Of(0xB89C6A), t), cover);
    };

    /// <summary>Crop rows running to a point on the horizon.</summary>
    private static Detail Furrows(double vanishX, double vanishV, int dark) => (x, v, pixel, colour) =>
    {
        var dv = v - vanishV;
        if (dv <= 0)
        {
            return colour;
        }

        const double k = 70;
        var wave = Math.Sin(k * (x - vanishX) / dv);
        return colour.To(Rgb.Of(dark), (0.5 + (0.5 * wave)) * 0.3 * Fade(k / dv * pixel));
    };

    /// <summary>Flowers scattered over a meadow seen from above its horizon at <paramref name="horizon"/>.</summary>
    private static Detail Flowers(double horizon, int seed) => (x, v, pixel, colour) =>
    {
        var dv = v - horizon;
        if (dv <= 0.05)
        {
            return colour;
        }

        const double across = 0.28;
        const double along = 0.22;
        var gz = 1 / dv;
        var gx = x * gz;
        var cx = (int)Math.Floor(gx / across);
        var cz = (int)Math.Floor(gz / along);
        if (Hash(cx, cz, seed) > 0.55)
        {
            return colour;
        }

        var fx = (cx + 0.25 + (0.5 * Hash(cx, cz, seed + 1))) * across;
        var fz = (cz + 0.3 + (0.4 * Hash(cx, cz, seed + 2))) * along;
        var sx = fx / fz;
        var sv = horizon + (1 / fz);
        var r = 0.016 * (sv - horizon);
        var d = Math.Sqrt(((x - sx) * (x - sx)) + ((v - sv) * (v - sv)));
        var cover = Math.Clamp(0.5 - ((d - r) / pixel), 0, 1) * Math.Clamp(r / pixel, 0, 1);
        int[] petals = [0xFFFFFF, 0xF7D64A, 0xF29BB8, 0xB9A2E6];
        return colour.To(Rgb.Of(petals[(int)(Hash(cx, cz, seed + 3) * petals.Length)]), cover);
    };

    /// <summary>Snow above a ragged line near <paramref name="line"/>, its edge a zigzag of tongues running down the slopes.</summary>
    private static Detail Snow(double line, double ragged, int snow, int seed, double tongues = 0.05) => (x, v, pixel, colour) =>
    {
        var phase = (x * 16) + (3 * Fbm(x * 2.5, seed + 1));
        var zigzag = 1 - Math.Abs((2 * (phase - Math.Floor(phase))) - 1);
        var at = line + (ragged * ((Fbm(x * 6, seed) * 2) - 1)) + (tongues * zigzag * (0.3 + Noise(x * 9, seed + 2)));
        return colour.To(Rgb.Of(snow), Math.Clamp(((at - v) / pixel) + 0.5, 0, 1));
    };

    /// <summary>A line of surf along the sand's edge and the darker wet sand below it.</summary>
    private static Detail Foam(Profile sand) => (x, v, pixel, colour) =>
    {
        var below = v - sand(x) - (0.004 * Math.Sin(x * 40));
        var wet = colour.To(Rgb.Of(0xCDB386), Math.Clamp(((0.03 - below) / pixel) + 0.5, 0, 1));
        return wet.To(Rgb.Of(0xF8FAF6), Math.Clamp(((0.011 - below) / pixel) + 0.5, 0, 1));
    };

    /// <summary>A kerb's lit edge across the street at <paramref name="at"/>.</summary>
    private static Detail Kerb(double at, int edge) => (x, v, pixel, colour) =>
        colour.To(Rgb.Of(edge), BoxCover(x, v, x, at, 1, 0.003, pixel));

    /// <summary>A street lamp at <paramref name="cx"/>: a post, a lantern and its cap.</summary>
    private static double Lamp(double x, double v, double cx) =>
        Math.Min(Box(x, v, cx, 0.75, 0.0045, 0.16), Math.Min(Box(x, v, cx, 0.555, 0.011, 0.02), Box(x, v, cx, 0.53, 0.016, 0.005)));

    /// <summary>A quay wall from the left to <paramref name="end"/>, its top at <paramref name="top"/>, with two bollards.</summary>
    private static Profile Quay(double end, double top) => x =>
    {
        if (x > end)
        {
            return 1.5;
        }

        var bollard = Math.Abs(x + 0.62) < 0.01 || Math.Abs(x + 0.44) < 0.01 ? 0.018 : 0;
        return top - bollard;
    };

    /// <summary>Sailing boats on the water at <paramref name="line"/>: (x, size) each.</summary>
    private static Profile Boats(double line, params (double X, double Size)[] boats) => x =>
    {
        var top = 1.5;
        foreach (var (bx, size) in boats)
        {
            var dx = x - bx;
            var hull = 0.065 * size;
            var deck = 0.016 * size;
            if (Math.Abs(dx) < hull)
            {
                // The hull's ends slope in; the mast stands in the middle; the sail is a triangle aft of it.
                var slope = Math.Min(1, (hull - Math.Abs(dx)) / (deck * 1.4));
                top = Math.Min(top, line - (deck * slope));
            }

            var mastTop = line - deck - (0.15 * size);
            if (Math.Abs(dx) < 0.0018 * Math.Max(size, 0.8))
            {
                top = Math.Min(top, mastTop);
            }

            var sail = 0.055 * size;
            if (dx > 0.003 && dx < sail)
            {
                top = Math.Min(top, mastTop + (0.012 * size) + ((0.13 * size) * (dx / sail)));
            }

            if (dx < -0.003 && dx > -sail * 0.6)
            {
                top = Math.Min(top, mastTop + (0.03 * size) + ((0.11 * size) * (-dx / (sail * 0.6))));
            }
        }

        return top;
    };
}
