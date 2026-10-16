namespace Themia.Core.Widgets;

/// <summary>
/// [S-3919] Night light's state value, where Windows keeps it: the account's CloudStore value
/// <c>HKCU\…\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate\
/// windows.data.bluelightreduction.bluelightreductionstate</c> "Data", Microsoft Bond compact binary v1,
/// read as a structure (as Karpo's <c>NightLightBlob</c> and Auto Dark Mode read it), never at fixed
/// offsets, and only read: Themia never writes it.
/// <code>
///   43 42 01 00                      marshalled compact binary, version 1
///   0A 02 01 00                      field 0: a struct { field 0: true }
///   2A                               field 1: a struct {
///     06 &lt;varint&gt;                     field 0: when it was written, in Unix seconds
///     2A 2B 0E &lt;varint n&gt; &lt;n bytes&gt;  field 1: a struct { field 1: the state, n bytes }
///   00 00 00                         }}}
///
///   the state, marshalled again:
///   43 42 01 00
///   10 00                            field 0 (int32), only while night light is on
///   D0 0A &lt;varint&gt;                  field 10 (int32), on some PCs and not on others
///   C6 14 &lt;varint&gt;                  field 20: when it last changed, as a FILETIME
///   00
/// </code>
/// Anything that is not exactly this shape reads as nothing (null), never as off.
/// </summary>
public static class NightLightValue
{
    /// <summary>What a value says: when it was written, whether night light is on, and when it last changed.</summary>
    public readonly record struct State(ulong Written, bool On, ulong Changed, ulong? Ten = null);

    private static ReadOnlySpan<byte> Marshalled => [0x43, 0x42, 0x01, 0x00];

    private static ReadOnlySpan<byte> Header => [0x0A, 0x02, 0x01, 0x00];

    private static ReadOnlySpan<byte> Stamp => [0x2A, 0x06];

    private static ReadOnlySpan<byte> Inner => [0x2A, 0x2B, 0x0E];

    private static ReadOnlySpan<byte> Close => [0x00, 0x00, 0x00];

    private static ReadOnlySpan<byte> OnField => [0x10, 0x00];

    private static ReadOnlySpan<byte> TenField => [0xD0, 0x0A];

    private static ReadOnlySpan<byte> ChangedField => [0xC6, 0x14];

    /// <summary>What <paramref name="data"/> says, or null where it is not exactly the shape above.</summary>
    public static State? Read(ReadOnlySpan<byte> data)
    {
        var at = 0;
        if (!Take(data, ref at, Marshalled) || !Take(data, ref at, Header) || !Take(data, ref at, Stamp)
            || Varint(data, ref at) is not { } written
            || !Take(data, ref at, Inner)
            || Varint(data, ref at) is not { } length || length > (ulong)(data.Length - at))
        {
            return null;
        }

        var inner = data.Slice(at, (int)length);
        at += (int)length;
        if (!Take(data, ref at, Close) || at != data.Length)
        {
            return null;
        }

        var part = 0;
        if (!Take(inner, ref part, Marshalled))
        {
            return null;
        }

        var on = Take(inner, ref part, OnField);
        ulong? ten = null;
        if (Take(inner, ref part, TenField))
        {
            if (Varint(inner, ref part) is not { } stored)
            {
                return null;
            }

            ten = stored;
        }

        if (!Take(inner, ref part, ChangedField) || Varint(inner, ref part) is not { } changed
            || part != inner.Length - 1 || inner[part] != 0x00)
        {
            return null;
        }

        return new State(written, on, changed, ten);
    }

    private static bool Take(ReadOnlySpan<byte> data, ref int at, ReadOnlySpan<byte> expected)
    {
        if (data.Length - at < expected.Length || !data.Slice(at, expected.Length).SequenceEqual(expected))
        {
            return false;
        }

        at += expected.Length;
        return true;
    }

    /// <summary>A little-endian base-128 number, as compact binary writes every integer; null where it runs off the end.</summary>
    private static ulong? Varint(ReadOnlySpan<byte> data, ref int at)
    {
        ulong value = 0;
        for (var shift = 0; shift < 64 && at < data.Length; shift += 7)
        {
            var next = data[at++];
            value |= (ulong)(next & 0x7F) << shift;
            if ((next & 0x80) == 0)
            {
                return value;
            }
        }

        return null;
    }
}

/// <summary>
/// [S-3919] Which night light state the tile shows. <b>The value is where it stands</b>: the Display
/// handler's item, made once in the process, keeps answering what it last knew, so a switch made
/// elsewhere (Windows' panel, Settings, the sunset schedule) changes the value and fires the watch
/// while the handler still says the old state. The handler answers only where the value does not read.
/// <para>
/// A switch of Themia's own is written by the handler on its own time: until the value moves from
/// what it was just before the switch, the tile shows where the switch went, not the value that has
/// not caught up yet. Any other value (the handler's write, or a change made elsewhere meanwhile)
/// ends that, and the value stands again. Thread-safe.
/// </para>
/// </summary>
public sealed class NightLightFollow
{
    private readonly Lock _gate = new();
    private (bool On, NightLightValue.State Before)? _expect;

    /// <summary>
    /// Themia's own switch to <paramref name="on"/> was taken, the value having read
    /// <paramref name="before"/> just before it. Nothing to wait for where the value did not read.
    /// </summary>
    public void Switched(bool on, NightLightValue.State? before)
    {
        lock (_gate)
        {
            _expect = before is { } was ? (on, was) : null;
        }
    }

    /// <summary>What the tile shows from the value (<paramref name="stored"/>) and the handler's answer.</summary>
    public bool? Shown(NightLightValue.State? stored, bool? handler)
    {
        lock (_gate)
        {
            if (_expect is { } expect)
            {
                if (stored == expect.Before)
                {
                    return expect.On;
                }

                _expect = null;
            }

            return stored?.On ?? handler;
        }
    }
}
