using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// The Appearance page: a style preset, then four groups ([S-3830]: theme and colours, cards,
/// text and size, layout) and the motion. Every setting here takes effect live through the appearance engine
/// (<see cref="DesignApplier"/>, <see cref="WidgetCard.ApplyDesign"/>). [S-820]..[S-834]
/// </summary>
internal static partial class ConfigPages
{
    /// <summary>The value the style choice uses for "Custom": no preset matches the design.</summary>
    private const int CustomPreset = -1;

    private static FrameworkElement BuildAppearance(AppHost host)
    {
        var page = Page(Str.Settings_Section_Appearance.Text(), []);
        FillAppearance(page, host);
        return page;
    }

    /// <summary>
    /// The rows, built into <paramref name="page"/>. A preset changes a dozen settings at once, so
    /// picking one rebuilds the rows in place to show the new values; nothing else does, so a
    /// slider is never torn down under the pointer.
    /// </summary>
    private static void FillAppearance(SettingsPage page, AppHost host)
    {
        var design = host.State.Design;
        page.Children.Clear();

        var rows = new List<FrameworkElement>
        {
            // [S-834] One pick sets the look; changing anything it owns afterwards reads as Custom.
            FluentLayout.Row(Glyphs.Palette, Str.Settings_Nav_Style.Text(), Str.Shell_Appearance_StyleDesc.Text(),
                PresetChoice(page, host)),

            // [S-3830] Four groups, each folded under one card as Settings folds related options: the
            // group's headline setting is the header's own control where it has one.
            FluentLayout.Expander(Glyphs.Theme, Str.Shell_Appearance_ThemeColours.Text(), Str.Shell_Appearance_ThemeDesc.Text(),
                FluentLayout.Choice(
                    [(AppTheme.System, Str.Shell_Appearance_UseSystem.Text()), (AppTheme.Light, Str.Settings_Theme_Light.Text()), (AppTheme.Dark, Str.Settings_Theme_Dark.Text())],
                    design.Theme,
                    value => host.Mutate(s => s.Design.Theme = value)),
                [
                    FluentLayout.ColorPickerRow(Glyphs.Palette, Str.Shell_Appearance_Accent.Text(), Str.Shell_Appearance_AccentDesc.Text(),
                        design.PrimaryColor, value => host.Mutate(s => s.Design.PrimaryColor = value),
                        new ColorChoiceOptions { DefaultLabel = Str.Shell_Appearance_WindowsAccent.Text(), DefaultColor = ColorChoice.WindowsAccent }),

                    FluentLayout.ColorPickerRow(Glyphs.Surface, Str.Settings_Style_WidgetBg_Label.Text(), Str.Shell_Appearance_WidgetBgDesc.Text(),
                        design.WidgetBackgroundColor, value => host.Mutate(s => s.Design.WidgetBackgroundColor = value),
                        new ColorChoiceOptions { DefaultLabel = Str.Shell_Appearance_ThemeColour.Text(), Presets = ColorPresets.Surfaces }),

                    FluentLayout.Row(Glyphs.Opacity, Str.Settings_Style_WidgetOpacity_Label.Text(), null,
                        FluentLayout.Slider(design.SurfaceOpacity * 100, 20, 100, 5, "%",
                            value => host.Mutate(s => s.Design.SurfaceOpacity = value / 100))),

                    // [S-828] The header strip, on its own terms, as in the original.
                    FluentLayout.ColorPickerRow(Glyphs.Surface, Str.Settings_Style_HeaderBg_Label.Text(), Str.Shell_Appearance_HeaderBgDesc.Text(),
                        design.HeaderBackgroundColor, value => host.Mutate(s => s.Design.HeaderBackgroundColor = value),
                        new ColorChoiceOptions { DefaultLabel = Str.Shell_Appearance_SameAsWidget.Text(), Presets = ColorPresets.Surfaces }),

                    FluentLayout.Row(Glyphs.Opacity, Str.Settings_Style_HeaderOpacity_Label.Text(), Str.Shell_Appearance_HeaderFade.Format(("percent", LocaleFormat.Percent(Strings.Current, 100))),
                        FluentLayout.Slider(design.HeaderOpacity * 100, 0, 100, 5, "%",
                            value => host.Mutate(s => s.Design.HeaderOpacity = value / 100))),
                ]),

            // [S-6516] The item background: the second layer, what the rows, tiles and chips inside
            // every card are filled with, whatever the card's own background is.
            FluentLayout.Expander(Glyphs.Surface, Str.Shell_Items_Background.Text(), Str.Shell_Items_BackgroundDesc.Text(), null,
                [
                    FluentLayout.ColorPickerRow(Glyphs.Palette, Str.Shell_Items_Colour.Text(), null,
                        design.ItemBackground.Color, value => host.Mutate(s => s.Design.ItemBackground.Color = value),
                        new ColorChoiceOptions { DefaultLabel = Str.Shell_Appearance_ThemeColour.Text(), Presets = ColorPresets.Surfaces }),

                    FluentLayout.Row(Glyphs.Opacity, Str.Shell_Items_Opacity.Text(), null,
                        FluentLayout.Slider(design.ItemBackground.Opacity * 100, 0, 100, 5, "%",
                            value => host.Mutate(s => s.Design.ItemBackground.Opacity = value / 100))),
                ]),

            // The card's shape and its border: the border style is the group's headline.
            FluentLayout.Expander(Glyphs.Grid, Str.Shell_Appearance_Cards.Text(), Str.Shell_Appearance_CardsDesc.Text(),
                FluentLayout.Choice(
                    [
                        (WidgetBorderStyle.None, Str.Settings_Style_None.Text()),
                        (WidgetBorderStyle.Solid, Str.Settings_Style_Solid.Text()),
                        (WidgetBorderStyle.Dashed, Str.Settings_Style_Dashed.Text()),
                        (WidgetBorderStyle.Dotted, Str.Settings_Style_Dotted.Text()),
                    ],
                    design.BorderStyle,
                    value => host.Mutate(s => s.Design.BorderStyle = value)),
                [
                    FluentLayout.Row(Glyphs.Surface, Str.Settings_Style_CornerRadius_Label.Text(), null,
                        FluentLayout.Slider(design.CornerRadiusPx, 0, 32, 1, "px",
                            value => host.Mutate(s => s.Design.CornerRadiusPx = value))),

                    FluentLayout.Row(Glyphs.Grid, Str.Settings_Style_BorderWidth_Label.Text(), null,
                        FluentLayout.Slider(design.BorderWidthPx, 0, 8, 1, "px",
                            value => host.Mutate(s => s.Design.BorderWidthPx = value))),

                    FluentLayout.ColorPickerRow(Glyphs.Palette, Str.Shell_Appearance_BorderColour.Text(), null,
                        design.BorderColor, value => host.Mutate(s => s.Design.BorderColor = value),
                        new ColorChoiceOptions { DefaultLabel = Str.Shell_Appearance_ThemeColour.Text(), Presets = ColorPresets.Surfaces }),

                    FluentLayout.Row(Glyphs.Opacity, Str.Settings_Style_BorderOpacity_Label.Text(), null,
                        FluentLayout.Slider(design.BorderOpacity * 100, 0, 100, 5, "%",
                            value => host.Mutate(s => s.Design.BorderOpacity = value / 100))),
                ]),

            FluentLayout.Expander(Glyphs.Font, Str.Shell_Appearance_TextAndSize.Text(), Str.Shell_Appearance_TextAndSizeDesc.Text(), null,
                [
                    FluentLayout.Row(Glyphs.Font, Str.Settings_Font_FamilyLabel.Text(), Str.Shell_Appearance_FontDesc.Text(),
                        FontChoice.Create(design.FontFamily, family => host.Mutate(s => s.Design.FontFamily = family)),
                        "typeface", "font"),

                    FluentLayout.Row(Glyphs.TextSize, Str.Shell_Appearance_TextSize.Text(), Str.Shell_Appearance_TextSizeDesc.Text(),
                        FluentLayout.Slider(design.FontScale * 100, FluentType.MinTextScale * 100, FluentType.MaxTextScale * 100, 5, "%",
                            value => host.Mutate(s => s.Design.FontScale = value / 100))),

                    FluentLayout.Row(Glyphs.Scale, Str.Display_WidgetScale_Label.Text(), Str.Shell_Appearance_WidgetScaleDesc.Text(),
                        FluentLayout.Slider(design.UiScale * 100, 50, 300, 5, "%",
                            value => host.Mutate(s => s.Design.UiScale = value / 100))),
                ]),

            // [S-1722] Where the widgets sit on the desktop: the gap between them and snapping to the grid.
            FluentLayout.Expander(Glyphs.GridLayout, Str.Shell_Appearance_Layout.Text(), Str.Shell_Appearance_LayoutDesc.Text(), null,
                [
                    FluentLayout.Row(Glyphs.Grid, Str.Shell_Layout_Gap.Text(), null,
                        FluentLayout.Slider(design.WidgetGapPx, 0, 48, 2, "px",
                            value => host.Mutate(s => s.Design.WidgetGapPx = value)),
                        "spacing", "margin"),

                    FluentLayout.Row(Glyphs.Grid, Str.Settings_Style_GridSnap_Label.Text(), Str.Shell_Layout_SnapDesc.Text(),
                        FluentLayout.Toggle(design.GridSnap, value => host.Mutate(s => s.Design.GridSnap = value)),
                        "grid", "snap", "align"),
                ]),
        };

        // [S-1014] The intros.
        rows.AddRange(MotionRows(host));
        page.With(rows);
    }

