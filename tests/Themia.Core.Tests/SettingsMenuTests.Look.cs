using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2141] The look tokens on the Appearance page: every token one row of its own, its range the token's, one write each.</summary>
public sealed partial class SettingsMenuTests
{
    private static List<SettingsLine> AppearanceLines(FakeHost host)
        => [.. Flat(SettingsPages.Appearance(host).Sections.SelectMany(s => s.Lines))];

    [Fact]
    public void S2141_every_number_token_is_one_slider_of_its_range_that_writes_it_alone()
    {
        foreach (var n in LookTokens.Numbers)
        {
            var host = new FakeHost();
            var sliders = AppearanceLines(host).OfType<SettingsSlider>().Where(s => s.Key.EndsWith("/" + n.Key, StringComparison.Ordinal)).ToList();
            var slider = Assert.Single(sliders);
            Assert.Equal((n.Min, n.Max, n.Step), (slider.Min, slider.Max, slider.Step));
            Assert.Equal(n.Default, slider.Value);

            var value = n.Default == n.Max ? n.Min : n.Max;
            slider.Set(value);
            Assert.Equal(1, host.Mutations);
            Assert.Equal(value, n.Get(host.State.Design.Tokens));
            Assert.All(LookTokens.Numbers.Where(o => o != n), o => Assert.Equal(o.Default, o.Get(host.State.Design.Tokens)));

            slider.Set(n.Max + 1000);
            Assert.Equal(n.Max, n.Get(host.State.Design.Tokens));
        }
    }

    [Fact]
    public void S2141_every_enum_token_is_one_choice_and_figures_a_toggle()
    {
        string[] choices = ["rendering", "headerWeight", "readoutWeight", "bodyWeight", "headerTone", "headerCase", "innerSurface", "rowDividers"];
        var lines = AppearanceLines(new FakeHost());
        foreach (var key in choices)
        {
            Assert.Single(lines.OfType<SettingsChoice>(), c => c.Key.EndsWith("/" + key, StringComparison.Ordinal));
        }

        var host = new FakeHost();
        var weight = AppearanceLines(host).OfType<SettingsChoice>().Single(c => c.Key.EndsWith("/readoutWeight", StringComparison.Ordinal));
        Assert.Equal(6, weight.Options.Count);
        weight.Options.Single(o => o.Key.EndsWith("/" + TypeWeight.Light, StringComparison.Ordinal)).Pick();
        Assert.Equal(TypeWeight.Light, host.State.Design.Tokens.ReadoutWeight);

        var dividers = AppearanceLines(host).OfType<SettingsChoice>().Single(c => c.Key.EndsWith("/rowDividers", StringComparison.Ordinal));
        dividers.Options.Single(o => o.Key.EndsWith("/" + RowDivider.Inset, StringComparison.Ordinal)).Pick();
        Assert.Equal(RowDivider.Inset, host.State.Design.Tokens.RowDividers);

        var figures = Assert.Single(AppearanceLines(host).OfType<SettingsToggle>(), t => t.Key.EndsWith("/tabularNumerals", StringComparison.Ordinal));
        figures.Set(true);
        Assert.True(host.State.Design.Tokens.TabularNumerals);
    }

    [Fact]
    public void S2141_the_look_tokens_are_the_appearance_pages_sections_and_the_advanced_ones_open_in_their_card()
    {
        var page = SettingsPages.Appearance(new FakeHost());
        var material = page.Sections.Single(s => s.Key == "appearance/material");
        var text = page.Sections.Single(s => s.Key == "appearance/text");

        // The glass's own values and each text level's opacity are one expander each: a card's one level inside it.
        var glass = Assert.Single(material.Lines.OfType<SettingsSubmenu>());
        var levels = Assert.Single(text.Lines.OfType<SettingsSubmenu>());
        Assert.Equal(SettingsRowKind.Expander, SettingsRows.KindOf(glass));
        Assert.Equal(SettingsRowKind.Expander, SettingsRows.KindOf(levels));
        Assert.All(glass.Lines.Concat(levels.Lines), l => Assert.IsType<SettingsSlider>(l));
        Assert.Equal(["appearance/edge", "appearance/surfaces", "appearance/layout"], page.Sections.Select(s => s.Key).Where(k => k is "appearance/edge" or "appearance/surfaces" or "appearance/layout"));
    }
}
