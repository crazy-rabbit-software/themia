using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The intro animations ([S-1000..S-1012]): the per-moment resolver, arming, the cascade, every
/// style's keyframes, the sheets, the hyperspace warp and star field, and the hacker tiles. Ports
/// of the original's <c>introSheets.test.ts</c>, <c>hyperspace.test.ts</c> and <c>hacker.test.ts</c>
/// where they pinned a rule that still applies.
/// </summary>
public sealed class MotionTests
{
    private static readonly IntroAnimation[] Styles = Enum.GetValues<IntroAnimation>();

    // -----------------------------------------------------------------------------------------
    // S-1000 moments
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1000_each_moment_reads_its_own_style_and_length()
    {
        var design = new DesignState
        {
            IntroAnimation = IntroAnimation.Fade,
            IntroAnimationMs = 300,
            TransitionAnimation = IntroAnimation.Flip,
            TransitionAnimationMs = 700,
            UnlockAnimation = IntroAnimation.Hyperspace,
            UnlockAnimationMs = 1200,
        };

        Assert.Equal(IntroAnimation.Fade, IntroMotion.StyleFor(design, IntroMode.Startup));
        Assert.Equal(IntroAnimation.Flip, IntroMotion.StyleFor(design, IntroMode.Transition));
        Assert.Equal(IntroAnimation.Hyperspace, IntroMotion.StyleFor(design, IntroMode.Unlock));
        Assert.Equal(300, IntroMotion.MsFor(design, IntroMode.Startup));
        Assert.Equal(700, IntroMotion.MsFor(design, IntroMode.Transition));
        Assert.Equal(1200, IntroMotion.MsFor(design, IntroMode.Unlock));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-5, 100)]
    [InlineData(99999, 5000)]
    [InlineData(double.NaN, 500)]
    [InlineData(double.PositiveInfinity, 500)]
    [InlineData(2500, 2500)]
    public void S1000_a_length_is_clamped_to_the_offered_range(double raw, double expected)
        => Assert.Equal(expected, IntroMotion.ClampMs(raw));

    // -----------------------------------------------------------------------------------------
    // S-1001 arming
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1001_the_first_layout_plays_startup_and_only_once()
    {
        var arming = new IntroArming();

        Assert.Equal(IntroMode.Startup, arming.OnLayout("a"));
        Assert.Null(arming.OnLayout("a"));
        Assert.Null(arming.OnLayout("a"));
    }

    [Fact]
    public void S1001_a_real_view_change_plays_the_transition_and_nothing_else_does()
    {
        var arming = new IntroArming();
        arming.OnLayout("a");

        Assert.Equal(IntroMode.Transition, arming.OnLayout("b"));
        Assert.Null(arming.OnLayout("b")); // a rename, a widget added, a resume: same view, silent
        Assert.Equal(IntroMode.Transition, arming.OnLayout("a"));
    }

    [Fact]
    public void S1001_startup_wins_when_the_first_layout_already_shows_another_view()
    {
        var arming = new IntroArming();
        Assert.Equal(IntroMode.Startup, arming.OnLayout("b"));
        Assert.Null(arming.OnLayout("b"));
    }

    // -----------------------------------------------------------------------------------------
    // S-1017 a show never leaves a card mid-flight
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1017_a_layout_pass_after_the_one_that_started_a_show_ends_it()
    {
        // Seen live 2026-11-15: a start whose intro never reported its end kept every card hidden
        // through the later passes (a metrics change 12 s on); the next pass must end it at rest.
        var arming = new IntroArming();
        Assert.False(arming.BeginPass());

        arming.OnLayout("a");
        arming.ShowStarted();
        Assert.True(arming.BeginPass());
        Assert.False(arming.BeginPass());
    }

    [Fact]
    public void S1017_a_show_that_ended_on_its_own_leaves_nothing_for_the_next_pass()
    {
        var arming = new IntroArming();
        arming.OnLayout("a");
        arming.ShowStarted();
        arming.ShowEnded();

        Assert.False(arming.BeginPass());
    }

    [Fact]
    public void S1017_every_style_ends_on_the_rest_pose_a_stopped_show_snaps_to()
    {
        Assert.Equal(new IntroPose(1, 1, 0, 0, 0), IntroPose.Rest);
        foreach (var style in Styles)
        {
            if (IntroTracks.For(style) is { } tracks)
            {
                Assert.Equal(IntroPose.Rest, tracks.At(1, (120, -80)));
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // S-1002 cascade, S-1010 reduced motion
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1002_the_cascade_steps_45ms_and_caps_at_540ms_when_the_length_has_room()
    {
        var few = IntroSchedule.For(IntroAnimation.Scale, IntroMode.Startup, 3000, 5);
        Assert.Equal(0, few.DelayMs(0));
        Assert.Equal(45, few.DelayMs(1), 6);
        Assert.Equal(180, few.DelayMs(4), 6);

        var many = IntroSchedule.For(IntroAnimation.Scale, IntroMode.Startup, 3000, 40);
        Assert.Equal(540, many.DelayMs(39), 6);
        Assert.Equal(0, many.DelayMs(-3));
    }

    [Theory]
    [InlineData(IntroAnimation.Sides)]
    [InlineData(IntroAnimation.TopBottom)]
    public void S1004_sheets_take_no_cascade(IntroAnimation style)
    {
        var schedule = IntroSchedule.For(style, IntroMode.Startup, 500, 20);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(0, schedule.DelayMs(i));
        }

        Assert.Equal(500, schedule.WidgetMs);
    }

    // -----------------------------------------------------------------------------------------
    // S-1760 the configured length is the whole show
    // -----------------------------------------------------------------------------------------

    public static TheoryData<IntroAnimation, IntroMode> EveryShow()
    {
        var data = new TheoryData<IntroAnimation, IntroMode>();
        foreach (var style in Styles.Where(s => s != IntroAnimation.None))
        {
            foreach (var mode in Enum.GetValues<IntroMode>())
            {
                data.Add(style, mode);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryShow))]
    public void S1760_the_whole_show_ends_exactly_at_the_configured_length_for_any_number_of_widgets(IntroAnimation style, IntroMode mode)
    {
        foreach (var ms in new[] { 100.0, 250, 500, 1000, 3000, 5000 })
        {
            foreach (var widgets in new[] { 1, 2, 3, 12, 40, 200 })
            {
                var schedule = IntroSchedule.For(style, mode, ms, widgets);

                for (var i = 0; i < widgets; i++)
                {
                    Assert.True(schedule.EndMs(i) <= ms + 1e-6, $"{style}/{mode} {ms} ms, widget {i} of {widgets} ends at {schedule.EndMs(i)}");
                    Assert.True(schedule.Tile(i).EndMs <= ms + 1e-6, $"{style}/{mode} {ms} ms, tile {i} of {widgets} ends at {schedule.Tile(i).EndMs}");
                    Assert.True(schedule.DelayMs(i) >= 0);
                }

                // Not shorter either: the last widget comes to rest on the length, not before it.
                Assert.Equal(ms, schedule.EndMs(widgets - 1), 6);
                Assert.Equal(ms, schedule.LastEndMs, 6);

                // The cascade never eats more than its share of the widgets' time.
                Assert.True(schedule.WidgetMs >= ((ms - schedule.BaseDelayMs) * (1 - IntroSchedule.CascadeShare)) - 1e-6);
            }
        }
    }

    [Fact]
    public void S1760_twenty_widgets_at_500ms_take_500ms_not_over_a_second()
    {
        // Regression: each widget used to take the whole 500 ms after a cascade of up to 540 ms, so
        // twenty widgets took 1040 ms; under the hyperspace overlay the show took 4640 ms.
        var cascade = IntroSchedule.For(IntroAnimation.Scale, IntroMode.Startup, 500, 20);
        Assert.Equal(500, cascade.EndMs(19), 6);
        Assert.Equal(200, cascade.DelayMs(19), 6);
        Assert.Equal(300, cascade.WidgetMs, 6);

        var warp = IntroSchedule.For(IntroAnimation.Hyperspace, IntroMode.Startup, 500, 20);
        Assert.Equal(500, warp.TotalMs);
        Assert.Equal(500, warp.EndMs(19), 6);
    }

    [Fact]
    public void S1760_the_overlay_timeline_is_scaled_as_one_piece()
    {
        var schedule = IntroSchedule.For(IntroAnimation.Hyperspace, IntroMode.Unlock, 900, 4);
        Assert.True(schedule.Overlay);
        Assert.Equal(900 / Hyperspace.NaturalMs, schedule.TimeScale, 9);
        Assert.Equal(schedule.Scaled(Hyperspace.JumpMs), schedule.BaseDelayMs, 9);
        Assert.Equal(900, schedule.Scaled(Hyperspace.NaturalMs), 9);

        // The shake and the veil end inside the show.
        Assert.True(schedule.Scaled(Hyperspace.StaticMs + Hyperspace.ShakeMs) <= 900 + 1e-6);
        Assert.True(schedule.Scaled(Hyperspace.JumpMs + Hyperspace.DecelMs) <= 900 + 1e-6);

        // No overlay on a view change, and no scaling without one.
        var change = IntroSchedule.For(IntroAnimation.Hyperspace, IntroMode.Transition, 900, 4);
        Assert.False(change.Overlay);
        Assert.Equal(1, change.TimeScale);
        Assert.Equal(0, change.BaseDelayMs);
    }

    [Fact]
    public void S1760_an_unplayable_length_is_clamped_first()
    {
        Assert.Equal(IntroMotion.MaxMs, IntroSchedule.For(IntroAnimation.Fade, IntroMode.Startup, 99_999, 3).TotalMs);
        Assert.Equal(IntroMotion.DefaultMs, IntroSchedule.For(IntroAnimation.Fade, IntroMode.Startup, double.NaN, 3).TotalMs);
        Assert.Equal(0, IntroSchedule.For(IntroAnimation.Fade, IntroMode.Startup, 500, 0).LastEndMs);
    }

    [Fact]
    public void S1010_nothing_plays_for_none_or_with_windows_animation_effects_off()
    {
        Assert.False(IntroMotion.Plays(IntroAnimation.None, animationsEnabled: true));
        Assert.False(IntroMotion.Plays((IntroAnimation)42, animationsEnabled: true));

        foreach (var style in Styles)
        {
            Assert.False(IntroMotion.Plays(style, animationsEnabled: false));
        }

        Assert.True(IntroMotion.Plays(IntroAnimation.Scale, animationsEnabled: true));
    }

    // -----------------------------------------------------------------------------------------
    // S-1003 keyframes
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1003_every_style_with_keyframes_comes_to_rest_at_the_identity()
    {
        foreach (var style in Styles)
        {
            if (IntroTracks.For(style) is not { } tracks)
            {
                continue;
            }

            var pose = tracks.At(1, (-300, 120));
            Assert.Equal(1, pose.Opacity, 6);
            Assert.Equal(1, pose.Scale, 6);
            Assert.Equal(0, pose.X, 6);
            Assert.Equal(0, pose.Y, 6);
            Assert.Equal(0, pose.Rotation, 6);
        }
    }

    [Fact]
    public void S1003_none_and_hacker_have_no_widget_keyframes()
    {
        Assert.Null(IntroTracks.For(IntroAnimation.None));
        Assert.Null(IntroTracks.For(IntroAnimation.Hacker));

        foreach (var style in Styles.Where(s => s is not IntroAnimation.None and not IntroAnimation.Hacker))
        {
            Assert.NotNull(IntroTracks.For(style));
        }
    }

    [Fact]
    public void S1003_every_track_starts_at_zero_ends_at_one_and_runs_forward()
    {
        foreach (var style in Styles)
        {
            if (IntroTracks.For(style) is not { } t)
            {
                continue;
            }

            foreach (var ats in new[] { t.Opacity.Select(k => k.At), t.Scale.Select(k => k.At), t.Move.Select(k => k.At), t.Rotation.Select(k => k.At) })
            {
                var list = ats.ToList();
                if (list.Count == 0)
                {
                    continue;
                }

                Assert.Equal(0, list[0]);
                Assert.Equal(1, list[^1]);
                Assert.True(list.Zip(list.Skip(1)).All(p => p.Second > p.First), $"{style} keys out of order");
            }
        }
    }

    [Fact]
    public void S1003_the_originals_start_poses()
    {
        var scale = IntroTracks.For(IntroAnimation.Scale)!.At(0, (0, 0));
        Assert.Equal(0, scale.Opacity);
        Assert.Equal(0.94, scale.Scale, 6);
        Assert.Equal(16, scale.Y);

        Assert.Equal(40, IntroTracks.For(IntroAnimation.Slide)!.At(0, (0, 0)).Y);

        var flip = IntroTracks.For(IntroAnimation.Flip)!;
        Assert.Equal(IntroAxis.Tilt, flip.Axis);
        Assert.True(flip.Perspective);
        Assert.Equal(-32, flip.At(0, (0, 0)).Rotation);
    }

    [Fact]
    public void S1003_the_settle_curve_is_fast_out_and_monotonic()
    {
        var previous = 0.0;
        for (var i = 1; i <= 20; i++)
        {
            var v = IntroTracks.Eased(MotionEase.Settle, i / 20.0);
            Assert.True(v >= previous);
            previous = v;
        }

        // cubic-bezier(0.22, 1, 0.36, 1) is three quarters of the way there a quarter of the way in.
        Assert.Equal(0.765, IntroTracks.Eased(MotionEase.Settle, 0.25), 3);
        Assert.Equal(1, IntroTracks.Eased(MotionEase.Settle, 1), 6);

        // The crash is still slow early and fast at the end: it never brakes.
        Assert.True(IntroTracks.Eased(MotionEase.Crash, 0.5) < 0.5);
    }

    // -----------------------------------------------------------------------------------------
    // S-1004 sheets (port of introSheets.test.ts)
    // -----------------------------------------------------------------------------------------

    private const double Extent = 1920;

    private static FracRect At(SheetAxis axis, double start, double size)
        => axis == SheetAxis.X ? new FracRect(start, 0, size, 1) : new FracRect(0, start, 1, size);

    [Fact]
    public void S1004_only_the_two_sheet_styles_have_an_axis()
    {
        Assert.Equal(SheetAxis.X, IntroSheets.AxisFor(IntroAnimation.Sides));
        Assert.Equal(SheetAxis.Y, IntroSheets.AxisFor(IntroAnimation.TopBottom));

        foreach (var style in Styles.Where(s => s is not IntroAnimation.Sides and not IntroAnimation.TopBottom))
        {
            Assert.Null(IntroSheets.AxisFor(style));
        }
    }

    [Theory]
    [InlineData(SheetAxis.X)]
    [InlineData(SheetAxis.Y)]
    public void S1004_direction_follows_the_widgets_centre(SheetAxis axis)
    {
        Assert.Equal(-1, IntroSheets.Direction(At(axis, 0.05, 0.2), axis));
        Assert.Equal(1, IntroSheets.Direction(At(axis, 0.7, 0.25), axis));
        Assert.Equal(-1, IntroSheets.Direction(At(axis, 0.3, 0.3), axis));   // straddles, mostly near
        Assert.Equal(1, IntroSheets.Direction(At(axis, 0.45, 0.5), axis));   // straddles, mostly far
        Assert.Equal(1, IntroSheets.Direction(At(axis, 0.25, 0.5), axis));   // dead centre goes far
        Assert.Equal(1, IntroSheets.Direction(At(axis, 0, 1), axis));
    }

    [Fact]
    public void S1004_each_axis_is_judged_on_its_own()
    {
        var r = new FracRect(0.05, 0.8, 0.2, 0.15);
        Assert.Equal(-1, IntroSheets.Direction(r, SheetAxis.X));
        Assert.Equal(1, IntroSheets.Direction(r, SheetAxis.Y));
    }

    [Theory]
    [InlineData(SheetAxis.X)]
    [InlineData(SheetAxis.Y)]
    public void S1004_each_sheet_moves_as_one_block_sized_by_its_deepest_member(SheetAxis axis)
    {
        var view = new[] { At(axis, 0.02, 0.18), At(axis, 0.24, 0.20), At(axis, 0.55, 0.15), At(axis, 0.78, 0.20) };
        var sheets = IntroSheets.Offsets(view, axis, Extent);

        // Near: deepest far edge 0.44 → -(0.44·1920 + 24). Far: shallowest near edge 0.55 → (0.45·1920 + 24).
        Assert.Equal(-Math.Round((0.44 * Extent) + IntroSheets.MarginPx), sheets.Near);
        Assert.Equal(Math.Round((0.45 * Extent) + IntroSheets.MarginPx), sheets.Far);

        var starts = view.Select(r => IntroSheets.StartFor(r, axis, sheets)).Select(p => axis == SheetAxis.X ? p.X : p.Y).ToArray();
        Assert.Equal(starts[0], starts[1]);
        Assert.Equal(starts[2], starts[3]);

        // Every member is fully off-screen at the start.
        foreach (var r in view)
        {
            var (s, size) = axis == SheetAxis.X ? (r.X, r.W) : (r.Y, r.H);
            var (sx, sy) = IntroSheets.StartFor(r, axis, sheets);
            var offset = axis == SheetAxis.X ? sx : sy;
            var a = (s * Extent) + offset;
            Assert.True(a + (size * Extent) <= 0 || a >= Extent);
        }
    }

    [Fact]
    public void S1004_an_empty_sheet_gets_zero_and_the_other_axis_is_untouched()
    {
        var sheets = IntroSheets.Offsets([new FracRect(0.6, 0.1, 0.3, 0.3)], SheetAxis.X, Extent);
        Assert.Equal(0, sheets.Near);
        Assert.True(sheets.Far > 0);
        Assert.Equal(0, IntroSheets.StartFor(new FracRect(0.6, 0.1, 0.3, 0.3), SheetAxis.X, sheets).Y);

        Assert.Equal(new SheetOffsets(0, 0), IntroSheets.Offsets([], SheetAxis.Y, Extent));
        Assert.Equal(new SheetOffsets(0, 24), IntroSheets.Offsets([new FracRect(0.9, 0, 0.1, 1)], SheetAxis.X, double.NaN));
    }

    // -----------------------------------------------------------------------------------------
    // S-1005 / S-1006 hyperspace (port of hyperspace.test.ts)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1005_the_phases_add_up_like_the_original()
    {
        Assert.Equal(3300, Hyperspace.JumpMs);
        Assert.Equal(3300, Hyperspace.ShakeMs);
        Assert.Equal(3300 + 900 + 300, Hyperspace.NaturalMs);
    }

    [Fact]
    public void S1005_the_warp_is_static_teases_snaps_cruises_and_drops()
    {
        Assert.Equal(0, Hyperspace.Warp(0));
        Assert.Equal(0, Hyperspace.Warp(Hyperspace.StaticMs));

        // The tease never passes its cap.
        var teaseEnd = Hyperspace.StaticMs + Hyperspace.AccelMs - Hyperspace.SnapMs;
        Assert.InRange(Hyperspace.Warp(teaseEnd - 1), 0, Hyperspace.TeasePeak);
        Assert.True(Hyperspace.Warp(Hyperspace.StaticMs + 400) < 0.02);

        // The snap reaches full warp by the end of acceleration, without a kink at the hand-off.
        Assert.Equal(Hyperspace.TeasePeak, Hyperspace.Warp(teaseEnd), 3);
        Assert.Equal(1, Hyperspace.Warp(Hyperspace.StaticMs + Hyperspace.AccelMs), 6);
        Assert.Equal(1, Hyperspace.Warp(Hyperspace.JumpMs));

        // Sharp drop: an eighth left a third of the way down, nothing after decel.
        Assert.Equal(Math.Pow(2.0 / 3, 3), Hyperspace.Warp(Hyperspace.JumpMs + 300), 6);
        Assert.Equal(0, Hyperspace.Warp(Hyperspace.JumpMs + Hyperspace.DecelMs));
        Assert.Equal(0, Hyperspace.Warp(99_999));
        Assert.Equal(0, Hyperspace.Warp(double.NaN));
    }

    [Fact]
    public void S1005_travel_is_the_running_integral_of_the_warp()
    {
        var samples = Hyperspace.Samples(50, Hyperspace.JumpMs + Hyperspace.DecelMs);

        Assert.Equal(0, samples[0].Travel);
        Assert.Equal(Hyperspace.JumpMs + Hyperspace.DecelMs, samples[^1].Ms);
        Assert.True(samples.Zip(samples.Skip(1)).All(p => p.Second.Travel >= p.First.Travel));

        // Nothing moves while static; cruise adds one full frame of travel per frame.
        Assert.Equal(0, samples.First(s => s.Ms >= Hyperspace.StaticMs).Travel);
        var cruiseStart = samples.First(s => s.Ms >= Hyperspace.StaticMs + Hyperspace.AccelMs);
        var cruiseEnd = samples.Last(s => s.Ms <= Hyperspace.JumpMs);
        Assert.Equal((cruiseEnd.Ms - cruiseStart.Ms) / Hyperspace.FrameMs, cruiseEnd.Travel - cruiseStart.Travel, 3);
    }

    [Fact]
    public void S1005_the_field_reaches_the_farthest_corner_from_the_lowered_core()
    {
        // 1920×1080: core at (960, 734.4); the farthest corner is a top one.
        Assert.Equal(Math.Sqrt((960.0 * 960) + (734.4 * 734.4)), Hyperspace.Radius(1920, 1080), 6);
    }

    [Fact]
    public void S1005_stars_are_deterministic_stay_in_the_field_and_wrap_to_a_new_ray()
    {
        const double R = 1200;
        var a = Hyperspace.Stars(Hyperspace.StarCount, 7, R);
        var b = Hyperspace.Stars(Hyperspace.StarCount, 7, R);
        Assert.Equal(a, b);
        Assert.Equal(100, a.Length);

        foreach (var star in a)
        {
            Assert.InRange(star.Speed, 0.4, 1.3);
            Assert.InRange(star.Hue, 200, 240);

            // Where it starts is where it was born, and nothing moves without travel.
            Assert.InRange(Hyperspace.Distance(star, 0, R), 0, (0.9 * R) + 1e-6);
            Assert.Equal(star.Angle, Hyperspace.Angle(star, 0, R));

            for (var travel = 0.0; travel < 400; travel += 3.7)
            {
                Assert.InRange(Hyperspace.Distance(star, travel, R), -1e-6, (Hyperspace.EdgeFraction * R) + 1e-6);
            }
        }

        // A star that has crossed the field once is on its next ray.
        var s = a[0];
        var (_, span) = Hyperspace.LogWindow(R);
        var once = (span - (s.Log0 - Hyperspace.LogWindow(R).Low) + 0.01) / s.Rate;
        Assert.Equal(1, Hyperspace.Lives(s, once, R));
        Assert.Equal(s.Angle + Hyperspace.RebirthTurn, Hyperspace.Angle(s, once, R), 9);
        Assert.True(Hyperspace.Distance(s, once, R) < 0.01 * R);
    }

    [Fact]
    public void S1005_a_star_speeds_up_as_it_goes_deeper()
    {
        var star = new Star(0, Math.Log(0.15 * 1000), 0.2, 1, 0, 220);
        var d1 = Hyperspace.Distance(star, 1, 1000) - Hyperspace.Distance(star, 0, 1000);
        var d2 = Hyperspace.Distance(star, 6, 1000) - Hyperspace.Distance(star, 5, 1000);
        Assert.True(d2 > d1);
    }

    [Fact]
    public void S1005_streaks_grow_with_the_warp_and_a_static_star_is_a_dot()
    {
        var star = new Star(0, 0, 0.1, 1, 0, 220);
        Assert.Equal(0.7, Hyperspace.Trail(star, 0, 0.5));
        Assert.True(Hyperspace.Trail(star, 1, 0.5) > 500);
        Assert.InRange(Hyperspace.Alpha(star, 1, 1, 0), 0, 1);

        // A streak is cut at the core, so it never reaches through to the far side.
        Assert.Equal(50, Hyperspace.Streak(star, 1, 50, 1000));
        Assert.Equal(0.7, Hyperspace.Streak(star, 1, 0.1, 1000));
        Assert.Equal(0.7, Hyperspace.Streak(star, 0, 400, 1000));
        Assert.True(Hyperspace.Thickness(1, 1) > Hyperspace.Thickness(0, 0));
    }

    [Fact]
    public void S1006_widgets_warp_in_from_the_lowered_core()
    {
        // A widget centred at (0.25, 0.25) of 1920×1080 flies in from (+480, +464.4) away.
        var (dx, dy) = Hyperspace.FromOffset(new FracRect(0.15, 0.15, 0.2, 0.2), 1920, 1080);
        Assert.Equal(480, dx);
        Assert.Equal(Math.Round((1080 * 0.68) - 270), dy);

        var tracks = IntroTracks.For(IntroAnimation.Hyperspace)!;
        var start = tracks.At(0, (dx, dy));
        Assert.Equal(0.02, start.Scale, 6);
        Assert.Equal(dx, start.X);
        Assert.Equal(dy, start.Y);

        // The impact: in its slot, over-scaled, at 38%.
        var impact = tracks.At(0.38, (dx, dy));
        Assert.Equal(1.05, impact.Scale, 6);
        Assert.Equal(0, impact.X, 6);
    }

    [Fact]
    public void S1006_the_shake_is_still_at_both_ends_and_hardest_at_the_jump()
    {
        var shake = Hyperspace.Shake;
        Assert.Equal(new ShakeKey(0, 0, 0, 0), shake[0]);
        Assert.Equal(new ShakeKey(1, 0, 0, 0), shake[^1]);
        Assert.True(shake.Zip(shake.Skip(1)).All(p => p.Second.At > p.First.At));

        var hardest = shake.MaxBy(k => Math.Abs(k.X));
        Assert.Equal(0.73, hardest.At);

        // Near-still through the tease.
        Assert.All(shake.Where(k => k.At <= 0.41), k => Assert.True(Math.Abs(k.X) < 1));
    }

    [Theory]
    [InlineData(IntroAnimation.Hyperspace, IntroMode.Startup, true)]
    [InlineData(IntroAnimation.Hyperspace, IntroMode.Unlock, true)]
    [InlineData(IntroAnimation.Hyperspace, IntroMode.Transition, false)]
    [InlineData(IntroAnimation.Hacker, IntroMode.Startup, false)]
    [InlineData(IntroAnimation.Scale, IntroMode.Startup, false)]
    public void S1005_only_hyperspace_brings_the_overlay_and_never_on_a_view_change(IntroAnimation style, IntroMode mode, bool overlay)
    {
        Assert.Equal(overlay, IntroMotion.PlaysOverlay(style, mode));

        // Under the overlay every widget waits for the jump, on the overlay's own (scaled) clock.
        var schedule = IntroSchedule.For(style, mode, Hyperspace.NaturalMs, 3);
        Assert.Equal(overlay ? Hyperspace.JumpMs : 0, schedule.BaseDelayMs, 6);
    }

    // -----------------------------------------------------------------------------------------
    // S-1007 hacker (port of hacker.test.ts)
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(3333)]
    public void S1007_a_tile_covers_65_percent_of_its_widget_time_and_the_last_ends_on_the_length(double ms)
    {
        var schedule = IntroSchedule.For(IntroAnimation.Hacker, IntroMode.Startup, ms, 3);
        var first = schedule.Tile(0);
        Assert.Equal(Math.Round(schedule.WidgetMs * 0.65), first.RevealMs);
        Assert.Equal(schedule.WidgetMs, first.EndMs, 6);

        var third = schedule.Tile(2);
        Assert.Equal(first.RevealMs + (2 * schedule.StepMs), third.RevealMs, 6);
        Assert.Equal(first.FadeMs, third.FadeMs);
        Assert.Equal(ms, third.EndMs, 6);
    }

    [Fact]
    public void S1007_neighbouring_tiles_never_share_a_panel_including_the_wrap()
    {
        var n = HackerIntro.Variants.Count;
        for (var i = 0; i < n; i++)
        {
            Assert.NotEqual(HackerIntro.Variants[i].Panel, HackerIntro.Variants[(i + 1) % n].Panel);
        }

        Assert.Equal(HackerIntro.VariantFor(0), HackerIntro.VariantFor(n));
        Assert.Equal(HackerIntro.VariantFor(n - 1), HackerIntro.VariantFor(-1));
        Assert.Equal(5, HackerIntro.Variants.Select(v => v.Panel).Distinct().Count());
    }

    [Fact]
    public void S1007_console_lines_are_deterministic()
    {
        Assert.Equal(HackerIntro.ConsoleLines(3, 12, hex: false), HackerIntro.ConsoleLines(3, 12, hex: false));
        Assert.Equal(12, HackerIntro.ConsoleLines(3, 12, hex: true).Count);
        Assert.Matches("^[0-9A-F]{8}  ([0-9A-F]{2} ){8} .{8}$", HackerIntro.ConsoleLines(9, 1, hex: true)[0]);
    }
}
