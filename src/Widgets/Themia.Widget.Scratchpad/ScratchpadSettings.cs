using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Widget.Scratchpad;

namespace Themia.App.Shell.Config;

/// <summary>
/// The scratchpad's settings: the pen, and whether the commands ride in the widget's header.
/// <para>
/// Clearing the drawing is <b>not</b> here. It lives on the pad itself, behind a confirmation,
/// because that is where the drawing is and because two ways to destroy the same thing is one too
/// many.
/// </para>
/// </summary>
internal static class ScratchpadSettings
{
    /// <summary>
    /// The inks the pen offers first: six that read on the pad in either theme. The shared picker
    /// ([S-1549]) puts them on top and keeps any other colour one "Custom colour" away.
    /// </summary>
    private static readonly ColorPreset[] PenColors =
        [.. InkPalette.Inks.Select(ink => new ColorPreset(ink.Hex, ink.Name.Local()))];

    public static void Rows(List<FrameworkElement> rows, ScratchpadConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(ScratchpadStr.Scratchpad_PenTab.Text()));

        rows.Add(FluentLayout.ColorPickerRow(Glyphs.Palette, ScratchpadStr.Scratchpad_PenColorLabel.Text(), ScratchpadStr.Scratchpad_PenColorDesc.Text(),
            config.EffectivePenColor,
            value => { config.PenColor = value; save(); },
            new ColorChoiceOptions { Presets = PenColors, PresetsTitle = ScratchpadStr.WidgetsC_Scratchpad_Settings_Inks.Text() }));

        // The width is in pixels and stays in pixels however the widget is resized: a pen has a
        // nib, so a bigger pad shows more drawing rather than a fatter line.
        rows.Add(FluentLayout.Row(ScratchpadModule.Glyph, ScratchpadStr.Scratchpad_PenSizeLabel.Text(), ScratchpadStr.Scratchpad_PenSizeDesc.Text(),
            FluentLayout.Slider(
                config.EffectivePenWidth,
                InkGeometry.MinWidth,
                InkGeometry.MaxWidth,
                1,
                "px",
                value => { config.PenWidth = value; save(); })));

        rows.Add(FluentLayout.Header(ScratchpadStr.WidgetsC_Scratchpad_Settings_Tools.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Edit, ScratchpadStr.WidgetsC_Scratchpad_Settings_ShowTools_Label.Text(),
            ScratchpadStr.WidgetsC_Scratchpad_Settings_ShowTools_Desc.Text(),
            FluentLayout.Toggle(config.ShowTools, value => { config.ShowTools = value; save(); })));
    }
}
