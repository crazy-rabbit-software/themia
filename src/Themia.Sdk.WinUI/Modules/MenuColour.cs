using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3724] What a colour line of the properties menu holds and what a pick in its submenu does: a
/// colour (<c>#RRGGBB</c>, or empty for the default) or a fill (<see cref="FillChoice"/>: Default,
/// Transparent or a colour at an opacity). The host draws every one as the same colour submenu,
/// the card's own Background, Item background and Item text among them ([S-3723]): Default (and a
/// fill's Transparent) with the dot, the swatches, a fill's Opacity, Custom colour… and Pick a colour
/// from the screen. The rules are here, so a module's tests can run a pick without a window.
/// </summary>
public sealed class MenuColour
{
    /// <summary>[S-3723] A fill's opacity steps, 100 % to 10 % in tens.</summary>
    public static IReadOnlyList<double> OpacitySteps { get; } = [1.0, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2, 0.1];

    private readonly Action<string>? _setColour;
    private readonly Action<FillChoice>? _setFill;

    private MenuColour(string? hex, FillChoice? fill, bool isFill, ColorChoiceOptions options, Action<string>? setColour, Action<FillChoice>? setFill)
    {
        Hex = hex;
        Fill = fill;
        IsFill = isFill;
        Options = options;
        _setColour = setColour;
        _setFill = setFill;
    }

    /// <summary>
    /// A colour: <paramref name="current"/> as stored (<c>#RRGGBB</c>, empty for the default, null for
    /// a selection whose colours differ). A setting that must hold a colour
    /// (<see cref="ColorChoiceOptions.DefaultLabel"/> null) but holds none shows its first swatch, as
    /// the settings' colour control does.
    /// </summary>
    public static MenuColour Of(string? current, Action<string> set, ColorChoiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(options);

        var hex = current is null ? null : HexColor.Normalise(current);
        if (hex is { Length: 0 } && options.DefaultLabel is null && options.Presets.Count > 0)
        {
            hex = HexColor.Normalise(options.Presets[0].Hex);
        }

        return new MenuColour(hex, null, false, options, set, null);
    }

    /// <summary>A fill: <paramref name="current"/> as stored, null for a selection whose fills differ.</summary>
    public static MenuColour OfFill(FillChoice? current, Action<FillChoice> set, FillChoiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(options);

        var fill = current?.Clone();

        // ignore: what Repair reports was logged when the state loaded; here it only keeps the line total.
        _ = fill?.Repair("menu fill");
        var colour = new ColorChoiceOptions { DefaultLabel = options.DefaultLabel, DefaultColor = options.DefaultColor, Presets = options.Presets };
        return new MenuColour(null, fill, true, colour, null, set);
    }

    /// <summary>A fill (Default, Transparent, Opacity), not a plain colour.</summary>
    public bool IsFill { get; }

    /// <summary>A colour's value, normalised (empty: the default); null when mixed or for a fill.</summary>
    public string? Hex { get; }

    /// <summary>A fill's value; null when mixed or for a colour.</summary>
    public FillChoice? Fill { get; }

    /// <summary>What the submenu offers and how Default is painted: its label (none: a colour is required), its colour, the swatches, Custom colour.</summary>
    public ColorChoiceOptions Options { get; }

    /// <summary>The selection's values differ: the line reads as a dash and nothing holds.</summary>
    public bool Mixed => IsFill ? Fill is null : Hex is null;

    /// <summary>The swatch that holds (ringed and ticked), or empty for none.</summary>
    public string Chosen => IsFill
        ? (Fill is { Kind: FillKind.Colour } colour ? HexColor.Normalise(colour.Colour) : string.Empty)
        : Hex ?? string.Empty;

    /// <summary>Default holds (the app-wide fill, or the colour that follows the theme).</summary>
    public bool DefaultHolds => IsFill ? Fill is { Kind: FillKind.Default } : Hex is { Length: 0 };

    /// <summary>A fill's Transparent holds.</summary>
    public bool TransparentHolds => Fill is { Kind: FillKind.Transparent };

    /// <summary>A fill holds a colour, so its Opacity means something.</summary>
    public bool HasOpacity => Fill is { Kind: FillKind.Colour };

    /// <summary>A fill's colour's opacity; 1 when it is not a colour (a colour picked next starts opaque).</summary>
    public double Opacity => Fill is { Kind: FillKind.Colour } colour ? colour.Opacity : 1.0;

    /// <summary>A swatch or the eyedropper: that colour (a fill keeps its opacity). The one that holds changes nothing.</summary>
    public void Choose(string hex)
    {
        var picked = HexColor.Normalise(hex);
        if (IsFill)
        {
            Store(FillChoice.Of(picked, Opacity));
        }
        else if (picked != Hex)
        {
            _setColour!(picked);
        }
    }

    /// <summary>Default: the app-wide fill, or the colour that follows the theme. Nothing for a colour that is required.</summary>
    public void ChooseDefault()
    {
        if (IsFill)
        {
            Store(FillChoice.Default());
        }
        else if (Options.DefaultLabel is not null && Hex is not { Length: 0 })
        {
            _setColour!(string.Empty);
        }
    }

    /// <summary>A fill's Transparent.</summary>
    public void ChooseTransparent()
    {
        if (IsFill)
        {
            Store(FillChoice.Transparent());
        }
    }

    /// <summary>A fill's opacity step, on the colour that holds; nothing while no colour holds.</summary>
    public void ChooseOpacity(double opacity)
    {
        if (Fill is { Kind: FillKind.Colour } colour)
        {
            Store(FillChoice.Of(colour.Colour, opacity));
        }
    }

    /// <summary>What the custom colour dialog gave: the colour, and for a fill its alpha as the opacity.</summary>
    public void ChooseCustom(string hex, double alpha)
    {
        if (IsFill)
        {
            Store(FillChoice.Of(hex, alpha));
        }
        else
        {
            Choose(hex);
        }
    }

    private void Store(FillChoice next)
    {
        if (!next.SameAs(Fill))
        {
            _setFill!(next);
        }
    }
}
