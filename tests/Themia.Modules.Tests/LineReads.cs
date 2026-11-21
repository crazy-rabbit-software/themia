global using Themia.Core.Settings;
using Themia.App.Shell;
using Themia.Sdk.WinUI;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3719] A widget's line read as its row on the page reads it, whatever its kind: its words, its glyph, what it says
/// at its end, the lines it opens on, whether it is on (a switch) or holds (an option), what a click on it does, and
/// what it carries to ask. A module's test reads its lines the one way through these.
/// </summary>
internal static class LineReads
{
    extension(SettingsLine line)
    {
        /// <summary>Its words; empty for a divider.</summary>
        public string Text => line switch
        {
            SettingsHeading l => l.Text,
            SettingsNote l => l.Text,
            SettingsReading l => l.Text,
            SettingsField l => l.Text,
            SettingsToggle l => l.Text,
            SettingsRadio l => l.Text,
            SettingsChoice l => l.Text,
            SettingsSlider l => l.Text,
            SettingsColour l => l.Text,
            SettingsFill l => l.Text,
            SettingsFont l => l.Text,
            SettingsSubmenu l => l.Text,
            SettingsAsk l => l.Text,
            SettingsIcon l => l.Text,
            SettingsCommand l => l.Text,
            SettingsItem l => l.Text,
            SettingsLink l => l.Text,
            SettingsPrompt l => l.Text,
            SettingsSearch l => l.Text,
            _ => string.Empty,
        };

        /// <summary>Its glyph; null for none.</summary>
        public string? Glyph => line switch
        {
            SettingsToggle l => l.Glyph,
            SettingsRadio l => l.Glyph,
            SettingsChoice l => l.Glyph,
            SettingsSlider l => l.Glyph,
            SettingsSubmenu l => l.Glyph,
            SettingsAsk l => l.Glyph,
            SettingsIcon l => l.Glyph,
            SettingsCommand l => l.Glyph,
            SettingsLink l => l.Glyph,
            SettingsPrompt l => l.Glyph,
            SettingsSearch l => l.Glyph,
            _ => null,
        };

        /// <summary>What it says at its end (a value, a choice's option, a number in its unit, a switch's words).</summary>
        public string? Value => line switch
        {
            SettingsToggle l => l.Value,
            SettingsChoice l => l.Value,
            SettingsSlider l => l.ValueText ?? l.Format(l.Value),
            SettingsSubmenu l => l.Value,
            SettingsAsk l => l.Value,
            SettingsCommand l => l.Value,
            SettingsPrompt l => l.Value,
            SettingsSearch l => l.Value,
            SettingsReading l => l.Value,
            SettingsRadio l => l.Value,
            _ => null,
        };

        /// <summary>The lines it opens on: a submenu's, a choice's options (and what follows them); null for none.</summary>
        public IReadOnlyList<SettingsLine>? Children => line switch
        {
            SettingsSubmenu l => l.Lines,
            SettingsChoice l => [.. l.Options, .. l.More],
            _ => null,
        };

        /// <summary>A switch's state, or whether an option holds; null for any other line.</summary>
        public bool? Checked => line switch
        {
            SettingsToggle l => l.On,
            SettingsRadio l => l.Holds,
            SettingsPrompt { Holds: true } => true,
            _ => null,
        };

        /// <summary>What a click on it does: a switch flipped, an option picked, a command, a link; null for a line that opens or asks.</summary>
        public Action? Action => line switch
        {
            SettingsToggle l => () => l.Set(!l.On),
            SettingsRadio l => l.Pick,
            SettingsCommand { Confirm: null } l => l.Run,
            SettingsLink l => l.Run,
            _ => null,
        };

        /// <summary>A divider.</summary>
        public bool IsSeparator => line is SettingsSeparator;

        /// <summary>Words only.</summary>
        public bool Note => line is SettingsNote;

        /// <summary>It destroys something (drawn in the critical colour).</summary>
        public bool Danger => line switch
        {
            SettingsCommand l => l.Danger,
            SettingsPrompt l => l.Danger,
            _ => false,
        };

        /// <summary>A search's line: null for any other.</summary>
        public SettingsSearch? Search => line as SettingsSearch;

        /// <summary>A number's or a slider's box as the page works it (its snap, its store); null for any other line.</summary>
        public MenuSlider? Slider => line is SettingsSlider l ? new MenuSlider(l.Value, l.Min, l.Max, l.Step, l.Format, l.Set) : null;

        /// <summary>A colour's or a fill's picker as the page works it; null for any other line.</summary>
        public MenuColour? Colour => line switch
        {
            SettingsColour l => MenuColour.Of(l.Current, l.Set, new ColorChoiceOptions { DefaultLabel = l.DefaultText, Presets = l.Presets }),
            SettingsFill l => MenuColour.OfFill(l.Current, l.Set, new FillChoiceOptions { DefaultLabel = l.DefaultText }),
            _ => null,
        };

        /// <summary>A line of text asked in place: what it asks; null for any other.</summary>
        public WidgetTextAsk? Ask => (line as SettingsPrompt)?.Ask;

        /// <summary>An "are you sure?" asked in place: the question; null for any other.</summary>
        public SettingsConfirm? Confirm => (line as SettingsCommand)?.Confirm;

        /// <summary>[S-2351] It asks in place (a line of text or a question).</summary>
        public bool Asks => line is SettingsPrompt or SettingsCommand { Confirm: not null };

        /// <summary>A link row's: where it leads; null for any other.</summary>
        public string? LinkTo => (line as SettingsLink)?.Owner;

        /// <summary>A list item's place in its list; null for any other line.</summary>
        public SettingsOrder? Order => line switch
        {
            SettingsSubmenu l => l.Order,
            SettingsItem l => l.Order,
            _ => null,
        };

        /// <summary>A line of text typed in place in its box: the line; null for any other.</summary>
        public SettingsAsk? Field => line as SettingsAsk;

        /// <summary>An icon's line: null for any other.</summary>
        public SettingsIcon? Icon => line as SettingsIcon;
    }
}
