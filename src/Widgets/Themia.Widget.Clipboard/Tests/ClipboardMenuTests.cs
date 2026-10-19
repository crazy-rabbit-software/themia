using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Clipboard.Tests;

/// <summary>[S-1450] The Clipboard's options on its properties menu: timestamps, entries typed, the privacy note.</summary>
public sealed class ClipboardMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(ClipboardConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        ClipboardMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S1450_appearance_then_timestamps_apart_from_entries_and_the_privacy_note_last()
    {
        var lines = Build(new ClipboardConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(ClipboardStr.WidgetsA_Clipboard_Settings_Timestamp_Label.Text(), lines.Lines[0].Text);
        Assert.True(lines.Lines[1].IsSeparator);
        Assert.Equal(ClipboardStr.WidgetsA_Clipboard_Settings_Entries_Label.Text(), lines.Lines[2].Text);
        Assert.Equal("50", lines.Lines[2].Value);
        Assert.Equal(4, lines.Lines.Count);
        var note = lines.Lines[^1];
        Assert.True(note.Note);
        Assert.Equal(ClipboardStr.WidgetsA_Clipboard_Settings_PrivacyNote.Text(), note.Text);
        Assert.Null(note.Action);
    }

    [Fact]
    public void S1450_the_menu_offers_nothing_that_clears_or_keeps_the_history()
        => Assert.DoesNotContain(Build(new ClipboardConfig()).Lines, l => l.Danger);

    [Fact]
    public void S1450_entries_are_typed_inside_1_to_200()
    {
        var config = new ClipboardConfig();
        Build(config).Lines.Single(l => l.Text == ClipboardStr.WidgetsA_Clipboard_Settings_Entries_Label.Text()).Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);

        Assert.NotNull(ask.Refuse("0"));
        Assert.NotNull(ask.Refuse("201"));
        Assert.NotNull(ask.Refuse("lots"));

        ask.Done("12");
        Assert.Equal(12, config.Capacity);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1450_the_timestamp_switch_turns_them_off_and_saves_once()
    {
        var config = new ClipboardConfig { ShowTimestamp = true };
        Build(config).Lines[0].Action!();

        Assert.False(config.ShowTimestamp);
        Assert.Equal(1, _saves);
    }
}
