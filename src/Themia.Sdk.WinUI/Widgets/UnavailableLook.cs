using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1940] The one look a widget draws its content in while its subject is not on this machine (no
/// battery, no display brightness, no output device, no adapter): its own parts at their own places,
/// in Fluent's disabled brushes, its controls disabled, the reason on hover and for a screen reader.
/// No widget builds an empty state for that, and none picks its own grey. The figures the parts stand
/// at are the SDK's <see cref="Themia.Core.Widgets.Unavailable"/>.
/// </summary>
public static class UnavailableLook
{
    /// <summary>Text and glyphs: Fluent's disabled text.</summary>
    public static Brush Text { get; } = ThemeBrush.Of("TextFillColorDisabledBrush", Colors.Gray);

    /// <summary>A level's fill (a battery, a ring, a chart): the disabled accent, as a disabled slider's value.</summary>
    public static Brush Fill { get; } = ThemeBrush.Of("AccentFillColorDisabledBrush", Colors.Gray);

    /// <summary>An outline (a battery's body): the disabled strong stroke, as a disabled control's edge.</summary>
    public static Brush Stroke { get; } = ThemeBrush.Of("ControlStrongStrokeColorDisabledBrush", Colors.Gray);

    /// <summary>
    /// Say why on <paramref name="element"/> (the widget's root): the tooltip and the help text are the
    /// reason while it is unavailable, and cleared again with null.
    /// </summary>
    public static void Say(FrameworkElement element, string? reason)
    {
        ArgumentNullException.ThrowIfNull(element);

        // [S-6332] Said on every reading: touched only when it says something else.
        var tip = string.IsNullOrEmpty(reason) ? null : reason;
        if (!Equals(ToolTipService.GetToolTip(element), tip))
        {
            ToolTipService.SetToolTip(element, tip);
        }

        if (AutomationProperties.GetHelpText(element) != (reason ?? string.Empty))
        {
            AutomationProperties.SetHelpText(element, reason ?? string.Empty);
        }
    }

    /// <summary>
    /// The controls take input only while the subject is there: a disabled control draws its own
    /// disabled look (a slider's grey track and thumb, a button's grey glyph) and takes no click.
    /// </summary>
    public static void Enable(bool enabled, params Control[] controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        foreach (var control in controls)
        {
            control.IsEnabled = enabled;
        }
    }
}
