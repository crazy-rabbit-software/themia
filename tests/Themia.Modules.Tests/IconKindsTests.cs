using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.WinUI;
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
    public void S15003_the_icon_kinds_are_the_buttons_the_icon_and_the_sky()
        => Assert.Equal(["icon", "musicNext", "musicPlayPause", "musicPrevious", "weatherSky"],
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
}
