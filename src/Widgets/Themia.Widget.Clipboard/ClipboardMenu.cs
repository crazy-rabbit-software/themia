using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Clipboard;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1450] The Clipboard widget's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the timestamps switch, how many entries are kept, and the privacy sentence.
/// <para>
/// The most important line is a sentence rather than a control: the history is kept in memory and
/// never written to disk. There is deliberately no switch to change that. A clipboard history
/// captures whatever was copied (a password, a recovery code), and a "save history" switch, however
/// it is defaulted, is an offer to create a credentials file. Clearing stays on the widget, behind
/// the one confirmation ([S-1444]): two ways to destroy the same thing is one too many.
/// </para>
/// </summary>
internal static class ClipboardMenu
{
    public static void Lines(WidgetMenuLines menu, ClipboardConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(menu.Toggle(Glyphs.Clock, ClipboardStr.WidgetsA_Clipboard_Settings_Timestamp_Label.Text(), config.ShowTimestamp, on => config.ShowTimestamp = on));

        // [S-1440] The one setting that changes exposure: fewer entries kept is less in memory.
        menu.Lines.Add(menu.Number(Glyphs.Count, ClipboardStr.WidgetsA_Clipboard_Settings_Entries_Label.Text(),
            config.EffectiveCapacity, ClipboardHistory.MinCapacity, ClipboardHistory.MaxCapacity, 1,
            value => value.ToString(System.Globalization.CultureInfo.InvariantCulture), value => config.Capacity = value));

        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(WidgetMenuLines.Note(ClipboardStr.WidgetsA_Clipboard_Settings_PrivacyNote.Text()));
    }
}
