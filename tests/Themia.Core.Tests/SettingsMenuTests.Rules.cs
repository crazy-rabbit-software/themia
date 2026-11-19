using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2374] Karpo's menu rules (karpo-app <c>MenuRulesTests</c>, <c>MenuConventionTests</c>) on every page of the edit bar:
/// General, Accounts, Display, Views and Appearance, as the state of a world with views and displays
/// shows them (owner, 2026-11-19: "make sure that you put the features of the karpo settings into themia too"). What
/// breaks a rule today and is still to be mended is <see cref="PagesAllowed"/>, one line each; a line kept now fails too,
/// so the list only shrinks.
/// </summary>
public sealed partial class SettingsMenuTests
{
    /// <summary>[S-2374] What still breaks a rule on the pages, by <see cref="RuleOffence.Key"/>: only shrinks.</summary>
    private static readonly HashSet<string> PagesAllowed = new(StringComparer.Ordinal)
    {
        "R13 Appearance › Style", // its icon is also Rendering, Frost saturation's
        "R13 Appearance › Theme", // its icon is also Accent in the card's
        "R13 Appearance › Blur behind widgets", // its icon is also Frost blur's
        "R13 Appearance › Material", // its icon is also Corner radius, Glass, Item surfaces's
        "R13 Appearance › Border width", // its icon is also Rim width, Letter spacing, …'s
        "R13 Appearance › Border opacity", // its icon is also Grain, Lift reach, …'s
        "R13 Appearance › Frost brightness", // its icon is also Rim light's
        "R13 Appearance › Header weight", // its icon is also Number weight, Text weight's
        "R13 Appearance › Figures of one width", // its icon is also Row dividers's
        "R14 Appearance › Glass › Light glass at its clearest", // its title is cut (27 characters)
        "R14 Appearance › Glass › Light glass at its most clouded", // its title is cut (31 characters)
        "R14 Appearance › Glass › Dark glass at its clearest", // its title is cut (26 characters)
        "R14 Appearance › Glass › Dark glass at its most clouded", // its title is cut (30 characters)
        "R14 Appearance › Letter spacing, large text", // its title is cut (26 characters)
        "R14 Appearance › Letter spacing, small text", // its title is cut (26 characters)
    };

    /// <summary>[S-2374] The rules' own exceptions on the pages, each with why: not to be mended.</summary>
    private static readonly Dictionary<string, string> PagesExceptions = new(StringComparer.Ordinal)
    {
        ["R4 General › Uninstall Themia"] = "a card of acts: its switch is a choice of the uninstall, no setting kept",
        ["R20 Accounts › Mail server (IMAP) › Test connection"] = "the form's note above its buttons says what is missing",
        ["R20 Accounts › Mail server (IMAP) › Add"] = "the form's note above its buttons says what is missing",
        ["R20 Accounts › Calendar server (CalDAV) › Test connection"] = "the form's note above its buttons says what is missing",
        ["R20 Accounts › Calendar server (CalDAV) › Add"] = "the form's note above its buttons says what is missing",
        ["R20 Accounts › Contacts server (CardDAV) › Test connection"] = "the form's note above its buttons says what is missing",
        ["R20 Accounts › Contacts server (CardDAV) › Add"] = "the form's note above its buttons says what is missing",
    };

