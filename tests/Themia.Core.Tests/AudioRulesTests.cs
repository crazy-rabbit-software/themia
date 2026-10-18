using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1220]..[S-1234] The audio widgets' shared rules: device binding, keys, stepping, meters and the
/// mixer's row ladder. CI has no audio device, so everything a person sees that does not need one is
/// pinned here.
/// </summary>
public class AudioRulesTests
{
    private static readonly AudioDevice[] Devices =
    [
        new("{a}", "Speakers (Realtek(R) Audio)"),
        new("{b}", "Headphones"),
    ];

    // ---- [S-1221] which device ------------------------------------------------------------------

    [Fact]
    public void S1221_no_choice_follows_the_default()
        => Assert.Equal(("{b}", AudioTarget.Default), AudioRules.Resolve(null, Devices, "{b}"));

    [Fact]
    public void S1221_an_empty_choice_is_no_choice()
        => Assert.Equal(("{a}", AudioTarget.Default), AudioRules.Resolve(string.Empty, Devices, "{a}"));

    [Fact]
    public void S1221_no_default_and_no_choice_is_missing()
        => Assert.Equal(((string?)null, AudioTarget.Missing), AudioRules.Resolve(null, Devices, null));

    [Fact]
    public void S1221_a_connected_choice_wins_over_the_default()
        => Assert.Equal(("{b}", AudioTarget.Chosen), AudioRules.Resolve("{B}", Devices, "{a}"));

    [Fact]
    public void S1221_an_unplugged_choice_is_missing_never_the_default()
        => Assert.Equal(((string?)null, AudioTarget.Missing), AudioRules.Resolve("{gone}", Devices, "{a}"));

    [Fact]
    public void S1221_a_device_is_named_by_windows_then_by_what_was_stored()
    {
        Assert.Equal("Headphones", AudioRules.DeviceName("{b}", Devices, "Old name"));
        Assert.Equal("Old name", AudioRules.DeviceName("{gone}", Devices, " Old name "));
        Assert.Equal("Output device", AudioRules.DeviceName("{gone}", Devices, null));
        Assert.Equal("Output device", AudioRules.DeviceName("{x}", [new AudioDevice("{x}", "  ")], null));
    }

    // ---- [S-1234] stepping ----------------------------------------------------------------------

    [Theory]
    [InlineData(0.50, 2, 0.52)]
    [InlineData(0.374, 2, 0.39)]
    [InlineData(0.99, 2, 1.0)]
    [InlineData(0.01, -2, 0.0)]
    [InlineData(0.5, -10, 0.4)]
    [InlineData(double.NaN, 2, 0.02)]
    public void S1234_a_step_lands_on_the_whole_percent_grid_and_clamps(double level, int delta, double expected)
        => Assert.Equal(expected, AudioRules.Step(level, delta), 6);

    // ---- [S-1226] volume keys -------------------------------------------------------------------

    [Theory]
    [InlineData(WidgetKey.Right, AudioKeyKind.Step, 2)]
    [InlineData(WidgetKey.Up, AudioKeyKind.Step, 2)]
    [InlineData(WidgetKey.Plus, AudioKeyKind.Step, 2)]
    [InlineData(WidgetKey.Left, AudioKeyKind.Step, -2)]
    [InlineData(WidgetKey.Down, AudioKeyKind.Step, -2)]
    [InlineData(WidgetKey.Minus, AudioKeyKind.Step, -2)]
    [InlineData(WidgetKey.PageUp, AudioKeyKind.Step, 10)]
    [InlineData(WidgetKey.PageDown, AudioKeyKind.Step, -10)]
    [InlineData(WidgetKey.Home, AudioKeyKind.Step, -100)]
    [InlineData(WidgetKey.End, AudioKeyKind.Step, 100)]
    [InlineData(WidgetKey.Mute, AudioKeyKind.ToggleMute, 0)]
    [InlineData(WidgetKey.Space, AudioKeyKind.ToggleMute, 0)]
    [InlineData(WidgetKey.Enter, AudioKeyKind.None, 0)]
    [InlineData(WidgetKey.Escape, AudioKeyKind.None, 0)]
    public void S1226_volume_keys(WidgetKey key, AudioKeyKind kind, int delta)
    {
        var action = AudioRules.VolumeKey(key);
        Assert.Equal((kind, delta), (action.Kind, action.Delta));
    }

    // ---- [S-1225] mixer keys --------------------------------------------------------------------

    [Theory]
    [InlineData(WidgetKey.Down, null, AudioKeyKind.Move, 0)]
    [InlineData(WidgetKey.Up, null, AudioKeyKind.Move, 4)]
    [InlineData(WidgetKey.Down, 1, AudioKeyKind.Move, 2)]
    [InlineData(WidgetKey.Down, 4, AudioKeyKind.Move, 4)]
    [InlineData(WidgetKey.Home, 3, AudioKeyKind.Move, 0)]
    [InlineData(WidgetKey.End, 0, AudioKeyKind.Move, 4)]
    [InlineData(WidgetKey.Escape, 2, AudioKeyKind.Clear, 0)]
    [InlineData(WidgetKey.Menu, 2, AudioKeyKind.Menu, 2)]
    [InlineData(WidgetKey.Mute, 3, AudioKeyKind.ToggleMute, 3)]
    [InlineData(WidgetKey.Space, 3, AudioKeyKind.ToggleMute, 3)]
    [InlineData(WidgetKey.Right, 1, AudioKeyKind.Step, 1)]
    [InlineData(WidgetKey.Enter, 1, AudioKeyKind.None, 0)]
    public void S1225_mixer_keys(WidgetKey key, int? focused, AudioKeyKind kind, int index)
    {
        var action = AudioRules.MixerKey(key, focused, 5);
        Assert.Equal(kind, action.Kind);
        Assert.Equal(index, action.Index);
    }

