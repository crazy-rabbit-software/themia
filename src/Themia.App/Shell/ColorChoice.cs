using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>What one <see cref="ColorChoice"/> offers.</summary>
internal sealed class ColorChoiceOptions
{
    /// <summary>The swatches, in rows of <see cref="ColorPresets.Columns"/>. Windows' accent palette by default.</summary>
    public IReadOnlyList<ColorPreset> Presets { get; init; } = ColorPresets.Windows;

    /// <summary>The heading over the swatches.</summary>
    public string PresetsTitle { get; init; } = Str.Shell_Color_WindowsColours.Text();

    /// <summary>
    /// The label of the "use the default" choice, e.g. "Windows accent colour" — or null when the
    /// setting must hold a colour (a palette entry, the pen's ink). Choosing it stores an empty
    /// value, which every colour setting reads as "follow the theme".
    /// </summary>
    public string? DefaultLabel { get; init; }

    /// <summary>
    /// What the default currently looks like, to paint its swatch (the Windows accent, the calendar's
    /// palette colour). Null when the default is the theme's own surface, which has no one colour.
    /// </summary>
    public Func<Windows.UI.Color?>? DefaultColor { get; init; }

    /// <summary>Whether "Custom colour" opens the full picker. On everywhere unless a setting is a fixed set.</summary>
    public bool AllowCustom { get; init; } = true;
}

/// <summary>[S-6567] What one fill control (<see cref="ColorChoice.CreateFill"/>) offers.</summary>
internal sealed class FillChoiceOptions
{
    /// <summary>The label of Default: "Default" on a widget (the app-wide fill), "Theme colour" app-wide.</summary>
    public required string DefaultLabel { get; init; }

    /// <summary>What Default currently looks like, to paint its swatch; null for the theme's own fill.</summary>
    public Func<Windows.UI.Color?>? DefaultColor { get; init; }

    /// <summary>The swatches.</summary>
    public IReadOnlyList<ColorPreset> Presets { get; init; } = ColorPresets.Surfaces;

    /// <summary>What the button reads while a mixed selection has no one value (a null value); null reads Default.</summary>
    public string? MixedLabel { get; init; }
}

/// <summary>The fill entries a fill control's panel adds to the colour panel ([S-6567]).</summary>
/// <param name="DefaultChosen">Default is the current choice (the colour panel would read that off an empty colour).</param>
/// <param name="TransparentChosen">Transparent is the current choice.</param>
/// <param name="Transparent">Picks Transparent.</param>
/// <param name="Opacity">The colour's opacity, 0..1.</param>
/// <param name="OpacityEnabled">A colour is chosen, so the opacity slider means something.</param>
/// <param name="OpacityPreview">The slider moved: show it, store nothing yet.</param>
/// <param name="OpacityCommit">The slider was let go (or stepped with a key): store it, one change.</param>
internal sealed record FillExtras(
    bool DefaultChosen,
    bool TransparentChosen,
    Action Transparent,
    double Opacity,
    bool OpacityEnabled,
    Action<double> OpacityPreview,
    Action<double> OpacityCommit);

/// <summary>
/// The one colour control in the settings: a swatch button that opens a Windows 11-style picker —
/// a "use the default" choice where empty means "follow the theme", the preset swatches as Settings
/// › Personalization › Colours lays them out, and "Custom colour" for WinUI's full
/// <see cref="ColorPicker"/>. [S-1549], [S-1550]
/// <para>
/// Colours are never typed. A hex box is a field most people cannot fill in and the rest mistype;
/// a picker can only ever produce a colour. What is stored stays <c>#RRGGBB</c> (or empty for the
/// default), so nothing that reads a colour setting changes.
/// </para>
/// <para>
/// Use <see cref="FluentLayout.ColorPickerRow"/> for a settings row, or <see cref="Create"/> where
/// the colour sits inside another control (a palette entry, a calendar row).
/// </para>
/// </summary>
internal static class ColorChoice
{
    private const string Scope = "color-choice";

    private const double SwatchSize = 32;

