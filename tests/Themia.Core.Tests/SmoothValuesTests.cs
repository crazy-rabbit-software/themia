using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6329] The user's switch for smooth value changes: readouts glide and charts scroll only with it on.</summary>
public sealed class SmoothValuesTests
{
    [Fact]
    public void S6329_values_move_smoothly_only_with_windows_animations_and_the_switch_on()
    {
        var on = new DesignState();
        var off = new DesignState { SmoothValues = false };

        Assert.True(Glide.Smooth(on, windowsAnimations: true));
        Assert.False(Glide.Smooth(off, windowsAnimations: true));
        Assert.False(Glide.Smooth(on, windowsAnimations: false));
        Assert.False(Glide.Smooth(off, windowsAnimations: false));

        // Before the first design is applied the default holds: on, Windows permitting.
        Assert.True(Glide.Smooth(null, windowsAnimations: true));
        Assert.False(Glide.Smooth(null, windowsAnimations: false));
    }

    [Fact]
    public void S6329_the_switch_is_on_by_default_and_copied_with_the_design()
    {
        Assert.True(AppState.CreateDefault().Design.SmoothValues);
        Assert.False(new DesignState { SmoothValues = false }.Clone().SmoothValues);
    }

    [Fact]
    public void S6329_off_survives_a_save_and_a_load()
    {
        var state = AppState.CreateDefault();
        state.Design.SmoothValues = false;

        var json = JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
        var loaded = JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppState)!;

        Assert.False(loaded.Design.SmoothValues);
    }

    [Fact]
    public void S6329_a_design_preset_leaves_the_switch_alone()
    {
        // Motion is the user's accessibility choice, never a look's ([S-1014]).
        var design = new DesignState { SmoothValues = false };

        foreach (var preset in DesignPresets.All)
        {
            DesignPresets.Apply(design, preset.Id);
            Assert.False(design.SmoothValues);
        }
    }
}
