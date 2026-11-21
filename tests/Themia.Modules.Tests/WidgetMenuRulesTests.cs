using Themia.App.Widgets;
using Themia.Core.Settings;
using Themia.Modules.Tests.Modules;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-2374] Karpo's menu rules (karpo-app <c>MenuRulesTests</c>, <c>MenuConventionTests.EveryNumberSaysItsUnit</c>) on every
/// widget's page: each kind's own lines and its style's, as a new widget of the kind has them (owner, 2026-11-19: "make
/// sure that you put the features of the karpo settings into themia too"). A kind added later keeps them or fails here.
/// What breaks a rule today and is still to be mended is <see cref="Allowed"/>, one line each; a line kept now fails too,
/// so the list only shrinks.
/// </summary>
public sealed class WidgetMenuRulesTests
{
    /// <summary>[S-2374] What still breaks a rule on a widget's page, by <see cref="RuleOffence.Key"/>: only shrinks.</summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "R14 airQuality › Index", // its value "Automatic: European in Europe" is 29 characters
        "R14 clock › Timezone", // its value "Current: Amsterdam, Berlin, …" is 57 characters
        "R13 exchangeHistory › Base currency", // its icon is also Currency's
        "R13 exchangeRate › Base currency", // its icon is also Currency's
        "R13 fence › Shows", // its icon is also New items's
        "R14 fence › Roll up with a click on the title", // its title is cut (33 characters)
        "R14 fence › Open while the pointer is over it", // its title is cut (33 characters)
        "R13 gallery › Folder", // its icon is also Include subfolders's
        "R13 nextMeeting › Organiser", // its icon is also Accounts's
    };

    /// <summary>The kinds whose lines read a list only the running host installs (as <c>SettingOwnershipTests</c>).</summary>
    private static readonly string[] NeedsTheHost = ["brightness", "desktopItem", "disk", "drive", "driveActivity", "driveCapacity", "driveFileSystem", "driveFree", "driveType", "driveUsed", "gpu", "gpuClock", "gpuDedicatedMemory", "gpuEngines", "gpuFan", "gpuLoad", "gpuMemoryClock", "gpuSharedMemory", "gpuTemperature"];

    /// <summary>[S-2374] A widget's entry as the rules read it.</summary>
    internal static RuleRow RowOf(SettingsLine entry)
    {
        RuleRow Row(RuleKind kind) => new(entry.Text, kind)
        {
            Glyph = entry.Glyph,
            Value = entry.Value,
            Danger = entry.Danger,
            Disabled = entry.Disabled,
            Why = entry.Why,
        };

        return entry switch
        {
            SettingsSeparator => new RuleRow(string.Empty, RuleKind.Separator),
            SettingsNote => Row(RuleKind.Note),
            SettingsSlider => Row(RuleKind.Number),
            SettingsColour or SettingsFill or SettingsIcon or SettingsAsk => Row(RuleKind.Value),
            SettingsSearch => Row(RuleKind.Value),
            SettingsChoice choice => Row(RuleKind.Choice) with { Below = [.. entry.Children!.Select(Inside)] },
            SettingsSubmenu submenu => Row(RuleKind.Level) with { Thing = submenu.Order is not null, Below = [.. submenu.Lines.Select(Inside)] },
            SettingsRadio => Row(RuleKind.Option),
            SettingsToggle => Row(RuleKind.Toggle),
            SettingsLink => Row(RuleKind.Link),
            _ => Row(RuleKind.Action),
        };
    }

    /// <summary>[S-2348] An entry inside a card as drawn: no glyph of its setting, an icon of its own (a file's, a chosen one) kept.</summary>
    private static RuleRow Inside(SettingsLine entry)
    {
        var row = RowOf(entry);
        return entry.IconFile is not null || entry.Icon is not null ? row : row with { Glyph = null };
    }

    /// <summary>Every offence on every kind's page, each kind's path under its id.</summary>
    private static List<RuleOffence> Offences()
    {
        var found = new List<RuleOffence>();
        foreach (var module in ModuleRegistry.Widgets)
        {
            var kind = module.Descriptor.Id.Value;
            if (NeedsTheHost.Contains(kind))
            {
                continue;
            }

            var menu = new WidgetMenuLines(() => { }) { OpenSetting = _ => { } };
            module.MenuLines(menu, module.Descriptor.CreateDefault());
            var rows = menu.StyleLines.Concat(menu.Lines).Select(RowOf).ToList();
            found.AddRange(MenuRules.Check(rows).Select(o => o with { Path = kind + " › " + o.Path }));
        }

        return found;
    }

    [Fact]
    public void S2374_every_widgets_page_keeps_karpos_menu_rules()
    {
        var offences = Offences();
        var keys = offences.Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
        var broken = offences.Where(o => !Allowed.Contains(o.Key)).GroupBy(o => o.Key).Select(g => g.First()).ToList();
        var mended = Allowed.Where(k => !keys.Contains(k)).ToList();

        var lines = broken.Select(o => $"        \"{o.Key}\", // {o.What}").ToList();
        if (Environment.GetEnvironmentVariable("THEMIA_MENU_RULES") is { Length: > 0 } dump)
        {
            File.WriteAllLines(dump, lines);
        }

        Assert.True(broken.Count == 0, "rules broken:\n" + string.Join("\n", lines));
        Assert.True(mended.Count == 0, "kept now; take them off the allowance:\n" + string.Join("\n", mended));
    }
}