    /// <summary>
    /// The swatch button. <paramref name="onChanged"/> receives <c>#RRGGBB</c>, or an empty string for
    /// the default.
    /// </summary>
    /// <param name="name">What a screen reader calls the button: the setting's title.</param>
    public static DropDownButton Create(string? value, Action<string> onChanged, ColorChoiceOptions? options = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(onChanged);
        options ??= new ColorChoiceOptions();

        var current = HexColor.Normalise(value);

        // A setting that must hold a colour but holds none (blank, corrupt) shows its first preset
        // rather than a "default" it does not have.
        if (current.Length == 0 && options.DefaultLabel is null && options.Presets.Count > 0)
        {
            current = HexColor.Normalise(options.Presets[0].Hex);
        }

        var button = SwatchButton(name, out var swatch, out var label, out var flyout);

        void Show()
        {
            PaintSwatch(swatch, current, options);
            label.Text = Describe(current, options);
            ToolTipService.SetToolTip(button, label.Text);
        }

        void Choose(string hex)
        {
            flyout.Hide();
            if (hex == current)
            {
                return;
            }

            current = hex;
            Show();

            try
            {
                onChanged(hex);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "saving the colour failed", ex);
            }
        }

        // Built each time it opens, so the ring sits on whatever is chosen now.
        flyout.Opening += (_, _) => flyout.Content = Panel(
            current,
            options,
            Choose,
            () =>
            {
                flyout.Hide();
                _ = CustomAsync(button, current, options, null, (hex, _) => Choose(hex));
            },
            () => PickFromScreen(flyout, Choose));

        Show();

#if DEBUG
        // The settings render draws the picker's panel in the page: a flyout on an off-screen,
        // never-activated window does not render.
        DebugPanels.AddOrUpdate(button, new Func<FrameworkElement>(() => Panel(current, options, _ => { }, () => { }, () => { })));
#endif

        return button;
    }

    /// <summary>
    /// [S-6567] The fill control: the same swatch button and panel as <see cref="Create"/>, with
    /// Default, <b>Transparent</b>, the swatches, the custom colour (its dialog with an alpha slider)
    /// and an <b>Opacity</b> slider under them, so a colour and how see-through it is are one choice.
    /// <paramref name="onChanged"/> receives a copy of the new choice. Picking a colour keeps the
    /// opacity last set (100 % at first); the slider stores once it is let go, not on every step.
    /// </summary>
    /// <param name="name">What a screen reader calls the button: the setting's title.</param>
    public static DropDownButton CreateFill(FillChoice? value, Action<FillChoice> onChanged, FillChoiceOptions options, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(onChanged);
        ArgumentNullException.ThrowIfNull(options);

        var current = value?.Clone() ?? new FillChoice();

        // ignore: what Repair reports was logged when the state loaded; here it only keeps the control total.
        _ = current.Repair("fill choice");
        var colourOptions = new ColorChoiceOptions { DefaultLabel = options.DefaultLabel, DefaultColor = options.DefaultColor, Presets = options.Presets };

        var button = SwatchButton(name, out var swatch, out var label, out var flyout);
        var pending = current.Opacity;

        // [S-6566] A selection that does not share one fill reads as mixed until something is picked.
        var mixed = value is null && options.MixedLabel is not null;

        void Show(FillChoice shown)
        {
            PaintFill(swatch, shown, colourOptions);
            label.Text = DescribeFill(shown, colourOptions);
            ToolTipService.SetToolTip(button, label.Text);
        }

        void Commit(FillChoice next)
        {
            pending = next.Opacity;
            if (next.SameAs(current) && !mixed)
            {
                Show(current);
                return;
            }

            current = next;
            mixed = false;
            Show(current);

            try
            {
                onChanged(current.Clone());
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "saving the fill failed", ex);
            }
        }

        FillChoice With(FillKind kind) => new() { Kind = kind, Colour = current.Colour, Opacity = current.Opacity };

        void Choose(string hex)
        {
            flyout.Hide();
            Commit(hex.Length == 0 ? With(FillKind.Default) : FillChoice.Of(hex, current.Opacity));
        }

        void CommitOpacity()
        {
            if (current.Kind == FillKind.Colour && Math.Abs(pending - current.Opacity) >= 0.001)
            {
                Commit(FillChoice.Of(current.Colour, pending));
            }
        }

        FillExtras Extras() => new(
            !mixed && current.Kind == FillKind.Default,
            !mixed && current.Kind == FillKind.Transparent,
            () =>
            {
                flyout.Hide();
                Commit(With(FillKind.Transparent));
            },
            current.Opacity,
            !mixed && current.Kind == FillKind.Colour,
            opacity =>
            {
                pending = opacity;
                Show(FillChoice.Of(current.Colour, opacity));
            },
            opacity =>
            {
                pending = opacity;
                CommitOpacity();
            });

        string Hex() => !mixed && current.Kind == FillKind.Colour ? current.Colour : string.Empty;

        flyout.Opening += (_, _) => flyout.Content = Panel(
            Hex(),
            colourOptions,
            Choose,
            () =>
            {
                flyout.Hide();
                _ = CustomAsync(button, Hex(), colourOptions, current.Opacity, (hex, alpha) => Commit(FillChoice.Of(hex, alpha)));
            },
            () => PickFromScreen(flyout, Choose),
            Extras());

        // A drag let go outside the slider still ends with the flyout.
        flyout.Closed += (_, _) => CommitOpacity();

        if (mixed)
        {
            swatch.Background = null;
            label.Text = options.MixedLabel;
            ToolTipService.SetToolTip(button, label.Text);
        }
        else
        {
            Show(current);
        }

