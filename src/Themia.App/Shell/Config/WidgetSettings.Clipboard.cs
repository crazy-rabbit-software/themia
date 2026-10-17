using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// The Clipboard widget's settings rows.
/// <para>
/// The most important thing on this page is a sentence rather than a control: it says, in plain
/// words, that the history is kept in memory and never written to disk. There is deliberately no
/// switch to change that. A clipboard history captures whatever was copied — which regularly
/// includes a password or a recovery code — and a "save history" toggle, however it is defaulted,
/// is an offer to create a credentials file.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    public static void Clipboard(List<FrameworkElement> rows, ClipboardConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsA_Catalog_Clipboard_Label.Text()));

        rows.Add(FluentLayout.Note(
            Str.WidgetsA_Clipboard_Settings_PrivacyNote.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Clock, Str.WidgetsA_Clipboard_Settings_Timestamp_Label.Text(),
            null,
            FluentLayout.Toggle(config.ShowTimestamp, value => { config.ShowTimestamp = value; save(); })));

        // The one setting here that genuinely changes exposure, so it says so: fewer entries kept
        // is less sitting in memory.
        rows.Add(FluentLayout.Row(Glyphs.Count, Str.WidgetsA_Clipboard_Settings_Entries_Label.Text(),
            Str.WidgetsA_Clipboard_Settings_Entries_Desc.Format(("min", ClipboardHistory.MinCapacity), ("max", ClipboardHistory.MaxCapacity)),
            FluentLayout.Slider(
                config.EffectiveCapacity,
                ClipboardHistory.MinCapacity,
                ClipboardHistory.MaxCapacity,
                1,
                string.Empty,
                value => { config.Capacity = (int)Math.Round(value); save(); })));

        // A signpost rather than a second button. Clearing lives on the widget, behind the same
        // confirmation every destructive action in the app uses, and two ways to destroy the same
        // thing is one too many. It is still marked as a danger row so it lands with the rest of
        // them at the bottom of the page, where somebody looking for it will look.
        rows.Add(FluentLayout.DangerRow(Glyphs.Clear, Str.Clipboard_ClearHistory.Text(),
            Str.WidgetsA_Clipboard_Settings_Clear_Desc.Text(),
            new TextBlock
            {
                Text = Str.WidgetsA_Clipboard_Settings_Clear_Where.Text(),
                FontSize = 12,
                Foreground = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray),
            }));
    }
}
