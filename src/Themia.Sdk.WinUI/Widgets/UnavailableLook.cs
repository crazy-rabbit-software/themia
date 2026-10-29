using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1940] The one look a widget draws its content in while its subject is not on this machine (no
/// battery, no display brightness, no output device, no adapter): its own parts at their own places,
/// in the family's disabled role ([S-6706]), its controls disabled, the reason on hover and for a screen reader.
/// No widget builds an empty state for that, and none picks its own grey. The figures the parts stand
/// at are the SDK's <see cref="Themia.Core.Widgets.Unavailable"/>.
/// </summary>
public static class UnavailableLook
{
    /// <summary>Text and glyphs: the disabled role.</summary>
    public static Brush Text { get; } = Family.Brush(ColourRole.TextDisabled);

    /// <summary>A level's fill (a chart's bars, a baseline): the disabled role too, so a part missing on this PC is one grey.</summary>
    public static Brush Fill { get; } = Family.Brush(ColourRole.TextDisabled);

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