#if DEBUG
        DebugPanels.AddOrUpdate(button, new Func<FrameworkElement>(() => Panel(Hex(), colourOptions, _ => { }, () => { }, () => { }, Extras())));
#endif

        return button;
    }

    /// <summary>The swatch button both controls are: a swatch, the value's words, and a flyout kept inside the window.</summary>
    private static DropDownButton SwatchButton(string? name, out Border swatch, out TextBlock label, out Flyout flyout)
    {
        swatch = new Border { Width = 32, Height = 20, VerticalAlignment = VerticalAlignment.Center };
        FluentLayout.ApplyStyle(swatch, "SettingsSwatchBorderStyle");

        label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 150,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var button = new DropDownButton
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space3,
                Children = { swatch, label },
            },
            MinWidth = FluentLayout.ChoiceMinWidth,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };

        if (!string.IsNullOrEmpty(name))
        {
            AutomationProperties.SetName(button, name);
        }

        // Kept inside the settings window, like Settings' own pickers, rather than as a separate
        // top-level popup window.
        flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight, ShouldConstrainToRootBounds = true };
        button.Flyout = flyout;
        return button;
    }

    /// <summary>[S-6567] A fill in words: Default's label, "Transparent", or the colour's name with its opacity under 100 %.</summary>
    internal static string DescribeFill(FillChoice fill, ColorChoiceOptions options)
    {
        switch (fill.Kind)
        {
            case FillKind.Transparent:
                return Str.Shell_Fill_Transparent.Text();
            case FillKind.Colour:
                var name = Describe(HexColor.Normalise(fill.Colour), options);
                return fill.Opacity >= 0.995 ? name
                    : Str.Shell_Fill_WithOpacity.Format(("colour", name), ("percent", LocaleFormat.Percent(Strings.Current, Math.Round(fill.Opacity * 100))));
            default:
                return options.DefaultLabel ?? Str.Common_Default.Text();
        }
    }

    /// <summary>[S-6567] A fill's swatch: the colour at its opacity, nothing for Transparent, Default as <see cref="PaintSwatch"/> paints it.</summary>
    private static void PaintFill(Border swatch, FillChoice fill, ColorChoiceOptions options)
    {
        if (fill.Kind == FillKind.Transparent)
        {
            swatch.Background = null;
            return;
        }

        if (fill.Kind == FillKind.Colour && HexColor.TryParse(fill.Colour, out var r, out var g, out var b))
        {
            swatch.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(DesignLook.Alpha(fill.Opacity), r, g, b));
            return;
        }

        PaintSwatch(swatch, string.Empty, options);
    }

#if DEBUG
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DropDownButton, Func<FrameworkElement>> DebugPanels = [];

    /// <summary>Debug renders only: the panel this button's flyout would show.</summary>
    internal static FrameworkElement? DebugPanel(DropDownButton button)
        => DebugPanels.TryGetValue(button, out var build) ? build() : null;
