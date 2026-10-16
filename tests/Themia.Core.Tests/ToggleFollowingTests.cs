using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// How the quick-setting toggles follow a change made outside Themia: night light's value read as it
/// is and preferred over the handler's stale answer, readings taken in order, and a registry watch
/// that is never left waiting where nothing will signal. [S-3919..S-3921] Nothing here touches the
/// machine; the values are copied byte for byte.
/// </summary>
public class ToggleFollowingTests
{
    /// <summary>Windows 11 25H2 with night light off (copied from Karpo's fixture).</summary>
    private static readonly byte[] Off =
    [
        0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06, 0x9C, 0xD1, 0xE3, 0xD5, 0x06, 0x2A,
        0x2B, 0x0E, 0x10, 0x43, 0x42, 0x01, 0x00, 0xC6, 0x14, 0x81, 0xBE, 0x83, 0xB5, 0xEA, 0xCC, 0xD3,
        0xEE, 0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>The same with night light on: the state's field 0 written, its length two more.</summary>
    private static readonly byte[] On =
    [
        0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06, 0x9C, 0xD1, 0xE3, 0xD5, 0x06, 0x2A,
        0x2B, 0x0E, 0x12, 0x43, 0x42, 0x01, 0x00, 0x10, 0x00, 0xC6, 0x14, 0x81, 0xBE, 0x83, 0xB5, 0xEA,
        0xCC, 0xD3, 0xEE, 0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>Captured read-only from the development PC (Windows 11 26300, 2026-10-17), night light on, with field 10.</summary>
    private static readonly byte[] CapturedOn =
    [
        0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06, 0x8F, 0xBD, 0xCA, 0xD6, 0x06, 0x2A,
        0x2B, 0x0E, 0x15, 0x43, 0x42, 0x01, 0x00, 0x10, 0x00, 0xD0, 0x0A, 0x02, 0xC6, 0x14, 0xDC, 0xB8,
        0xF2, 0xDF, 0x9C, 0xB7, 0xD7, 0xEE, 0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>The captured value with night light off: field 0 gone, the state's length two less.</summary>
    private static readonly byte[] CapturedOff =
    [
        0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06, 0x8F, 0xBD, 0xCA, 0xD6, 0x06, 0x2A,
        0x2B, 0x0E, 0x13, 0x43, 0x42, 0x01, 0x00, 0xD0, 0x0A, 0x02, 0xC6, 0x14, 0xDC, 0xB8,
        0xF2, 0xDF, 0x9C, 0xB7, 0xD7, 0xEE, 0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    // ---- night light's value -------------------------------------------------------------------

    [Fact]
    public void S3919_the_value_says_whether_night_light_is_on()
    {
        Assert.False(NightLightValue.Read(Off)!.Value.On);
        Assert.True(NightLightValue.Read(On)!.Value.On);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790503068), DateTimeOffset.FromUnixTimeSeconds((long)NightLightValue.Read(On)!.Value.Written));
    }

    [Fact]
    public void S3919_a_value_with_field_ten_reads_both_ways()
    {
        var on = NightLightValue.Read(CapturedOn)!.Value;
        var off = NightLightValue.Read(CapturedOff)!.Value;
        Assert.True(on.On);
        Assert.False(off.On);
        Assert.Equal(2ul, on.Ten);
        Assert.Equal(2ul, off.Ten);
        Assert.Equal(on.Changed, off.Changed);
    }

    /// <summary>Anything not exactly the shape Windows writes is not read: unknown, never "off".</summary>
    [Theory]
    [InlineData("")]
    [InlineData("43 42 01 00")]
    [InlineData("43 42 02 00 0A 02 01 00 2A 06 01 2A 2B 0E 08 43 42 01 00 C6 14 01 00 00 00 00")]
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 09 43 42 01 00 C6 14 01 00 00 00 00")]
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 08 43 42 01 00 C6 14 01 00 00 00")]
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 08 43 42 01 00 C6 14 01 00 00 00 00 00")]
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 0A 43 42 01 00 02 01 C6 14 01 00 00 00 00")]
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 0A 43 42 01 00 D0 0A FF C6 14 01 00 00 00 00")]
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 FF FF FF FF FF FF FF FF FF FF 2A 2B 0E 08 43 42 01 00 C6 14 01 00 00 00 00")]
    public void S3919_what_is_not_the_shape_reads_as_nothing(string hex)
    {
        var value = hex.Length == 0 ? [] : Convert.FromHexString(hex.Replace(" ", "", StringComparison.Ordinal));
        Assert.Null(NightLightValue.Read(value));
    }

    [Fact]
    public void S3919_the_smallest_value_of_the_shape_reads() =>
        Assert.False(NightLightValue.Read(Convert.FromHexString("434201000A0201002A06012A2B0E0843420100C6140100000000"))!.Value.On);

    /// <summary>
    /// The reported bug: switched off in Windows' panel (or by the schedule), the value said off and
    /// the watch fired, but the tile asked the Display handler, whose item still said on.
    /// </summary>
    [Fact]
    public void S3919_the_value_wins_over_a_stale_handler()
    {
        var follow = new NightLightFollow();
        Assert.False(follow.Shown(NightLightValue.Read(Off), handler: true));
        Assert.True(follow.Shown(NightLightValue.Read(On), handler: false));
    }

    [Fact]
    public void S3919_the_handler_answers_only_where_the_value_does_not_read()
    {
        var follow = new NightLightFollow();
        Assert.True(follow.Shown(null, handler: true));
        Assert.Null(follow.Shown(null, handler: null));
    }

    /// <summary>Themia's own switch: the handler writes the value on its own time, so the old value does not flick the tile back.</summary>
    [Fact]
    public void S3919_an_own_switch_shows_where_it_went_until_the_value_moves()
    {
        var follow = new NightLightFollow();
        var before = NightLightValue.Read(Off);
        follow.Switched(true, before);

        Assert.True(follow.Shown(before, handler: false));
        Assert.True(follow.Shown(NightLightValue.Read(On), handler: false));

        // Once the value has moved, it stands again: switched off elsewhere, it shows off.
        Assert.False(follow.Shown(NightLightValue.Read(Off), handler: true));
    }

    /// <summary>A change made elsewhere right after Themia's own switch is a new value, and it stands.</summary>
    [Fact]
    public void S3919_a_change_elsewhere_after_an_own_switch_stands()
    {
        var follow = new NightLightFollow();
        follow.Switched(false, NightLightValue.Read(CapturedOn));

        Assert.True(follow.Shown(NightLightValue.Read(On), handler: false));
    }

    [Fact]
    public void S3919_an_own_switch_over_an_unread_value_waits_for_nothing()
    {
        var follow = new NightLightFollow();
        follow.Switched(true, null);
        Assert.False(follow.Shown(NightLightValue.Read(Off), handler: true));
    }

    // ---- readings in order ---------------------------------------------------------------------

    /// <summary>Two outside changes in a row: the read started first finished last and put the old state back.</summary>
    [Fact]
    public void S3920_a_read_that_started_earlier_and_finished_later_is_dropped()
    {
        var order = new ReadingOrder();
        var first = order.Begin();
        var second = order.Begin();

        Assert.True(order.Accept(second));
        Assert.False(order.Accept(first));
    }

    [Fact]
    public void S3920_reads_finishing_in_order_are_all_shown()
    {
        var order = new ReadingOrder();
        var first = order.Begin();
        Assert.True(order.Accept(first));
        var second = order.Begin();
        Assert.True(order.Accept(second));
        Assert.True(order.Accept(order.Begin()));
    }

    // ---- the registry watch's steps ------------------------------------------------------------

    /// <summary>
    /// The CloudStore writes the key again from scratch: it went (the re-ask is refused) and came
    /// back before the parent was armed. Watching the parent then hears nothing more, ever.
    /// </summary>
    [Fact]
    public void S3921_a_key_back_before_its_parent_is_armed_is_watched_again()
    {
        var keys = new Keys { Armed = KeyWatched.Key, Key = false };
        keys.BeforeParentArmed = () => keys.Key = true;

        var (at, tell) = KeyFollow.Heard(KeyWatched.Key, keys);

        Assert.Equal(KeyWatched.Key, at);
        Assert.Equal(KeyWatched.Key, keys.Armed);
        Assert.True(tell);
    }

    [Fact]
    public void S3921_a_key_appearing_while_the_watch_opens_is_watched()
    {
        var keys = new Keys { Key = false };
        keys.BeforeParentArmed = () => keys.Key = true;

        Assert.Equal(KeyWatched.Key, KeyFollow.Open(keys));
        Assert.Equal(KeyWatched.Key, keys.Armed);
    }

    /// <summary>The parent signalled, the key was not there yet, and it appeared before the parent was asked again.</summary>
    [Fact]
    public void S3921_a_key_appearing_before_the_parent_is_asked_again_is_watched()
    {
        var keys = new Keys { Armed = KeyWatched.Parent, Key = false };
        keys.BeforeAskAgain = () => keys.Key = true;

        var (at, tell) = KeyFollow.Heard(KeyWatched.Parent, keys);

        Assert.Equal(KeyWatched.Key, at);
        Assert.True(tell);
    }

    [Fact]
    public void S3921_a_change_of_the_key_re_arms_it_and_tells()
    {
        var keys = new Keys { Armed = KeyWatched.Key };
        Assert.Equal((KeyWatched.Key, true), KeyFollow.Heard(KeyWatched.Key, keys));
        Assert.Equal(1, keys.Asks);
    }

    [Fact]
    public void S3921_the_parent_waits_on_while_the_key_is_not_there()
    {
        var keys = new Keys { Armed = KeyWatched.Parent, Key = false };
        Assert.Equal((KeyWatched.Parent, false), KeyFollow.Heard(KeyWatched.Parent, keys));
    }

    [Fact]
    public void S3921_a_key_gone_for_good_is_waited_for_at_its_parent()
    {
        var keys = new Keys { Armed = KeyWatched.Key, Key = false };
        Assert.Equal((KeyWatched.Parent, true), KeyFollow.Heard(KeyWatched.Key, keys));
        Assert.Equal(KeyWatched.Nothing, KeyFollow.Open(new Keys { Key = false, Parent = false }));
    }

    /// <summary>A registry with one key and its parent; hooks run where a race would land.</summary>
    private sealed class Keys : IKeyArms
    {
        public bool Key { get; set; } = true;

        public bool Parent { get; set; } = true;

        public KeyWatched Armed { get; set; } = KeyWatched.Nothing;

        public int Asks { get; private set; }

        public Action? BeforeParentArmed { get; set; }

        public Action? BeforeAskAgain { get; set; }

        public bool ArmKey()
        {
            if (!Key)
            {
                return false;
            }

            Armed = KeyWatched.Key;
            return true;
        }

        public bool ArmParent()
        {
            BeforeParentArmed?.Invoke();
            BeforeParentArmed = null;
            if (!Parent)
            {
                return false;
            }

            Armed = KeyWatched.Parent;
            return true;
        }

        public bool AskAgain()
        {
            BeforeAskAgain?.Invoke();
            BeforeAskAgain = null;
            Asks++;
            return Armed switch
            {
                KeyWatched.Key => Key,
                KeyWatched.Parent => Parent,
                _ => false,
            };
        }
    }
}
