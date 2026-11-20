using System.Runtime.CompilerServices;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6700]..[S-6706] The family's tokens as WinUI parts: a text on a step in a role, a glyph at a
/// family size, a role's brush, the insets, and the one entry point for a data colour. Every block
/// is built from these and nothing else; a widget that draws a part of its own does too.
/// <para>
/// A text's line is the step's exactly (<see cref="LineStackingStrategy.BlockLineHeight"/>): with
/// the default, a large step's line grew to the font's own and a 192 stood 3 DIPs taller than its
/// token, so the rows under it slid.
/// </para>
/// </summary>
public static class Family
{
    private const string Scope = "family";

    /// <summary>The card's inset on every side.</summary>
    public static Thickness Inset { get; } = new(FamilySpace.Inset);

    /// <summary>The inset either side only (a row reaching both edges carries it as its padding).</summary>
    public static Thickness SideInset { get; } = new(FamilySpace.Inset, 0, FamilySpace.Inset, 0);

    /// <summary>
    /// The theme brush of <paramref name="role"/>: the shared object, recoloured with the theme. [S-2134] A role the look
    /// tokens recolour (a text level, a track, the accent) is the widgets' own brush of it (<see cref="ColourRoles.WidgetKey"/>).
    /// </summary>
    public static Brush Brush(ColourRole role) => ThemeBrush.Of(ColourRoles.WidgetKey(role), default);

    /// <summary>A text on <paramref name="step"/> in <paramref name="role"/>, one line, ending in an ellipsis.</summary>
    public static TextBlock Text(TypeStep step, ColourRole role = ColourRole.TextPrimary, string? text = null)
    {
        var block = new TextBlock
        {
            Foreground = Brush(role),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Text = text ?? string.Empty,
        };

        Step(block, step);
        return block;
    }

    /// <summary>
    /// Set <paramref name="block"/> on <paramref name="step"/>: size, line (exactly) and weight together. [S-2135] [S-2136]
    /// The weight is the design's for the step's role and the tracking and figures its type tokens' (<see cref="FamilyLook.Type"/>);
    /// the block is remembered, so a change of those tokens restyles it where it stands (<see cref="Restyle"/>).
    /// </summary>
    public static void Step(TextBlock block, TypeStep step)
    {
        ArgumentNullException.ThrowIfNull(block);
        block.FontSize = step.FontSize;
        block.LineHeight = step.LineHeight;
        block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        var typed = new Typed(FamilyType.BaseOf(step), step.SemiBold);
        Steps.AddOrUpdate(block, typed);
        Style(block, typed);
    }

    /// <summary>A run on <paramref name="step"/>'s size and weight in <paramref name="role"/> (a unit, a delta); [S-2135] its weight and tracking the design's, as a block's.</summary>
    public static Microsoft.UI.Xaml.Documents.Run Run(TypeStep step, ColourRole role, string text)
    {
        var run = new Microsoft.UI.Xaml.Documents.Run
        {
            Text = text,
            FontSize = step.FontSize,
            Foreground = Brush(role),
        };

        var typed = new Typed(FamilyType.BaseOf(step), step.SemiBold);
        Runs.AddOrUpdate(run, typed);
        Style(run, typed);
        return run;
    }

    /// <summary>What a text was set on: its step's base size (unscaled) and whether the step is strong.</summary>
    private sealed record Typed(double Base, bool Strong);

    /// <summary>[S-2135] Every block and run set on a step, with the step: what <see cref="Restyle"/> walks.</summary>
    private static readonly ConditionalWeakTable<TextBlock, Typed> Steps = [];

    private static readonly ConditionalWeakTable<Microsoft.UI.Xaml.Documents.Run, Typed> Runs = [];

    /// <summary>[S-14319] The blocks whose figures are tabular whatever the design's type tokens say (<see cref="Figures"/>).</summary>
    private static readonly ConditionalWeakTable<TextBlock, object> Figured = [];

