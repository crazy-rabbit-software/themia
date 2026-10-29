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

    /// <summary>The header's own box: the inset on three sides and the gap under its line.</summary>
    public static Thickness HeaderInset { get; } = new(FamilySpace.Inset, FamilySpace.Inset, FamilySpace.Inset, FamilySpace.HeaderGap);

    /// <summary>The theme brush of <paramref name="role"/>: the shared object, recoloured with the theme.</summary>
    public static Brush Brush(ColourRole role) => ThemeBrush.Of(ColourRoles.Key(role), default);

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

    /// <summary>Set <paramref name="block"/> on <paramref name="step"/>: size, line (exactly) and weight together.</summary>
    public static void Step(TextBlock block, TypeStep step)
    {
        ArgumentNullException.ThrowIfNull(block);
        block.FontSize = step.FontSize;
        block.LineHeight = step.LineHeight;
        block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        block.FontWeight = step.SemiBold ? FontWeights.SemiBold : FontWeights.Normal;
    }

    /// <summary>A run on <paramref name="step"/>'s size and weight in <paramref name="role"/> (a unit, a delta).</summary>
    public static Microsoft.UI.Xaml.Documents.Run Run(TypeStep step, ColourRole role, string text) => new()
    {
        Text = text,
        FontSize = step.FontSize,
        FontWeight = step.SemiBold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = Brush(role),
    };

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

    /// <summary>
    /// [S-6705] A data colour (a calendar's, an app's, a pen, a photo's): not a token but the data's own,
    /// as <c>#RRGGBB</c>, for a 2 px bar, a dot or a series only. Built on <see cref="HexBrush"/>, the one
    /// place a user's colour becomes a brush ([S-566]), and marked, so the render walk tells a data colour
    /// from a typed one; a value that is not a colour is the tertiary text role, logged.
    /// </summary>
    public static Brush Data(string hex)
    {
        if (HexBrush.Of(hex) is not { } brush)
        {
            Log.Warn(Scope, $"data colour '{hex}' is not #RRGGBB; drawn in the tertiary role");
            return Brush(ColourRole.TextTertiary);
        }

        DataBrushes.AddOrUpdate(brush, true);
        return brush;
    }

    /// <summary>Whether <paramref name="brush"/> came from <see cref="Data"/>: the render walk allows it as a data colour ([S-6731]).</summary>
    public static bool IsData(Brush? brush) => brush is not null && DataBrushes.TryGetValue(brush, out _);
}