    [Theory]
    [InlineData(WidgetKey.Right)]
    [InlineData(WidgetKey.Plus)]
    [InlineData(WidgetKey.Mute)]
    [InlineData(WidgetKey.Menu)]
    public void S1225_with_no_row_focused_a_row_key_only_focuses_the_first_row(WidgetKey key)
        => Assert.Equal(new AudioKeyAction(AudioKeyKind.Move, 0), AudioRules.MixerKey(key, null, 3));

    [Fact]
    public void S1225_left_right_change_the_row_rather_than_move()
    {
        Assert.Equal(new AudioKeyAction(AudioKeyKind.Step, 2, -2), AudioRules.MixerKey(WidgetKey.Left, 2, 3));
        Assert.Equal(new AudioKeyAction(AudioKeyKind.Step, 2, 10), AudioRules.MixerKey(WidgetKey.PageUp, 2, 3));
    }

    [Fact]
    public void S1225_an_empty_or_stale_cursor_is_safe()
    {
        Assert.Equal(AudioKeyKind.None, AudioRules.MixerKey(WidgetKey.Down, null, 0).Kind);
        Assert.Equal(AudioKeyKind.Clear, AudioRules.MixerKey(WidgetKey.Escape, null, 0).Kind);

        // A cursor past the end (its row left) behaves as no cursor.
        Assert.Equal(new AudioKeyAction(AudioKeyKind.Move, 0), AudioRules.MixerKey(WidgetKey.Right, 7, 3));
    }

    [Theory]
    [InlineData(0x6B, false, WidgetKey.Plus)]
    [InlineData(0xBB, false, WidgetKey.Plus)]
    [InlineData(0xBB, true, WidgetKey.Plus)]
    [InlineData(0x6D, false, WidgetKey.Minus)]
    [InlineData(0xBD, false, WidgetKey.Minus)]
    [InlineData(0x4D, false, WidgetKey.Mute)]
    public void S1225_plus_minus_and_m_map_to_widget_keys(int vk, bool shift, WidgetKey expected)
        => Assert.Equal(expected, WidgetKeys.FromVirtualKey(vk, shift, ctrl: false, alt: false));

    [Theory]
    [InlineData(0x4D, true, false)] // Shift+M: a capital, type-ahead
    [InlineData(0x4D, false, true)] // Ctrl+M
    [InlineData(0xBB, false, true)] // Ctrl+= is a zoom shortcut
    public void S1225_chords_on_the_audio_keys_stay_the_users(int vk, bool shift, bool ctrl)
        => Assert.Null(WidgetKeys.FromVirtualKey(vk, shift, ctrl, alt: false));

    // ---- [S-1227] [S-1228] [S-1231] the mixer's rows ---------------------------------------------

    [Theory]
    [InlineData(120, MixerRowStyle.Compact)]
    [InlineData(169.9, MixerRowStyle.Compact)]
    [InlineData(170, MixerRowStyle.TwoLine)]
    [InlineData(320, MixerRowStyle.TwoLine)]
    [InlineData(360, MixerRowStyle.OneLine)]
    [InlineData(800, MixerRowStyle.OneLine)]
    public void S1227_row_style_by_width(double width, MixerRowStyle expected)
        => Assert.Equal(expected, MixerRules.RowStyle(width));

    private static MixerRow Row(string key, string name, bool active, bool system = false)
        => new(key, name, null, 0.5, false, active, system, 1);

    [Fact]
    public void S1228_quiet_rows_are_what_only_active_left_out()
    {
        MixerRow[] rows =
        [
            Row("a", "Alpha", active: true),
            Row("b", "Beta", active: false),
            Row(MixerRules.SystemSoundsKey, string.Empty, active: false, system: true),
            Row("c", "Gamma", active: false),
        ];

        Assert.Equal(["b", "c"], MixerRules.QuietRows(rows, new MixerRowOptions(true, OnlyActive: true)).Select(r => r.Key));
        Assert.Empty(MixerRules.QuietRows(rows, new MixerRowOptions(true, OnlyActive: false)));
    }

    // ---- [S-2732] the wheel ----------------------------------------------------------------------

    [Fact]
    public void S2732_a_wheel_notch_is_one_arrow_step()
    {
        Assert.Equal((2, 0), AudioRules.Wheel(0, 120));
        Assert.Equal((-2, 0), AudioRules.Wheel(0, -120));
        Assert.Equal((6, 0), AudioRules.Wheel(0, 360));
    }

    [Fact]
    public void S2732_a_precision_wheel_steps_once_its_small_deltas_add_up_to_a_notch()
    {
        var (step, carry) = AudioRules.Wheel(0, 40);
        Assert.Equal((0, 40), (step, carry));

        (step, carry) = AudioRules.Wheel(carry, 40);
        Assert.Equal((0, 80), (step, carry));

        (step, carry) = AudioRules.Wheel(carry, 50);
        Assert.Equal((2, 10), (step, carry));
    }

    [Fact]
    public void S2732_turning_the_wheel_back_drops_what_was_carried_the_other_way()
    {
        // Half a notch up and then a notch down is a step down, not nothing.
        Assert.Equal((-2, 0), AudioRules.Wheel(60, -120));
    }
}