    /// <summary>
    /// [S-14319] <paramref name="block"/> shows a reading that runs (a clock's time, a timer's count): its figures are
    /// tabular whatever the design's type tokens say, now and on every restyle, so it never jumps as it runs.
    /// </summary>
    public static void Figures(TextBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        Figured.AddOrUpdate(block, Figured);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular);
    }

    /// <summary>[S-14319] Whether <paramref name="block"/>'s figures are tabular: the design's, or a running reading's.</summary>
    public static bool Tabular(TextBlock block) => FamilyLook.Type.TabularNumerals || Figured.TryGetValue(block, out _);

    private static void Style(TextBlock block, Typed typed)
    {
        var look = FamilyLook.Type;
        block.FontWeight = Weight(look.WeightOf(typed.Base, typed.Strong));
        block.CharacterSpacing = (int)Math.Round(look.TrackingOf(typed.Base));
        if (look.TabularNumerals || Figured.TryGetValue(block, out _))
        {
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular);
        }
        else
        {
            block.ClearValue(Microsoft.UI.Xaml.Documents.Typography.NumeralAlignmentProperty);
        }
    }

    private static void Style(Microsoft.UI.Xaml.Documents.Run run, Typed typed)
    {
        var look = FamilyLook.Type;
        run.FontWeight = Weight(look.WeightOf(typed.Base, typed.Strong));
        run.CharacterSpacing = (int)Math.Round(look.TrackingOf(typed.Base));
    }

    /// <summary>[S-2135] The weight the type tokens chose (<see cref="TypeLook.WeightOf"/>), as XAML draws it: never a literal of a view's.</summary>
    private static Windows.UI.Text.FontWeight Weight(int tokenWeight)
    {
        var weight = default(Windows.UI.Text.FontWeight);
        weight.Weight = (ushort)Math.Clamp(tokenWeight, (int)TypeWeight.Light, (int)TypeWeight.Bold);
        return weight;
    }

    /// <summary>
    /// [S-2135] [S-2136] Bring every live text set on a step to the type tokens now in force (<see cref="FamilyLook.Type"/>;
    /// XAML's <c>CharacterSpacing</c> is in thousandths of an em, as the tracking tokens are): the host calls it once when
    /// they change. UI thread only.
    /// </summary>
    internal static void Restyle()
    {
        foreach (var (block, typed) in Steps)
        {
            Style(block, typed);
        }

        foreach (var (run, typed) in Runs)
        {
            Style(run, typed);
        }
    }

    /// <summary>A glyph of <paramref name="size"/> (16, 32, 48 or a Readout step) in <paramref name="role"/>, mirrored for a right-to-left language.</summary>
    public static FontIcon Glyph(string glyph, double size = FamilyShape.Icon, ColourRole role = ColourRole.TextSecondary)
        => UiLanguage.Mirrored(new FontIcon
        {
            Glyph = glyph,
            FontSize = size,
            Width = size,
            Height = size,
            Foreground = Brush(role),
            VerticalAlignment = VerticalAlignment.Center,
        });

    /// <summary>The one radius as a corner set.</summary>
    public static CornerRadius Radius { get; } = new(FamilyShape.Radius);

    /// <summary>A round corner set for a part <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static CornerRadius Round(double width, double height) => new(FamilyShape.Round(width, height));

    private static readonly ConditionalWeakTable<Brush, object> DataBrushes = [];

    /// <summary>[S-2133] One brush per data colour, so the rendering mode recolours them in place.</summary>
    private static readonly Dictionary<RgbColor, SolidColorBrush> DataByColour = [];

    /// <summary>[S-2133] What the rendering mode does to a data colour; null: nothing.</summary>
    private static Func<RgbColor, RgbColor>? _tone;

    /// <summary>
    /// [S-6705] A data colour (a calendar's, an app's, a pen, a photo's): not a token but the data's own,
    /// as <c>#RRGGBB</c>, for a 2 px bar, a dot or a series only. Its colour made by <see cref="HexBrush"/>, the one
    /// place a user's colour becomes one ([S-566]), one brush per colour, and marked, so the render walk tells a data
    /// colour from a typed one; a value that is not a colour is the tertiary text role, logged. [S-2133] Drawn as the
    /// design's rendering mode tones it (<see cref="Retone"/>).
    /// </summary>
    public static Brush Data(string hex)
    {
        if (!HexColor.TryParse(hex, out var r, out var g, out var b))
        {
            Log.Warn(Scope, $"data colour '{hex}' is not #RRGGBB; drawn in the tertiary role");
            return Brush(ColourRole.TextTertiary);
        }

        var colour = new RgbColor(r, g, b);
        if (!DataByColour.TryGetValue(colour, out var brush))
        {
            brush = new SolidColorBrush(HexBrush.Colour(_tone?.Invoke(colour) ?? colour));
            DataByColour[colour] = brush;
            DataBrushes.AddOrUpdate(brush, true);
        }

        return brush;
    }

    /// <summary>[S-2133] Tone every data colour by <paramref name="tone"/> (null: as it is), in place: the host calls it when the rendering mode or the accent changes. UI thread only.</summary>
    internal static void Retone(Func<RgbColor, RgbColor>? tone)
    {
        _tone = tone;
        foreach (var (colour, brush) in DataByColour)
        {
            brush.Color = HexBrush.Colour(tone?.Invoke(colour) ?? colour);
        }
    }


    /// <summary>Whether <paramref name="brush"/> came from <see cref="Data"/>: the render walk allows it as a data colour ([S-6731]).</summary>
    public static bool IsData(Brush? brush) => brush is not null && DataBrushes.TryGetValue(brush, out _);
}
