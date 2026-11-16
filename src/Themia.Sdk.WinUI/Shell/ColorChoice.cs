using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell;

/// <summary>What one <see cref="ColorChoice"/> offers.</summary>
public sealed class ColorChoiceOptions
{
    /// <summary>The swatches, in rows of <see cref="ColorPresets.Columns"/>. Windows' accent palette by default.</summary>
    public IReadOnlyList<ColorPreset> Presets { get; init; } = ColorPresets.Windows;

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

/// <summary>[S-3724] What a fill line of a menu offers (<see cref="Themia.Sdk.WinUI.MenuColour.OfFill"/>).</summary>
public sealed class FillChoiceOptions
{
    /// <summary>The label of Default: "Default" on a widget (the app-wide fill), "Theme colour" app-wide.</summary>
    public required string DefaultLabel { get; init; }

    /// <summary>What Default currently looks like, to paint its swatch; null for the theme's own fill.</summary>
    public Func<Windows.UI.Color?>? DefaultColor { get; init; }

    /// <summary>The swatches.</summary>
    public IReadOnlyList<ColorPreset> Presets { get; init; } = ColorPresets.Surfaces;
}

/// <summary>
/// [S-3724] What the menus' one colour picker needs that is not a line: a colour and a fill in words
/// and on a swatch, the recent colours ([S-1575]) and the eyedropper. [S-1549], [S-1550]
/// <para>
/// [S-1577] A colour is picked from the swatches, mixed, taken from the screen or typed in the hex box
/// as Karpo's picker has it; what is stored stays <c>#RRGGBB</c> (or empty for the default).
/// </para>
/// </summary>
public static class ColorChoice
{
    /// <summary>The host's eyedropper ([S-1556]): the screen capture and its overlay are Win32, which the SDK does not carry.</summary>
    private static IScreenEyedropper? _eyedropper;

    private static IScreenEyedropper Eyedropper
        => Volatile.Read(ref _eyedropper) ?? throw new InvalidOperationException("no eyedropper installed: the host installs it before any colour row is shown");

    /// <summary>The host's eyedropper. Once per process.</summary>
    internal static void InstallEyedropper(IScreenEyedropper eyedropper)
    {
        ArgumentNullException.ThrowIfNull(eyedropper);
        if (Interlocked.CompareExchange(ref _eyedropper, eyedropper, null) is not null)
        {
            throw new InvalidOperationException("the eyedropper is installed once per process");
        }
    }
    private const string Scope = "color-choice";

    /// <summary>[S-1575] The host's recent colours: read, and a kept colour remembered.</summary>
    private static (Func<IReadOnlyList<string>> Read, Action<string> Remember)? _recent;

    /// <summary>[S-1575] The host's store of the recent colours. Once per process.</summary>
    internal static void InstallRecent(Func<IReadOnlyList<string>> read, Action<string> remember)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(remember);
        if (_recent is not null)
        {
            throw new InvalidOperationException("the recent colours are installed once per process");
        }

        _recent = (read, remember);
    }

    /// <summary>[S-1575] The recent colours, newest first; none where the host keeps none (a render).</summary>
    internal static IReadOnlyList<string> Recent => _recent is { } recent ? ColourPicker.Recent(recent.Read()) : [];

    /// <summary>[S-1575] A colour kept from the mixer, the hex box or the eyedropper: first of the recent colours.</summary>
    internal static void Remember(string hex)
    {
        if (_recent is { } recent && ColourPicker.Typed(hex) is { } colour)
        {
            recent.Remember(colour);
        }
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
    internal static void PaintFill(Border swatch, FillChoice fill, ColorChoiceOptions options)
    {
        if (fill.Kind == FillKind.Transparent)
        {
            swatch.Background = null;
            return;
        }

        if (fill.Kind == FillKind.Colour && HexColor.TryParse(fill.Colour, out var r, out var g, out var b))
        {
            swatch.Background = new SolidColorBrush(HexBrush.Colour(new RgbColor(r, g, b), FillChoice.Alpha(fill.Opacity) / 255.0));
            return;
        }

        PaintSwatch(swatch, string.Empty, options);
    }

    /// <summary>"Default blue", "Custom", or the default's own label.</summary>
    internal static string Describe(string hex, ColorChoiceOptions options)
    {
        if (hex.Length == 0)
        {
            return options.DefaultLabel ?? Str.Common_None.Text();
        }

        return ColorPresets.Find(options.Presets, hex)?.Name ?? Str.Settings_Style_Preset_Custom.Text();
    }

    internal static void PaintSwatch(Border swatch, string hex, ColorChoiceOptions options)
    {
        if (HexColor.TryParse(hex, out var r, out var g, out var b))
        {
            swatch.Background = new SolidColorBrush(HexBrush.Colour(new RgbColor(r, g, b)));
            return;
        }

        swatch.Background = options.DefaultColor?.Invoke() is { } fallback
            ? new SolidColorBrush(fallback)
            : ShellBrush.Of(ShellRole.Raised);
    }

    /// <summary>The eyedropper's loupe, sized from the design tokens: 11 magnified pixels a side.</summary>
    private static LoupeMetrics Loupe => new(
        Cells: 11,
        CellDips: Fluent.Space3,
        OffsetDips: FamilySpace.S6,
        GapDips: Fluent.Space1,
        LabelHeightDips: FamilyType.BodyStrong.LineHeight + (2 * Fluent.Space1));

    /// <summary>[S-1556] [S-3723] The eyedropper from a line of the properties menu, which stays where it is.</summary>
    internal static void PickFromScreen(Action<string> choose)
    {
        ArgumentNullException.ThrowIfNull(choose);
        if (!Eyedropper.IsActive)
        {
            AfterNextFrame(() => _ = PickFromScreenAsync(choose));
        }
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
            Eyedropper.WaitForComposedFrame();
            var result = await Eyedropper.PickAsync(Loupe, FamilyType.BodyStrong.FontSize);
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

/// <summary>[S-1556]..[S-1559] What the host's eyedropper does for a colour row: pick any pixel on any display.</summary>
public interface IScreenEyedropper
{
    /// <summary>A pick is under way (a second one is not started).</summary>
    bool IsActive { get; }

    /// <summary>Wait for the desktop compositor's next frame, so the capture shows the screen and not the colour flyout.</summary>
    void WaitForComposedFrame();

    /// <summary>Run the pick: the loupe drawn with <paramref name="metrics"/>, its label at <paramref name="labelFontDips"/>.</summary>
    Task<ScreenPickResult> PickAsync(LoupeMetrics metrics, double labelFontDips);
}