#endif

    /// <summary>"Default blue", "Custom", or the default's own label.</summary>
    internal static string Describe(string hex, ColorChoiceOptions options)
    {
        if (hex.Length == 0)
        {
            return options.DefaultLabel ?? Str.Common_None.Text();
        }

        return ColorPresets.Find(options.Presets, hex)?.Name ?? Str.Settings_Style_Preset_Custom.Text();
    }

    private static void PaintSwatch(Border swatch, string hex, ColorChoiceOptions options)
    {
        if (HexColor.TryParse(hex, out var r, out var g, out var b))
        {
            swatch.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
            return;
        }

        swatch.Background = options.DefaultColor?.Invoke() is { } fallback
            ? new SolidColorBrush(fallback)
            : FluentLayout.Themed("SolidBackgroundFillColorTertiaryBrush", Colors.Gray);
    }

    /// <summary>The flyout: the default, the swatches, and the ways to a custom colour (the full picker, the eyedropper).</summary>
    /// <param name="fill">[S-6567] The fill control's entries: Transparent and the opacity slider. Null for a plain colour.</param>
    private static StackPanel Panel(string current, ColorChoiceOptions options, Action<string> choose, Action custom, Action pickFromScreen, FillExtras? fill = null)
    {
        var panel = new StackPanel { Spacing = Fluent.Space3, Width = (SwatchSize * ColorPresets.Columns) + (Fluent.Space1 * (ColorPresets.Columns - 1)) };

        if (options.DefaultLabel is { } defaultLabel)
        {
            var preview = new Border { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            FluentLayout.ApplyStyle(preview, "SettingsSwatchBorderStyle");
            PaintSwatch(preview, string.Empty, options);

            var check = new FontIcon
            {
                Glyph = Glyphs.Done,
                FontSize = 12,
                Visibility = (fill?.DefaultChosen ?? current.Length == 0) ? Visibility.Visible : Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var text = new TextBlock { Text = defaultLabel, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            Grid.SetColumn(check, 2);

            var useDefault = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = new Grid
                {
                    ColumnSpacing = Fluent.Space3,
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto },
                    },
                    Children = { preview, text, check },
                },
            };

            AutomationProperties.SetName(useDefault, defaultLabel);
            useDefault.Click += (_, _) => choose(string.Empty);
            panel.Children.Add(useDefault);
        }

        if (fill is not null)
        {
            panel.Children.Add(TransparentEntry(fill));
        }

        if (options.Presets.Count > 0)
        {
            panel.Children.Add(Caption(options.PresetsTitle));
            panel.Children.Add(Swatches(current, options.Presets, choose));
        }

        if (options.AllowCustom)
        {
            var row = new Grid
            {
                ColumnSpacing = Fluent.Space3,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
            };

            // A custom colour in use is shown, ringed, beside the ways to change it.
            var isCustom = current.Length > 0 && ColorPresets.Find(options.Presets, current) is null;
            var mine = new Border { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center, Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed };
            FluentLayout.ApplyStyle(mine, "SettingsSwatchBorderStyle");
            PaintSwatch(mine, current, options);

            var title = new TextBlock { Text = Str.Shell_Color_Custom.Text(), VerticalAlignment = VerticalAlignment.Center };
            var view = new Button { Content = Str.Shell_Color_ViewColours.Text() };
            view.Click += (_, _) => custom();

            // [S-1556] The eyedropper: any pixel on any display becomes the custom colour.
            var dropper = new Button
            {
                Content = new FontIcon { Glyph = Glyphs.Eyedropper, FontSize = Fluent.IconSize },
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            AutomationProperties.SetName(dropper, Str.Shell_Color_PickFromScreen.Text());
            ToolTipService.SetToolTip(dropper, Str.Shell_Color_PickFromScreenTip.Text());
            dropper.Click += (_, _) => pickFromScreen();

            Grid.SetColumn(title, 1);
            Grid.SetColumn(dropper, 2);
            Grid.SetColumn(view, 3);
            row.Children.Add(mine);
            row.Children.Add(title);
            row.Children.Add(dropper);
            row.Children.Add(view);
            panel.Children.Add(row);
        }

        if (fill is not null)
        {
            panel.Children.Add(OpacityEntry(fill));
        }

        return panel;
    }

    /// <summary>[S-6567] "Transparent", laid out as the default entry: an empty swatch, the word, a check when chosen.</summary>
    private static Button TransparentEntry(FillExtras fill)
    {
        var preview = new Border { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center };
        FluentLayout.ApplyStyle(preview, "SettingsSwatchBorderStyle");
        preview.Background = null;

        var check = new FontIcon
        {
            Glyph = Glyphs.Done,
            FontSize = 12,
            Visibility = fill.TransparentChosen ? Visibility.Visible : Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var text = new TextBlock { Text = Str.Shell_Fill_Transparent.Text(), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);
        Grid.SetColumn(check, 2);

        var entry = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new Grid
            {
                ColumnSpacing = Fluent.Space3,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children = { preview, text, check },
            },
        };

        AutomationProperties.SetName(entry, Str.Shell_Fill_Transparent.Text());
        entry.Click += (_, _) => fill.Transparent();
        return entry;
    }

    /// <summary>
    /// [S-6567] The colour's opacity, 0..100 % in 5 % steps, under the colours; off until a colour is
    /// chosen. Moving it shows the opacity at once; it is stored when the pointer lets go or a key
    /// steps it, so a drag is one change (one undo step on the panel), not forty.
    /// </summary>
    private static StackPanel OpacityEntry(FillExtras fill)
    {
        static string Readout(double v) => LocaleFormat.Percent(Strings.Current, Math.Round(v));

        var readout = new TextBlock { Text = Readout(fill.Opacity * 100), VerticalAlignment = VerticalAlignment.Center, MinWidth = 44, TextAlignment = TextAlignment.Right };
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            StepFrequency = 5,
            Value = Math.Round(fill.Opacity * 100),
            IsEnabled = fill.OpacityEnabled,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(slider, Str.Shell_Fill_Opacity.Text());

        slider.ValueChanged += (_, args) =>
        {
            readout.Text = Readout(args.NewValue);
            fill.OpacityPreview(args.NewValue / 100);
        };
        slider.PointerCaptureLost += (_, _) => fill.OpacityCommit(slider.Value / 100);
        slider.KeyUp += (_, _) => fill.OpacityCommit(slider.Value / 100);

        Grid.SetColumn(readout, 1);
        return new StackPanel
        {
            Spacing = Fluent.Space1,
            Children =
            {
                Caption(Str.Shell_Fill_Opacity.Text()),
                new Grid
                {
                    ColumnSpacing = Fluent.Space2,
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto },
                    },
                    Children = { slider, readout },
                },
            },
        };
    }

    private static TextBlock Caption(string text)
    {
        var caption = new TextBlock { Text = text };
        FluentLayout.ApplyStyle(caption, "SettingsCaptionTextBlockStyle");
        return caption;
    }

    /// <summary>The swatch grid, rows of eight; the chosen one carries a check in a colour that reads on it.</summary>
    private static Grid Swatches(string current, IReadOnlyList<ColorPreset> presets, Action<string> choose)
    {
        var grid = new Grid { RowSpacing = Fluent.Space1, ColumnSpacing = Fluent.Space1 };

        for (var c = 0; c < ColorPresets.Columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SwatchSize) });
        }

        for (var r = 0; r < (presets.Count + ColorPresets.Columns - 1) / ColorPresets.Columns; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SwatchSize) });
        }

        for (var i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var hex = HexColor.Normalise(preset.Hex);
            HexColor.TryParse(hex, out var r, out var g, out var b);

            var chosen = hex == current;
            var fill = new Border
            {
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b)),
                Child = chosen
                    ? new FontIcon
                    {
                        Glyph = Glyphs.Done,
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,

                        // The check is drawn ON the colour, so its colour is the colour's contrast,
                        // not a theme brush.
                        Foreground = new SolidColorBrush(IsLight(r, g, b) ? Colors.Black : Colors.White),
                    }
                    : null,
            };

            var swatch = new Button
            {
                Width = SwatchSize,
                Height = SwatchSize,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = fill,
            };

            if (chosen)
            {
                swatch.BorderBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
                swatch.BorderThickness = new Thickness(2);
            }

            ToolTipService.SetToolTip(swatch, preset.Name);
            AutomationProperties.SetName(swatch, preset.Name);
            swatch.Click += (_, _) => choose(hex);

            Grid.SetRow(swatch, i / ColorPresets.Columns);
            Grid.SetColumn(swatch, i % ColorPresets.Columns);
            grid.Children.Add(swatch);
        }

        return grid;
    }

    private static bool IsLight(byte r, byte g, byte b) => ScreenPick.WantsDarkInk(r, g, b);

    /// <summary>The eyedropper's loupe, sized from the design tokens: 11 magnified pixels a side.</summary>
    private static LoupeMetrics Loupe => new(
        Cells: 11,
        CellDips: Fluent.Space3,
        OffsetDips: Fluent.Space5,
        GapDips: Fluent.Space1,
        LabelHeightDips: FluentType.BodyStrong.LineHeight + (2 * Fluent.Space1));

    /// <summary>
    /// [S-1556] Close the flyout, wait until the screen no longer shows it, then run the eyedropper.
    /// Event-driven throughout: the flyout's Closed, the next XAML frame, then the compositor's.
    /// </summary>
    private static void PickFromScreen(Flyout flyout, Action<string> choose)
    {
        if (ScreenColorPicker.IsActive)
        {
            return;
        }

        void OnClosed(object? sender, object e)
        {
            flyout.Closed -= OnClosed;
            AfterNextFrame(() => _ = PickFromScreenAsync(choose));
        }

        flyout.Closed += OnClosed;
        flyout.Hide();
    }

    /// <summary>Run <paramref name="then"/> once the next XAML frame has been handed to the compositor.</summary>
    private static void AfterNextFrame(Action then)
    {
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        void OnRendering(object? sender, object e)
        {
            CompositionTarget.Rendering -= OnRendering;

            // Rendering runs before the frame is drawn; a low-priority item runs after it is sent.
            if (queue is null || !queue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => then()))
            {
                Log.Warn(Scope, "could not queue the eyedropper after the frame; starting it now");
                then();
            }
        }

        CompositionTarget.Rendering += OnRendering;
    }

    private static async Task PickFromScreenAsync(Action<string> choose)
    {
        try
        {
            ScreenColorPicker.WaitForComposedFrame();
            var result = await ScreenColorPicker.PickAsync(Loupe, FluentType.BodyStrong.FontSize);
            switch (result.Outcome)
            {
                case ScreenPickOutcome.Picked:
                    // Normalised again: the result crossed from native memory, and Choose stores it as is.
                    var hex = HexColor.Normalise(result.Hex);
                    if (hex.Length == 0)
                    {
                        Log.Error(Scope, $"the eyedropper returned an unreadable colour '{result.Hex}'");
                        Notices.Show(Notice.Error(Str.Shell_Color_PickFailed.Text()));
                        return;
                    }

                    choose(hex);
                    return;

                case ScreenPickOutcome.Failed:
                    Notices.Show(Notice.Error(Str.Shell_Color_PickFailed.Text()));
                    return;

                default:
                    // Escape, a right click, focus elsewhere: the user changed their mind.
                    return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the eyedropper failed", ex);
            Notices.Show(Notice.Error(Str.Shell_Color_PickFailed.Text()));
        }
    }

    /// <summary>
    /// "Custom colour": WinUI's colour picker in a dialog, as Settings opens one for a custom accent.
    /// Text entry is off — the spectrum and the slider only — because a typed value is exactly what
    /// this control exists to avoid. [S-6567] With an <paramref name="opacity"/> (a fill) it has the
    /// alpha slider too, starting there; <paramref name="choose"/> gets the colour and the alpha (1
    /// for a plain colour).
    /// </summary>
    private static async Task CustomAsync(FrameworkElement source, string current, ColorChoiceOptions options, double? opacity, Action<string, double> choose)
    {
        if (source.XamlRoot is not { } root)
        {
            Log.Warn(Scope, "no XamlRoot for the custom colour dialog");
            return;
        }

        Windows.UI.Color start;
        if (HexColor.TryParse(current, out var r, out var g, out var b))
        {
            start = Windows.UI.Color.FromArgb(255, r, g, b);
        }
        else
        {
            start = options.DefaultColor?.Invoke() ?? Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4);
        }

        if (opacity is { } alpha)
        {
            start.A = DesignLook.Alpha(alpha);
        }

        var picker = new ColorPicker
        {
            Color = start,
            IsAlphaEnabled = opacity is not null,
            IsAlphaTextInputVisible = false,
            IsHexInputVisible = false,
            IsColorChannelTextInputVisible = false,
            IsMoreButtonVisible = false,
            IsColorSliderVisible = true,
            IsColorPreviewVisible = true,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            RequestedTheme = source.ActualTheme,
            Title = Str.Shell_Color_PickCustom.Text(),
            Content = picker,
            PrimaryButtonText = Str.Common_Done.Text(),
            CloseButtonText = Str.Common_Cancel.Text(),
            DefaultButton = ContentDialogButton.Primary,
        };

        try
        {
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            // ShowAsync throws when another dialog already owns the window. Nothing changes.
            Log.Error(Scope, "could not show the colour picker", ex);
            return;
        }

        var color = picker.Color;
        choose(HexColor.From(color.R, color.G, color.B), opacity is null ? 1 : color.A / 255.0);
    }

    /// <summary>The Windows accent colour, for an accent setting's "default" swatch.</summary>
    public static Windows.UI.Color? WindowsAccent()
    {
        try
        {
            return new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read the Windows accent colour", ex);
            return null;
        }
    }
}