    /// <summary>[S-2374] A settings line as the rules read it.</summary>
    internal static RuleRow RuleRowOf(SettingsLine line)
    {
        var row = line switch
        {
            SettingsSeparator => new RuleRow(string.Empty, RuleKind.Separator),
            SettingsHeading heading => new RuleRow(heading.Text, RuleKind.Heading),
            SettingsNote note => new RuleRow(note.Text, RuleKind.Note),
            SettingsReading reading => new RuleRow(reading.Text, RuleKind.Note) { Value = reading.Value, Glyph = reading.Glyph },
            SettingsPreview => new RuleRow(string.Empty, RuleKind.Note),
            SettingsField field => new RuleRow(field.Text, RuleKind.Value) { Value = field.Masked ? null : field.Value },
            SettingsToggle toggle => new RuleRow(toggle.Text, RuleKind.Toggle) { Glyph = toggle.Glyph, Value = toggle.Value },
            SettingsRadio radio => new RuleRow(radio.Text, RuleKind.Option) { Glyph = radio.Glyph },
            SettingsChoice choice => new RuleRow(choice.Text, RuleKind.Choice) { Glyph = choice.Glyph, Value = choice.Value, Below = [.. choice.Options.Select(RuleRowOf)] },
            SettingsSlider slider => new RuleRow(slider.Text, RuleKind.Number) { Glyph = slider.Glyph, Value = slider.Format(slider.Value) },
            SettingsColour colour => new RuleRow(colour.Text, RuleKind.Value),
            SettingsFill fill => new RuleRow(fill.Text, RuleKind.Value),
            SettingsFont font => new RuleRow(font.Text, RuleKind.Value) { Glyph = font.Glyph, Value = font.Value },
            SettingsIcon icon => new RuleRow(icon.Text, RuleKind.Value),
            SettingsAsk ask => new RuleRow(ask.Text, RuleKind.Value) { Glyph = ask.Glyph, Value = ask.Value },
            SettingsCommand command => new RuleRow(command.Text, RuleKind.Action) { Glyph = command.Glyph, Value = command.Value, Danger = command.Danger },
            SettingsItem item => new RuleRow(item.Text, RuleKind.Action) { Glyph = item.Glyph, Thing = true },
            // [S-2310] A thing's card (a view, a display, an account) says what it is under its name; its name is the user's.
            SettingsSubmenu submenu => new RuleRow(submenu.Text, RuleKind.Level)
            {
                Glyph = submenu.Glyph,
                Value = submenu.Value ?? (submenu.Card ? submenu.Caption : null),
                Card = submenu.Card,
                Thing = submenu.Card && submenu.Caption is not null,
                Below = [.. submenu.Lines.Select(Inside)],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(line), line.GetType().Name, "a line the rules cannot read"),
        };

        // A line wearing a thing's own mark (a view's, in its colour) is that thing: its name and mark are the user's.
        return row with { Disabled = line.Disabled, Why = line.Why, Thing = row.Thing || line.Marked || line.GlyphColour is not null };
    }

    /// <summary>[S-2348] A line inside a card as drawn: no glyph of its setting, a mark of its own kept (Karpo's §644).</summary>
    private static RuleRow Inside(SettingsLine line)
    {
        var row = RuleRowOf(line);
        return line.Marked || line.GlyphColour is not null || line.Picture is not null || line is SettingsHeading ? row : row with { Glyph = null };
    }

    /// <summary>[S-2374] A page as the rules read it: each section's heading over its lines.</summary>
    internal static IReadOnlyList<RuleRow> RuleRowsOf(SettingsPage page)
        => [.. page.Sections.SelectMany(s => (s.Heading is { } heading ? [new RuleRow(heading, RuleKind.Heading)] : Array.Empty<RuleRow>()).Concat(s.Lines.Select(RuleRowOf)))];

    /// <summary>Every offence on every page of the edit bar, as a world with views and displays shows them.</summary>
    private static List<RuleOffence> PageOffences()
    {
        var world = new World();
        var pages = new[]
        {
            SettingsPages.General(world.Settings), SettingsPages.Accounts(world.Settings), SettingsPages.Display(world.Display),
            SettingsPages.Views(world.Views), SettingsPages.Appearance(world.Settings),
        };

        var found = pages.SelectMany(page => MenuRules.Check(RuleRowsOf(page)).Select(o => o with { Path = page.Name + " › " + o.Path })).ToList();
        return found;
    }

