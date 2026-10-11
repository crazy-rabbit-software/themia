using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The now-playing arithmetic. [S-250] [S-251] [S-252] [S-253]
/// <para>
/// These are the parts that need no media session to exercise and are the easiest to get quietly
/// wrong: a player that reports a non-zero start offset, a live stream with no duration, a position
/// anchored before the widget was even on screen.
/// </para>
/// </summary>
public class MusicRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 14, 0, 0, TimeSpan.Zero);

    private static TimeSpan Sec(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void S250_A_timeline_with_no_offset_passes_through_unchanged()
    {
        var timeline = MusicRules.Normalise(TimeSpan.Zero, Sec(30), Sec(210));

        Assert.Equal(Sec(30), timeline.Position);
        Assert.Equal(Sec(210), timeline.Duration);
    }

    [Fact]
    public void S250_A_player_that_reports_a_start_offset_is_folded_back_to_zero()
    {
        // A chapter inside a longer file: position and end are both measured from the same origin,
        // so taking position at face value puts the bar most of the way along a track that has
        // barely started.
        var timeline = MusicRules.Normalise(Sec(600), Sec(630), Sec(900));

        Assert.Equal(Sec(30), timeline.Position);
        Assert.Equal(Sec(300), timeline.Duration);
    }

    [Fact]
    public void S250_A_nonsense_timeline_cannot_produce_a_negative_bar()
    {
        // An end before the start, and a position before the start. Both are seen from players
        // mid-transition between tracks.
        var backwards = MusicRules.Normalise(Sec(100), Sec(50), Sec(40));

        Assert.Equal(TimeSpan.Zero, backwards.Position);
        Assert.Equal(TimeSpan.Zero, backwards.Duration);
    }

    [Fact]
    public void S250_A_position_past_the_end_is_clamped_into_the_track()
    {
        var timeline = MusicRules.Normalise(TimeSpan.Zero, Sec(400), Sec(210));

        Assert.Equal(Sec(210), timeline.Position);
    }

    [Fact]
    public void S251_A_playing_track_advances_by_the_time_that_has_passed()
    {
        var timeline = new TrackTimeline(Sec(30), Sec(210));

        var at = MusicRules.Interpolate(timeline, Now, Now.AddSeconds(7), playing: true);

        Assert.Equal(Sec(37), at);
    }

    [Fact]
    public void S251_A_paused_track_does_not_move()
    {
        var timeline = new TrackTimeline(Sec(30), Sec(210));

        var at = MusicRules.Interpolate(timeline, Now, Now.AddMinutes(5), playing: false);

        Assert.Equal(Sec(30), at);
    }

    [Fact]
    public void S251_Interpolation_never_runs_past_the_end_of_the_track()
    {
        // A session that stops pushing at the end of a track would otherwise count upwards forever.
        var timeline = new TrackTimeline(Sec(200), Sec(210));

        var at = MusicRules.Interpolate(timeline, Now, Now.AddHours(1), playing: true);

        Assert.Equal(Sec(210), at);
    }

    [Fact]
    public void S251_An_anchor_in_the_future_is_not_run_backwards()
    {
        // Clock skew between the session's timestamp and this machine's. Rewinding the readout
        // reads as the track jumping back.
        var timeline = new TrackTimeline(Sec(30), Sec(210));

        var at = MusicRules.Interpolate(timeline, Now.AddSeconds(5), Now, playing: true);

        Assert.Equal(Sec(30), at);
    }

    [Fact]
    public void S251_A_stream_with_no_duration_still_counts_up()
    {
        // A live stream reports no end. The elapsed time is the only honest number it has.
        var timeline = new TrackTimeline(Sec(30), TimeSpan.Zero);

        Assert.Equal(Sec(90), MusicRules.Interpolate(timeline, Now, Now.AddSeconds(60), playing: true));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(7, "0:07")]
    [InlineData(67, "1:07")]
    [InlineData(600, "10:00")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3750, "1:02:30")]
    public void S252_A_time_is_written_the_way_every_player_writes_it(int seconds, string expected)
        => Assert.Equal(expected, MusicRules.Clock(Sec(seconds)));

    [Fact]
    public void S252_A_negative_or_absent_time_reads_as_the_start()
        => Assert.Equal("0:00", MusicRules.Clock(Sec(-5)));

    [Fact]
    public void S252_A_track_with_no_duration_has_no_progress_rather_than_a_full_bar()
    {
        Assert.Equal(0, MusicRules.Progress(Sec(30), TimeSpan.Zero));
        Assert.Equal(0.5, MusicRules.Progress(Sec(105), Sec(210)));
        Assert.Equal(1, MusicRules.Progress(Sec(400), Sec(210)));
    }

    [Fact]
    public void S253_A_track_with_no_title_still_says_something()
        => Assert.Equal("Unknown track", MusicRules.Title("   ", Locale.En));

    [Fact]
    public void S253_A_title_from_another_app_is_collapsed_to_one_line_and_capped()
    {
        // It comes from whatever is playing, which makes it somebody else's string. It is drawn
        // and nothing else — never parsed into a path, never executed.
        Assert.Equal("Track One", MusicRules.Title("Track\r\n   One"));
        Assert.Equal(120, MusicRules.Title(new string('t', 400)).Length);
    }

    [Fact]
    public void S253_The_subtitle_joins_whichever_of_artist_and_album_exist()
    {
        Assert.Equal("Nils Frahm · Spaces", MusicRules.Subtitle("Nils Frahm", "Spaces"));
        Assert.Equal("Nils Frahm", MusicRules.Subtitle("Nils Frahm", null));
        Assert.Equal("Spaces", MusicRules.Subtitle(" ", "Spaces"));
        Assert.Equal(string.Empty, MusicRules.Subtitle(null, null));
    }

    [Fact]
    public void S253_A_single_named_release_is_not_said_twice()
    {
        // Podcasts and singles routinely report the same string as artist and album.
        Assert.Equal("Reply All", MusicRules.Subtitle("Reply All", "reply all"));
    }

    [Fact]
    public void S253_The_track_key_changes_when_any_of_the_three_fields_does()
    {
        var first = MusicRules.TrackKey("A", "B", "C");

        Assert.Equal(first, MusicRules.TrackKey(" A ", "B", "C"));
        Assert.NotEqual(first, MusicRules.TrackKey("A", "B", "D"));

        // The separator must not be forgeable out of the fields themselves, or two different
        // tracks could share a key and keep the wrong album art.
        Assert.NotEqual(MusicRules.TrackKey("A B", string.Empty, "C"), MusicRules.TrackKey("A", "B", "C"));
    }

    [Fact]
    public void S254_Every_idle_state_says_which_idle_state_it_is()
    {
        Assert.Equal("Nothing is playing on this PC", MusicRules.StatusMessage(MusicState.NoSession, Locale.En));
        Assert.Equal("Nothing playing", MusicRules.StatusMessage(MusicState.Idle, Locale.En));
        Assert.Equal("Reading the media session", MusicRules.StatusMessage(MusicState.Loading, Locale.En));
        Assert.Equal(string.Empty, MusicRules.StatusMessage(MusicState.Playing, Locale.En));
    }

    [Fact]
    public void S254_the_idle_states_speak_the_language()
    {
        Assert.Equal("Auf diesem PC wird nichts wiedergegeben", MusicRules.StatusMessage(MusicState.NoSession, Locale.De));
        Assert.Equal("Keine Wiedergabe", MusicRules.StatusMessage(MusicState.Idle, Locale.De));
    }

    [Fact]
    public void S253_a_missing_title_is_unknown_in_the_language()
    {
        Assert.Equal("Unknown track", MusicRules.Title(null, Locale.En));
        Assert.Equal("Unbekannter Titel", MusicRules.Title("  ", Locale.De));
        Assert.Equal("Track", MusicRules.Title("Track", Locale.De));
    }
}
