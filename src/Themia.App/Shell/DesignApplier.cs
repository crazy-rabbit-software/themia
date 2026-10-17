using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.UI.ViewManagement;
using Color = Windows.UI.Color;

namespace Themia.App.Shell;

/// <summary>
/// The appearance engine: the one place the app-wide half of <see cref="DesignState"/> is turned
/// into what XAML draws — the theme, the accent, the widget font and the text size. The per-card
/// half (fill, border, header fill, corner radius) is <see cref="WidgetCard.ApplyDesign"/>, reading
/// the answers worked out here and in <see cref="DesignLook"/>. [S-820]..[S-829]
/// <para>
/// <b>Everything changes in place.</b> A brush handed out by <see cref="Brush"/> is one shared
/// <see cref="SolidColorBrush"/> per resource key whose colour is rewritten when the theme or the
/// accent changes, and an accent override rewrites the colour of the framework's own accent
/// brushes, so every <c>{ThemeResource AccentFill…}</c> in a control template follows too. Nothing is
/// rebuilt, no card is re-created and no widget's tick restarts.
/// </para>
/// <para>
/// Why the brushes are ours: a brush looked up from code is the brush of the <i>application's</i>
/// theme, which is fixed when the app starts. Themia's own Light/Dark choice is an element theme,
/// so code-made text in a light card stayed white. The colour comes instead from the Fluent
/// <i>colour</i> resource of Themia's effective theme (see <see cref="Resolve"/>).
/// </para>
/// <para>
/// One rule this class keeps: it never reads a <b>brush</b> out of a theme dictionary other than the
/// application's own, except the accent brushes it immediately recolours. Reading one creates it, and
/// a brush created from code resolves its <c>{ThemeResource}</c> colour against the application's
/// theme — so the Light dictionary's text brush, once read, is white for good and every light
/// control template that uses it draws white text. Colour resources are plain values and are safe.
/// </para>
/// <para>UI thread only. All state is static because the resources it edits are app-wide.</para>
/// </summary>
internal static class DesignApplier
{
    private const string Scope = "design";

    /// <summary>The theme dictionary names WinUI uses: dark is "Default" (some sets also say "Dark").</summary>
    private static readonly string[] DarkThemes = ["Default", "Dark"];

    private static readonly string[] LightThemes = ["Light"];

    /// <summary>One brush per key, recoloured in place. See the class summary.</summary>
    private static readonly Dictionary<string, SolidColorBrush> Themed = new(StringComparer.Ordinal);

    /// <summary>Plain colour brushes for the user's own colours, shared by every card.</summary>
    private static readonly Dictionary<(string Hex, byte Alpha), SolidColorBrush> Plain = [];

    /// <summary>The <c>SystemAccentColor*</c> resource keys, in <see cref="AccentShades.ToArray"/> order.</summary>
    private static readonly string[] AccentKeys =
    [
        "SystemAccentColorDark3", "SystemAccentColorDark2", "SystemAccentColorDark1", "SystemAccentColor",
        "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3",
    ];

    /// <summary>Windows' shades when the framework's resources were first read: what "is the accent" means.</summary>
    private static Color[]? _systemShades;

    /// <summary>The shades now in force.</summary>
    private static Color[]? _shades;

    private static UISettings? _ui;
    private static bool _applied;
    private static bool _dark = true;
    private static string _accentKey = string.Empty;
    private static string? _fontName;

    /// <summary>Themia's effective theme: the user's choice, or Windows' when they chose System.</summary>
    public static ElementTheme Theme => _dark ? ElementTheme.Dark : ElementTheme.Light;

    public static bool IsDark => _dark;

    /// <summary>The font widget content is drawn in, or null for the system's own.</summary>
    public static FontFamily? ContentFont { get; private set; }

    /// <summary>The design last applied (the app's own, live), for a debug view that shows it.</summary>
    public static DesignState? Current { get; private set; }

    private static UISettings Ui => _ui ??= new UISettings();