    [Fact]
    public void S2374_every_page_of_the_edit_bar_keeps_karpos_menu_rules()
    {
        var offences = PageOffences();
        var keys = offences.Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
        var broken = offences.Where(o => !PagesAllowed.Contains(o.Key) && !PagesExceptions.ContainsKey(o.Key)).GroupBy(o => o.Key).Select(g => g.First()).ToList();
        var mended = PagesAllowed.Where(k => !keys.Contains(k)).ToList();

        var lines = broken.Select(o => $"        \"{o.Key}\", // {o.What}").ToList();
        if (Environment.GetEnvironmentVariable("THEMIA_MENU_RULES") is { Length: > 0 } dump)
        {
            File.WriteAllLines(dump, lines);
        }

        Assert.True(broken.Count == 0, "rules broken:\n" + string.Join("\n", lines));
        Assert.True(mended.Count == 0, "kept now; take them off the allowance:\n" + string.Join("\n", mended));
    }

    [Fact]
    public void S2374_the_rules_read_each_break()
    {
        static IReadOnlyList<string> Rules(params RuleRow[] rows) => [.. MenuRules.Check(rows).Select(o => o.Rule).Distinct()];
        var off = Str.Toggle_Off.Text();

        Assert.Equal(["R3"], Rules(new RuleRow("Sound", RuleKind.Choice) { Value = off, Below = [new RuleRow(off, RuleKind.Option), new RuleRow("Chime", RuleKind.Option)] }));
        Assert.Equal(["R4"], Rules(new RuleRow("Rows", RuleKind.Number) { Glyph = "a" }));
        Assert.Equal(["R7"], Rules(new RuleRow("Location", RuleKind.Value) { Value = "Zurich" }));
        Assert.Equal(["R8"], Rules(new RuleRow("Back up & reset", RuleKind.Level)));
        Assert.Equal(["R8"], Rules(new RuleRow("Add widgets here", RuleKind.Action)));
        Assert.Equal(["R8"], Rules(new RuleRow("Show Seconds", RuleKind.Toggle)));
        Assert.Equal(["R10"], Rules(new RuleRow("Look", RuleKind.Heading), new RuleRow("Text", RuleKind.Heading), new RuleRow("Size", RuleKind.Value)));
        Assert.Equal(["R11"], Rules(new RuleRow("Reset", RuleKind.Action) { Glyph = Glyphs.Reset }));
        Assert.Equal(["R12"], Rules(new RuleRow("Delete", RuleKind.Action) { Danger = true }, new RuleRow("Rename", RuleKind.Action)));
        Assert.Equal(["R13"], Rules(new RuleRow("Rows", RuleKind.Toggle) { Glyph = "a" }, new RuleRow("Lines", RuleKind.Toggle) { Glyph = "a" }));
        Assert.Equal(["R14"], Rules(new RuleRow("Place", RuleKind.Value) { Value = "A value far longer than twenty-four letters" }));
        Assert.Equal(["R18"], Rules(new RuleRow("Clock", RuleKind.Choice) { Value = "Windows setting (12h)" }));
        Assert.Equal(["R18"], Rules(new RuleRow("Colour", RuleKind.Value) { Value = Str.Common_Default.Text() }));
        Assert.Equal(["R20"], Rules(new RuleRow("Delete view", RuleKind.Action) { Disabled = true }));
        Assert.Equal(["R21"], Rules(new RuleRow("Rows", RuleKind.Number) { Value = "12" }));

        // What keeps them: a default said with what it comes to (measured inside it), a greyed row saying why, a unit.
        Assert.Empty(Rules(
            new RuleRow("Clock", RuleKind.Choice) { Value = Str.Common_DefaultOf.Format(("value", "24-hour")) },
            new RuleRow("Delete view", RuleKind.Action) { Disabled = true, Why = "Themia keeps one view" },
            new RuleRow("Rows", RuleKind.Number) { Value = "12 rows" }));
        Assert.Equal("24-hour", MenuRules.Measured(Str.Common_DefaultOf.Format(("value", "24-hour"))));
    }
}
