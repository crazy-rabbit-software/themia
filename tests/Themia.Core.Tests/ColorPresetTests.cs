using Themia.Core.Settings;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1549] The colour submenu's presets: Windows' accent palette and the surfaces' neutrals.</summary>
public sealed class ColorPresetTests
{
    [Fact]
    public void S1549_the_windows_palette_is_48_distinct_colours_in_rows_of_8()
    {
        Assert.Equal(48, ColorPresets.Windows.Count);
        Assert.Equal(0, ColorPresets.Windows.Count % ColorPresets.Columns);
        Assert.Equal(ColorPresets.Columns, ColorPresets.Neutrals.Count);

        foreach (var preset in ColorPresets.Surfaces)
        {
            Assert.Equal(preset.Hex, HexColor.Normalise(preset.Hex));
            Assert.False(string.IsNullOrWhiteSpace(preset.Name));
        }

        Assert.Equal(ColorPresets.Surfaces.Count, ColorPresets.Surfaces.Select(p => p.Hex).Distinct().Count());
    }

    [Fact]
    public void S1549_a_stored_value_finds_its_preset_whatever_its_case()
    {
        Assert.Equal("Default blue", ColorPresets.Find(ColorPresets.Windows, "#0078d4")?.Name);
        Assert.Equal("Default blue", ColorPresets.Find(ColorPresets.Windows, "0078D4")?.Name);
        Assert.Null(ColorPresets.Find(ColorPresets.Windows, "#123456"));
        Assert.Null(ColorPresets.Find(ColorPresets.Windows, string.Empty));
        Assert.Null(ColorPresets.Find(ColorPresets.Windows, "not a colour"));
    }
}
