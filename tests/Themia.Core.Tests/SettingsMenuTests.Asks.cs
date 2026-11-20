using System.Reflection;
using Themia.Core.Model;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2351] No page of its own for one input, anywhere in the settings (owner, 2026-11-20: "this type of settings
/// component should not exist anywhere in themia settings. implement and replace everywhere."): a line of text is typed in
/// its box in its row or asked in place in its card, a question is asked in place in the card of what asks it, and a page
/// of its own is only ever many lines.
/// </summary>
public sealed partial class SettingsMenuTests
{
    /// <summary>Every line of <paramref name="lines"/> at any depth: a card's lines, a choice's options.</summary>
    private static IEnumerable<SettingsLine> EveryLine(IEnumerable<SettingsLine> lines)
        => lines.SelectMany(line => line switch
        {
            SettingsSubmenu submenu => [line, .. EveryLine(submenu.Lines)],
            SettingsChoice choice => [line, .. EveryLine(choice.Options)],
            _ => (IEnumerable<SettingsLine>)[line],
        });

    /// <summary>A line that takes one value or answers one question: what a page of its own must never be opened for.</summary>
    private static bool OneInput(SettingsLine line) => line is SettingsAsk or SettingsField or SettingsCommand { Confirm: not null };

    /// <summary>Every page of the edit bar over a world with views (one showing chosen desktop icons: a real page of its own) and accounts.</summary>
    private static IEnumerable<SettingsPage> EveryPage()
    {
        var world = new World();
        world.Settings.State.Views[1].DesktopIcons = ViewDesktopIcons.Chosen;
        var (accounts, _) = WithAccounts(Microsoft, MailServer);
        return
        [
            SettingsPages.General(world.Settings), SettingsPages.Accounts(accounts), SettingsPages.Display(world.Display),
            SettingsPages.Views(world.Views), SettingsPages.Appearance(world.Settings),
        ];
    }

    [Fact]
    public void S2351_no_line_of_any_page_opens_a_page_of_its_own_for_one_input()
    {
        var lines = EveryPage().SelectMany(p => EveryLine(p.Sections.SelectMany(s => s.Lines))).ToList();
        var pages = 0;
        foreach (var line in lines)
        {
            switch (line)
            {
                // A line of text is typed in its box in its own row.
                case SettingsAsk or SettingsField:
                    Assert.Equal(SettingsRowKind.Text, SettingsRows.KindOf(line));
                    break;

                // A command asked first asks in place, in its own card.
                case SettingsCommand { Confirm: not null }:
                    Assert.Equal(SettingsRowKind.Ask, SettingsRows.KindOf(line));
                    break;

                // A page of its own holds many lines, never one input alone.
                case SettingsSubmenu submenu when SettingsRows.KindOf(submenu) == SettingsRowKind.SubPage:
                    pages++;
                    var rows = submenu.Lines.Where(l => l is not (SettingsNote or SettingsSeparator or SettingsHeading or SettingsReading)).ToList();
                    Assert.False(rows is [var only] && OneInput(only), $"'{submenu.Key}' is a page of its own for one input");
                    break;
            }

            // A tool that asks first asks in place in its thing's card, and does nothing else of its own.
            foreach (var tool in (line as SettingsSubmenu)?.Tools ?? [])
            {
                Assert.False(tool.Confirm is not null && tool.Run is not null, $"'{tool.Key}' both asks and runs");
            }
        }

        // The walk saw the asking lines and a real page of its own (a view's chosen desktop icons).
        Assert.Contains(lines, l => l is SettingsCommand { Confirm: not null });
        Assert.Contains(lines, l => l is SettingsSubmenu { Tools: var tools } && tools.Any(t => t.Confirm is not null));
        Assert.True(pages > 0);
    }

    [Fact]
    public void S2351_a_page_asks_only_at_a_line_of_its_own()
    {
        // The hosts' one way to ask after a step of their own names the line it opens at; the views' host asks nothing.
        foreach (var method in typeof(ISettingsMenuHost).GetMethods().Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(SettingsConfirm))))
        {
            var parameters = method.GetParameters();
            Assert.Equal(("at", typeof(string)), (parameters[0].Name, parameters[0].ParameterType));
        }

        Assert.DoesNotContain(typeof(IViewsMenuHost).GetMethods(), m => m.GetParameters().Any(p => p.ParameterType == typeof(SettingsConfirm)));
        Assert.Null(typeof(SettingsTool).GetProperty("Ask", BindingFlags.Public | BindingFlags.Instance));

        // Where the questions after a step open: the import's under Import settings, the preset's in the Style card.
        var host = new FakeHost();
        Assert.NotNull(SettingsPages.PathTo(SettingsPages.General(host), SettingsMenu.Keys.BackupImport));
        Assert.NotNull(SettingsPages.PathTo(SettingsPages.Appearance(host), SettingsMenu.Keys.Appearance + "/style"));
    }

    [Fact]
    public void S2351_a_view_and_an_account_ask_their_delete_and_sign_out_in_their_card()
    {
        var world = new World();
        var view = SettingsPages.Views(world.Views).Sections.SelectMany(s => s.Lines).OfType<SettingsSubmenu>().First(s => s.Card);
        var delete = Assert.Single(view.Tools, t => t.Shortcut == SettingsToolKey.Delete);
        Assert.NotNull(delete.Confirm);
        Assert.Null(delete.Run);

        // Its name is typed in its box in its own row, never on a page of its own.
        Assert.Contains(view.Lines, l => l is SettingsAsk && SettingsRows.KindOf(l) == SettingsRowKind.Text);
    }
}
