using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.QuickToggles.Tests;

/// <summary>[S-6901] Each kind of the family stands for its one toggle, from its own config.</summary>
public sealed class QuickTogglesTests
{
    /// <summary>[S-8200] The Wifi toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S8200_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("wifiToggle");
        var config = Assert.IsType<WifiToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.Wifi, config.Toggle);
        Assert.Equal(QuickToggle.Wifi, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.Wifi));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<WifiToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-8300] The Bluetooth toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S8300_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("bluetoothToggle");
        var config = Assert.IsType<BluetoothToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.Bluetooth, config.Toggle);
        Assert.Equal(QuickToggle.Bluetooth, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.Bluetooth));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<BluetoothToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-8400] The Airplane mode toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S8400_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("airplaneModeToggle");
        var config = Assert.IsType<AirplaneModeToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.AirplaneMode, config.Toggle);
        Assert.Equal(QuickToggle.AirplaneMode, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.AirplaneMode));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<AirplaneModeToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-8900] The Sound toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S8900_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("soundToggle");
        var config = Assert.IsType<SoundToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.Sound, config.Toggle);
        Assert.Equal(QuickToggle.Sound, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.Sound));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<SoundToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-9000] The Microphone toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S9000_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("microphoneToggle");
        var config = Assert.IsType<MicrophoneToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.Microphone, config.Toggle);
        Assert.Equal(QuickToggle.Microphone, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.Microphone));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<MicrophoneToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-9100] The Spatial sound toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S9100_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("spatialSoundToggle");
        var config = Assert.IsType<SpatialSoundToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.SpatialSound, config.Toggle);
        Assert.Equal(QuickToggle.SpatialSound, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.SpatialSound));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<SpatialSoundToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-9200] The Mono audio toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S9200_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("monoAudioToggle");
        var config = Assert.IsType<MonoAudioToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.MonoAudio, config.Toggle);
        Assert.Equal(QuickToggle.MonoAudio, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.MonoAudio));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<MonoAudioToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-8600] The Night light toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S8600_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("nightLightToggle");
        var config = Assert.IsType<NightLightToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.NightLight, config.Toggle);
        Assert.Equal(QuickToggle.NightLight, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.NightLight));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<NightLightToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-9300] The Color filters toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S9300_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("colorFiltersToggle");
        var config = Assert.IsType<ColorFiltersToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.ColorFilters, config.Toggle);
        Assert.Equal(QuickToggle.ColorFilters, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.ColorFilters));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<ColorFiltersToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-8700] The Rotation lock toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S8700_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("rotationLockToggle");
        var config = Assert.IsType<RotationLockToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.RotationLock, config.Toggle);
        Assert.Equal(QuickToggle.RotationLock, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.RotationLock));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<RotationLockToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-8500] The Energy saver toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S8500_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("energySaverToggle");
        var config = Assert.IsType<EnergySaverToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.EnergySaver, config.Toggle);
        Assert.Equal(QuickToggle.EnergySaver, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.EnergySaver));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<EnergySaverToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-8800] The Do not disturb toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S8800_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("doNotDisturbToggle");
        var config = Assert.IsType<DoNotDisturbToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.DoNotDisturb, config.Toggle);
        Assert.Equal(QuickToggle.DoNotDisturb, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.DoNotDisturb));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<DoNotDisturbToggleConfig>(config.Clone()).Style);
    }

    /// <summary>[S-9400] The Game mode toggle stands for its one quick setting, from its own config.</summary>
    [Fact]
    public void S9400_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("gameModeToggle");
        var config = Assert.IsType<GameModeToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.GameMode, config.Toggle);
        Assert.Equal(QuickToggle.GameMode, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.GameMode));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<GameModeToggleConfig>(config.Clone()).Style);
    }
}