    /// <summary>
    /// Bring the app-wide appearance up to <paramref name="design"/>. Cheap when nothing it owns
    /// changed, which is most calls: it runs on every state change, ahead of the surfaces' relayout.
    /// </summary>
    public static void Apply(DesignState design)
    {
        ArgumentNullException.ThrowIfNull(design);
        Current = design;

        try
        {
            var dark = design.Theme switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => SystemIsDark(),
            };

            var shades = AccentFor(design.PrimaryColor);
            var accentKey = string.Join(',', shades.Select(c => c.ToString()));

            var accentChanged = !_applied || accentKey != _accentKey;
            var themeChanged = !_applied || dark != _dark;

            if (accentChanged)
            {
                ApplyAccent(shades);
                _accentKey = accentKey;
            }

            _dark = dark;
            _applied = true;

            if (accentChanged || themeChanged)
            {
                RefreshThemed();
            }

            // [S-6510] The app-wide item layer, for an item painted before it stands in a card.
            Widgets.ItemPalette.App.Apply(design.ItemBackground ?? new FillChoice(), ItemLook.TextColour(design, null), dark);

            var fontName = DesignLook.FontFamilyName(design.FontFamily);
            if (!string.Equals(fontName, _fontName, StringComparison.Ordinal) || (fontName is not null && ContentFont is null))
            {
                _fontName = fontName;
                ContentFont = fontName is null ? null : new FontFamily(fontName);
            }

            FluentType.SetTextScale(design.FontScale);
        }
        catch (Exception ex)
        {
            // A design that cannot be applied leaves the last one on screen, which is still a
            // working desktop; it must never take the surface down.
            Log.Error(Scope, "applying the appearance failed; the previous look stays", ex);
        }
    }

    /// <summary>
    /// Re-run <paramref name="changed"/> on the UI thread when Windows' theme or accent changes, so
    /// "use system setting" and "Windows accent colour" follow Windows live. Once per process.
    /// </summary>
    public static void Watch(DispatcherQueue dispatcher, Action changed)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(changed);

        Ui.ColorValuesChanged += (_, _) =>
        {
            // Raised on a background thread; brushes belong to the UI thread.
            if (!dispatcher.TryEnqueue(() => changed()))
            {
                Log.Warn(Scope, "could not queue a Windows colour change; the look follows on the next change");
            }
        };
    }

    /// <summary>
    /// [S-822] The stock Fluent brush for <paramref name="key"/> in Themia's theme, as a brush that
    /// follows later theme and accent changes in place. A key that is not a plain colour (an acrylic,
    /// a gradient) is handed out as the framework has it.
    /// </summary>
    public static Brush Brush(string key, Color fallback)
    {
        if (Themed.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (Resolve(key, _dark) is { } resolved)
        {
            var brush = new SolidColorBrush(resolved.Color) { Opacity = resolved.Opacity };
            Themed[key] = brush;
            return brush;
        }

        if (Application.Current.Resources.TryGetValue(key, out var other) && other is Brush plain)
        {
            return plain;
        }

        // A missing key is a typo or a renamed system resource, and the literal that replaces it is
        // a colour that will not follow the theme. Worth saying out loud rather than quietly wrong.
        Log.Warn(Scope, $"theme resource '{key}' missing; using a literal colour");
        return new SolidColorBrush(fallback);
    }

    /// <summary>
    /// A brush for one of the user's own <c>#RRGGBB</c> colours at an opacity, shared by every card
    /// that asks for the same pair. Null when the setting is empty or not a colour (the theme's).
    /// </summary>
    public static SolidColorBrush? UserBrush(string? hex, double opacity = 1)
    {
        if (!HexColor.TryParse(hex, out var r, out var g, out var b))
        {
            return null;
        }

        var alpha = FillChoice.Alpha(opacity);
        var key = (HexColor.From(r, g, b), alpha);

        if (!Plain.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
            Plain[key] = brush;
        }

        return brush;
    }

    // -----------------------------------------------------------------------------------------
    // Theme
    // -----------------------------------------------------------------------------------------

    /// <summary>Windows' app mode: a dark background colour means dark mode. The documented test.</summary>
    private static bool SystemIsDark()
    {
        var background = Ui.GetColorValue(UIColorType.Background);
        return background.R + background.G + background.B < 3 * 128;
    }

    private static bool AppIsDark => Application.Current.RequestedTheme == ApplicationTheme.Dark;

    private static void RefreshThemed()
    {
        foreach (var (key, brush) in Themed)
        {
            if (Resolve(key, _dark) is { } source)
            {
                brush.Color = source.Color;
                brush.Opacity = source.Opacity;
            }
        }
    }

    /// <summary>
    /// A plain-colour brush key's colour and opacity in one theme, or null when the key is not a
    /// plain colour. In order: an accent brush is its shade of the accent in force
    /// (<see cref="DesignLook.AccentShade"/>); a Fluent brush is its colour resource
    /// (<c>TextFillColorPrimaryBrush</c> → <c>TextFillColorPrimary</c>) in that theme; anything else
    /// is the application theme's brush, which is right when the themes agree (and logged when not).
    /// The opacity is always the application theme's brush's own — it does not vary by theme.
    /// </summary>
    private static (Color Color, double Opacity)? Resolve(string key, bool dark)
    {
        var appBrush = Application.Current.Resources.TryGetValue(key, out var found) ? found as SolidColorBrush : null;
        Color color;

        if (DesignLook.AccentShade(key, dark) is int shade && _shades is { } shades)
        {
            color = shades[shade];
        }
        else if (key.EndsWith("Brush", StringComparison.Ordinal) && ThemeColor(key[..^5], dark) is Color value)
        {
            color = value;

            // A colour resource that is one of Windows' accent shades is the accent: the override
            // reaches it too.
            if (_systemShades is { } system && _shades is { } current && ShadeOf(value, system) is int accentShade)
            {
                color = Color.FromArgb(value.A, current[accentShade].R, current[accentShade].G, current[accentShade].B);
            }
        }
        else if (appBrush is not null)
        {
            if (dark != AppIsDark)
            {
                Log.Debug(Scope, $"'{key}' has no colour resource; it shows the app theme's colour in the other theme");
            }

            color = appBrush.Color;
        }
        else
        {
            return null;
        }

        return (color, appBrush?.Opacity ?? 1);
    }

    /// <summary>A <see cref="Color"/> resource in one theme, from the theme dictionaries anywhere in the app's resources.</summary>
    private static Color? ThemeColor(string key, bool dark)
    {
        foreach (var theme in dark ? DarkThemes : LightThemes)
        {
            if (FindInTheme(Application.Current.Resources, theme, key) is Color color)
            {
                return color;
            }
        }

        return null;
    }

    /// <summary>
    /// A value from a theme dictionary: the dictionary's own, then its merged ones, as XAML resolves
    /// it. Only ever called for colour keys and the accent brush keys — see the class summary.
    /// </summary>
    private static object? FindInTheme(ResourceDictionary dictionary, string theme, string key)
    {
        if (dictionary.ThemeDictionaries.TryGetValue(theme, out var themed)
            && themed is ResourceDictionary themeDictionary
            && themeDictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (FindInTheme(merged, theme, key) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // -----------------------------------------------------------------------------------------
    // Accent
    // -----------------------------------------------------------------------------------------

    /// <summary>The seven shades: the user's override derived in Core, or Windows' own.</summary>
    private static Color[] AccentFor(string? hex)
    {
        if (HexColor.TryParse(hex, out var r, out var g, out var b))
        {
            return [.. DesignLook.Shades(new RgbColor(r, g, b)).ToArray().Select(c => Color.FromArgb(255, c.R, c.G, c.B))];
        }

        return SystemShades();
    }

    private static Color[] SystemShades() =>
    [
        Ui.GetColorValue(UIColorType.AccentDark3),
        Ui.GetColorValue(UIColorType.AccentDark2),
        Ui.GetColorValue(UIColorType.AccentDark1),
        Ui.GetColorValue(UIColorType.Accent),
        Ui.GetColorValue(UIColorType.AccentLight1),
        Ui.GetColorValue(UIColorType.AccentLight2),
        Ui.GetColorValue(UIColorType.AccentLight3),
    ];

    /// <summary>
    /// [S-820] Recolour the framework's accent brushes in both themes (alpha and opacity kept), and set
    /// the <c>SystemAccentColor*</c> resources for anything that looks them up later. Control
    /// templates reach the accent through these brushes (a toggle's "on" fill is an alias of
    /// <c>AccentFillColorDefaultBrush</c>), so recolouring the brush recolours every control.
    /// </summary>
    private static void ApplyAccent(Color[] shades)
    {
        _systemShades ??= SystemShades();
        _shades = shades;

        var recoloured = 0;
        foreach (var (themes, dark) in new[] { (DarkThemes, true), (LightThemes, false) })
        {
            foreach (var theme in themes)
            {
                foreach (var key in DesignLook.AccentBrushKeys)
                {
                    if (FindInTheme(Application.Current.Resources, theme, key) is SolidColorBrush brush
                        && DesignLook.AccentShade(key, dark) is int shade)
                    {
                        var c = shades[shade];
                        brush.Color = Color.FromArgb(brush.Color.A, c.R, c.G, c.B);
                        recoloured++;
                    }
                }
            }
        }

        var resources = Application.Current.Resources;
        for (var i = 0; i < AccentKeys.Length; i++)
        {
            resources[AccentKeys[i]] = shades[i];
        }

        Log.Debug(Scope, $"accent applied to {recoloured} framework brushes");
    }

    private static int? ShadeOf(Color color, Color[] shades)
    {
        for (var i = 0; i < shades.Length; i++)
        {
            if (color.R == shades[i].R && color.G == shades[i].G && color.B == shades[i].B)
            {
                return i;
            }
        }

        return null;
    }
}
