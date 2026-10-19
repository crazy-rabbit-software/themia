using System.Globalization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Scratchpad;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1501] The scratchpad's options, all on its properties menu ([S-3719]): Appearance ([S-3723]),
/// the pen's colour and size, and whether the tools ride in the widget's header. It has no settings page.
/// <para>
/// Clearing the drawing is <b>not</b> here. It lives on the pad itself, behind a confirmation,
/// because that is where the drawing is and because two ways to destroy the same thing is one too
/// many.
/// </para>
/// </summary>
internal static class ScratchpadMenu
{
    /// <summary>
    /// The inks the pen offers first: six that read on the pad in either theme ([S-1497]), the same
    /// as the ink bar's. Any other colour is one "Custom colour" away.
    /// </summary>
    private static ColorPreset[] PenColors() =>
        [.. InkPalette.Inks.Select(ink => new ColorPreset(ink.Hex, ink.Name.Local()))];

    public static void Lines(WidgetMenuLines menu, ScratchpadConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();

        // A pen always has a colour: no Default line.
        menu.Lines.Add(menu.Colour(ScratchpadStr.Scratchpad_PenColorLabel.Text(), config.EffectivePenColor, value => config.PenColor = value,
            new ColorChoiceOptions { DefaultLabel = null, Presets = PenColors() }));

        // The width is in pixels and stays in pixels however the widget is resized: a pen has a nib,
        // so a bigger pad shows more drawing rather than a fatter line.
        menu.Lines.Add(menu.Number(ScratchpadModule.Glyph, ScratchpadStr.Scratchpad_PenSizeLabel.Text(),
            config.EffectivePenWidth, InkGeometry.MinWidth, InkGeometry.MaxWidth, 1d, Pixels, value => config.PenWidth = value));

        menu.Lines.Add(menu.Toggle(Glyphs.EraseTool, ScratchpadStr.WidgetsC_Scratchpad_Settings_ShowTools_Label.Text(), config.ShowTools, on => config.ShowTools = on));
    }

    /// <summary>"3 px".</summary>
    internal static string Pixels(double width) => string.Create(CultureInfo.InvariantCulture, $"{width:0.#} px");
}