    /// <summary>
    /// [S-834] The presets, plus "Custom" while the design matches none of them. Choosing one writes
    /// its fields in one mutation and rebuilds the page so every row shows them.
    /// </summary>
    private static ComboBox PresetChoice(SettingsPage page, AppHost host)
    {
        var match = DesignPresets.Match(host.State.Design);
        var options = new List<(int Value, string Label)>();

        foreach (var preset in DesignPresets.All)
        {
            options.Add(((int)preset.Id, preset.Label));
        }

        if (match is null)
        {
            options.Add((CustomPreset, Str.Settings_Style_Preset_Custom.Text()));
        }

        return FluentLayout.Choice(options, match is { } id ? (int)id : CustomPreset, value =>
        {
            if (value == CustomPreset || !Enum.IsDefined((DesignPresetId)value))
            {
                return;
            }

            _ = PickPresetAsync(page, host, (DesignPresetId)value);
        });
    }

    /// <summary>
    /// [S-6483] Apply a picked preset, first asking when it would overwrite a look the user made
    /// (the design reads as Custom). Cancel changes nothing, and the rebuilt page shows Custom again.
    /// Its own method rather than an async lambda on the event: an exception out of an async void
    /// handler would take the process with it.
    /// </summary>
    private static async Task PickPresetAsync(SettingsPage page, AppHost host, DesignPresetId id)
    {
        try
        {
            if (DesignPresets.ReplacesCustomLook(host.State.Design, id))
            {
                var replace = await FluentLayout.ConfirmAsync(
                    page,
                    Str.Shell_Appearance_ReplaceLook_Title.Text(),
                    Str.Shell_Appearance_ReplaceLook_Body.Format(("preset", DesignPresets.Get(id).Label)),
                    Str.Shell_Appearance_ReplaceLook_Action.Text()).ConfigureAwait(true);

                if (!replace)
                {
                    RefreshAppearance(page, host);
                    return;
                }
            }

            host.Mutate(s => DesignPresets.Apply(s.Design, id));
            RefreshAppearance(page, host);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"could not apply the {id} style", ex);
        }
    }

    /// <summary>
    /// Rebuild the page after the selection change has finished: rebuilding inside it would tear down
    /// the combo box that is still raising the event.
    /// </summary>
    private static void RefreshAppearance(SettingsPage page, AppHost host)
    {
        if (page.DispatcherQueue is not { } queue || !queue.TryEnqueue(() => FillAppearance(page, host)))
        {
            Log.Warn(Scope, "could not refresh the Appearance page after a preset; reopen it to see the values");
        }
    }
}
