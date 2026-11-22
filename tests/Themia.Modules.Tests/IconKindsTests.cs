using System.Reflection;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-15000] [S-15003] Every icon kind of the build is on the one base: its sizing the base's, a one-readout face with its
/// one item, its glyph one character; and the kinds on it are the ones their modules say.
/// </summary>
public sealed class IconKindsTests
{
    private static IReadOnlyList<IconModule> Icons => [.. ModuleRegistry.Widgets.OfType<IconModule>()];

    [Fact]
    public void S15003_the_icon_kinds_are_the_buttons_and_the_icon()
        => Assert.Equal(["icon", "musicNext", "musicPlayPause", "musicPrevious"],
            Icons.Select(m => m.Descriptor.Id.Value).Order(StringComparer.Ordinal));

    [Fact]
    public void S15000_every_icon_kind_sizes_as_the_base_and_is_a_one_readout_face_with_its_item()
    {
        foreach (var module in Icons)
        {
            var descriptor = module.Descriptor;
            var config = descriptor.CreateDefault();
            Assert.Equal(SizeStretch.Relax(IconRules.Sizing()), descriptor.Sizing(config));
            Assert.Equal(WidgetTraits.HasItems | WidgetTraits.OneReadout, descriptor.Traits);
            Assert.Equal(module.Kind.Id, descriptor.Id.Value);
            Assert.Equal(module.Kind.ConfigType, config.GetType());
            Assert.Single(module.Kind.Glyph.EnumerateRunes());
        }
    }

    /// <summary>
    /// [S-15160] A stale sky's cloud stood left of centre and smaller: the face took the status glyph's width off its end.
    /// No part of the icon face leaves the status room; the status stands in the card's corner over the inset.
    /// </summary>
    [Fact]
    public void S15160_no_part_of_the_icon_face_gives_the_status_glyph_room()
    {
        var parts = typeof(IconView).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Append(typeof(IconView));
        Assert.DoesNotContain(parts, t => typeof(IStatusRoom).IsAssignableFrom(t));
    }
}
