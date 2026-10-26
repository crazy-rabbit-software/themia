using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3526] Appearance ›: the look of every widget, in the groups the Appearance page had ([S-3830]):
/// Style (the presets, [S-834]), Theme and colours, Item background, Cards (the gap between them
/// with them), Text and size, then Motion ([S-3529]). Snap is the edit bar's ([S-3516]) and a scale
/// a display's or a widget's ([S-3590]): neither is here. Every line writes the very field the page wrote, through one
/// <see cref="ISettingsMenuHost.Mutate"/>, and the appearance engine applies it live.
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-3526] The Appearance line and its submenu.</summary>
    public static SettingsSubmenu Appearance(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        const string at = Keys.Appearance;
        return new SettingsSubmenu(at, Str.Settings_Section_Appearance.Text(), Glyphs.Palette,
        [
            Style(host, at + "/style"),
            new SettingsSeparator(at + "/-style"),
            ThemeAndColours(host, at + "/theme"),
            Items(host, at + "/items"),
            Cards(host, at + "/cards"),
            TextAndSize(host, at + "/text"),
            new SettingsSeparator(at + "/-motion"),
            Motion(host, at + "/motion"),
        ]);
    }

    /// <summary>
    /// [S-834] [S-3527] Style: the presets, and Custom (greyed, the dot on it) while the design matches
    /// none. A pick writes the preset's fields in one change; over a look the user made it asks
    /// first ([S-6483]), and Cancel changes nothing.
    /// </summary>
    private static SettingsChoice Style(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        var match = DesignPresets.Match(design);
        var options = new List<SettingsRadio>();
        foreach (var preset in DesignPresets.All)
        {
            var id = preset.Id;
            options.Add(new SettingsRadio(key + "/" + id, preset.Label, match == id, () => PickPreset(host, id)));
        }

        if (match is null)
        {
            options.Add(new SettingsRadio(key + "/custom", Str.Settings_Style_Preset_Custom.Text(), true, () => { }) { Disabled = true });
        }

        return new SettingsChoice(key, Str.Settings_Nav_Style.Text(), Glyphs.Palette, options);
    }

    /// <summary>[S-6483] Apply a preset; asked first where it would overwrite a look of the user's own.</summary>
    private static void PickPreset(ISettingsMenuHost host, DesignPresetId id)
    {
        var design = host.State.Design;
        if (DesignPresets.Match(design) == id)
        {
            return;
        }

        void Apply() => host.Mutate(s => DesignPresets.Apply(s.Design, id));

        if (DesignPresets.ReplacesCustomLook(design, id))
        {
            host.Confirm(new SettingsConfirm(
                Str.Shell_Appearance_ReplaceLook_Title.Text(),
                Str.Shell_Appearance_ReplaceLook_Action.Text(),
                Apply,
                Str.Shell_Appearance_ReplaceLook_Body.Format(("preset", DesignPresets.Get(id).Label))));
            return;
        }

        Apply();
    }

    /// <summary>[S-3830] Theme and colours: the theme, the accent, the widget background and the header's fill and opacity ([S-828]).</summary>
    private static SettingsSubmenu ThemeAndColours(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        var theme = Choice(host, key + "/theme", Str.Settings_Theme_Label.Text(), Glyphs.Theme,
            [(AppTheme.System, Str.Shell_Appearance_UseSystem.Text()), (AppTheme.Light, Str.Settings_Theme_Light.Text()), (AppTheme.Dark, Str.Settings_Theme_Dark.Text())],
            design.Theme, (s, v) => s.Design.Theme = v);

        return new SettingsSubmenu(key, Str.Shell_Appearance_ThemeColours.Text(), Glyphs.Theme,
        [
            theme,
            new SettingsSeparator(key + "/-colours"),
            new SettingsColour(key + "/accent", Str.Shell_Appearance_Accent.Text(), design.PrimaryColor,
                v => host.Mutate(s => s.Design.PrimaryColor = v), Str.Shell_Appearance_WindowsAccent.Text(), ColorPresets.Windows) { DefaultIsWindowsAccent = true },

            // [S-6568] The widget background: the Windows card, none, or a colour at an opacity. Its
            // group says whose: "Background" here, as on the page's group, so the words fit the menu.
            new SettingsFill(key + "/background", Str.Settings_Style_BgLabel.Text(), design.Background,
                v => host.Mutate(s => s.Design.Background = v), Str.Shell_Appearance_ThemeColour.Text()),
            new SettingsSeparator(key + "/-header"),

            // [S-828] The header strip, on its own terms: its fill (the line "Header", its value the colour) and its opacity.
            new SettingsColour(key + "/headerBackground", Str.App_ConfigHeaderLabel.Text(), design.HeaderBackgroundColor,
                v => host.Mutate(s => s.Design.HeaderBackgroundColor = v), Str.Shell_Appearance_SameAsWidget.Text(), ColorPresets.Surfaces),
            new SettingsSlider(key + "/headerOpacity", Str.Settings_Style_HeaderOpacity_Label.Text(), Glyphs.Opacity, design.HeaderOpacity, 0, 1, 0.05, Percent,
                v => host.Mutate(s => s.Design.HeaderOpacity = v)),
        ]);
    }

    /// <summary>[S-6516] [S-6545] Item background: what the rows, tiles and chips inside every card are filled with, and the text on them.</summary>
    private static SettingsSubmenu Items(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        return new SettingsSubmenu(key, Str.Shell_Items_Background.Text(), Glyphs.Surface,
        [
            new SettingsFill(key + "/background", Str.Settings_Style_BgLabel.Text(), design.ItemBackground,
                v => host.Mutate(s => s.Design.ItemBackground = v), Str.Shell_Appearance_ThemeColour.Text()),
            new SettingsColour(key + "/text", Str.Shell_Items_Text.Text(), design.ItemTextColor,
                v => host.Mutate(s => s.Design.ItemTextColor = v), Str.Shell_Appearance_ThemeColour.Text(), ColorPresets.Surfaces),
        ]);
    }

    /// <summary>[S-3830] [S-1722] Cards: the border's style (the line's value), the corners, the gap between the cards, the border's width, colour and opacity.</summary>
    private static SettingsSubmenu Cards(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        var border = Choice(host, key + "/border", Str.Settings_Style_Border.Text(), Glyphs.Grid,
            [
                (WidgetBorderStyle.None, Str.Settings_Style_None.Text()),
                (WidgetBorderStyle.Solid, Str.Settings_Style_Solid.Text()),
                (WidgetBorderStyle.Dashed, Str.Settings_Style_Dashed.Text()),
                (WidgetBorderStyle.Dotted, Str.Settings_Style_Dotted.Text()),
            ],
            design.BorderStyle, (s, v) => s.Design.BorderStyle = v);

        return new SettingsSubmenu(key, Str.Shell_Appearance_Cards.Text(), Glyphs.Grid,
        [
            new SettingsSlider(key + "/radius", Str.Settings_Style_CornerRadius_Label.Text(), Glyphs.Surface, design.CornerRadiusPx, 0, 32, 1, v => Unit(v, "px"),
                v => host.Mutate(s => s.Design.CornerRadiusPx = v)),
            new SettingsSlider(key + "/gap", Str.Shell_Layout_Gap.Text(), Glyphs.ColumnsLayout, design.WidgetGapPx, 0, 48, 2, v => Unit(v, "px"),
                v => host.Mutate(s => s.Design.WidgetGapPx = v)),
            new SettingsSeparator(key + "/-border"),
            border,
            new SettingsSlider(key + "/borderWidth", Str.Settings_Style_BorderWidth_Label.Text(), Glyphs.Fit, design.BorderWidthPx, 0, 8, 1, v => Unit(v, "px"),
                v => host.Mutate(s => s.Design.BorderWidthPx = v)),
            new SettingsColour(key + "/borderColour", Str.Shell_Appearance_BorderColour.Text(), design.BorderColor,
                v => host.Mutate(s => s.Design.BorderColor = v), Str.Shell_Appearance_ThemeColour.Text(), ColorPresets.Surfaces),
            new SettingsSlider(key + "/borderOpacity", Str.Settings_Style_BorderOpacity_Label.Text(), Glyphs.Opacity, design.BorderOpacity, 0, 1, 0.05, Percent,
                v => host.Mutate(s => s.Design.BorderOpacity = v)),
        ]) { Value = border.Value };
    }

    /// <summary>[S-3830] Text and size: the font ([S-3528]) and the text size, the user's accessibility choice.</summary>
    private static SettingsSubmenu TextAndSize(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        return new SettingsSubmenu(key, Str.Shell_Appearance_TextAndSize.Text(), Glyphs.Font,
        [
            new SettingsFont(key + "/font", Str.Settings_Font_FamilyLabel.Text(), Glyphs.Font, design.FontFamily,
                v => host.Mutate(s => s.Design.FontFamily = v)),
            new SettingsSlider(key + "/size", Str.Shell_Appearance_TextSize.Text(), Glyphs.TextSize, design.FontScale, FluentType.MinTextScale, FluentType.MaxTextScale, 0.05, Percent,
                v => host.Mutate(s => s.Design.FontScale = v)),
        ]);
    }

    /// <summary>[S-826] The four font presets, under their names in the UI's language, before the installed families.</summary>
    public static IReadOnlyList<FontOption> FontPresets() =>
    [
        new(DesignLook.SystemFont, Str.Shell_Appearance_SystemFont.Text()),
        new(DesignLook.SansFont, Str.Shell_Appearance_SansFont.Text()),
        new(DesignLook.SerifFont, Str.Settings_Font_Serif.Text()),
        new(DesignLook.MonoFont, Str.Settings_Font_Mono.Text()),
    ];

    /// <summary>[S-6441] The name of the font <paramref name="current"/> picks: a preset's name, a family's own, the system font's for none.</summary>
    public static string FontName(string? current)
    {
        var options = FontSearch.Options(FontPresets(), InstalledFonts.None, current);
        var chosen = FontSearch.Chosen(options, current);
        return options.First(o => o.Value == chosen).Label;
    }

    /// <summary>
    /// [S-3718] A choice of one over an enum: the dot on the value that holds, a pick of another writes
    /// it in one change, the one that holds changes nothing.
    /// </summary>
    private static SettingsChoice Choice<T>(ISettingsMenuHost host, string key, string text, string? glyph, IReadOnlyList<(T Value, string Text)> options, T current, Action<AppState, T> set)
        where T : struct, Enum
    {
        var radios = new List<SettingsRadio>(options.Count);
        foreach (var (value, label) in options)
        {
            var holds = EqualityComparer<T>.Default.Equals(value, current);
            radios.Add(new SettingsRadio(key + "/" + value, label, holds, () =>
            {
                if (!holds)
                {
                    host.Mutate(s => set(s, value));
                }
            }));
        }

        return new SettingsChoice(key, text, glyph, radios);
    }
}
