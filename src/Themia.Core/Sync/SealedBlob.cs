using System.Security.Cryptography;
using System.Text;

namespace Themia.Core.Sync;

/// <summary>How opening a sealed shelf went.</summary>
public enum SealOutcome
{
    Opened,

    /// <summary>Not a blob this build writes: too short, or the wrong magic.</summary>
    NotSealed,

    /// <summary>
    /// The right shape, but it does not authenticate under this key and context: damaged on disk,
    /// sealed with another key (another Windows user or PC), or a file copied over another shelf.
    /// </summary>
    Tampered,
}

/// <summary>
/// [S-2602] The offline sync shelves' encryption: AES-256-GCM under one random key per install, the
/// key itself sealed with DPAPI for the current Windows user (the app's <c>VaultKey</c>).
/// <para>
/// Why not DPAPI on every shelf: a shelf is rewritten after every sync, and a calendar shelf can be
/// megabytes. AES-GCM on a key held in memory is one pass with no per-call key derivation, and the
/// whole format lives here, in Core, where it is tested without Windows.
/// </para>
/// <para>
/// Layout: <c>"TSB1"</c> · 12-byte nonce · 16-byte tag · ciphertext. The associated data is the
/// shelf's context (<c>kind|owner</c>), so a shelf file copied over another account's opens as
/// <see cref="SealOutcome.Tampered"/>, never as the other account's mail. A fresh random nonce per
/// seal: with one key and at most a few thousand writes a day, a 96-bit random nonce does not repeat.
/// </para>
/// </summary>
public static class SealedBlob
{
    public const int KeySize = 32;

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static ReadOnlySpan<byte> Magic => "TSB1"u8;

    private static int HeaderSize => Magic.Length + NonceSize + TagSize;

    /// <summary>A fresh random key.</summary>
    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeySize);

    public static byte[] Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, string context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (key.Length != KeySize)
        {
            throw new ArgumentException($"a shelf key is {KeySize} bytes", nameof(key));
        }

        var output = new byte[HeaderSize + plaintext.Length];
        Magic.CopyTo(output);

        var nonce = output.AsSpan(Magic.Length, NonceSize);
        var tag = output.AsSpan(Magic.Length + NonceSize, TagSize);
        var cipher = output.AsSpan(HeaderSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, cipher, tag, Encoding.UTF8.GetBytes(context));
        return output;
    }

    /// <summary>Open a sealed blob. Never throws for bad input: a damaged file is an outcome.</summary>
    public static SealOutcome TryOpen(ReadOnlySpan<byte> key, ReadOnlySpan<byte> blob, string context, out byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(context);
        plaintext = [];

        if (key.Length != KeySize)
        {
            throw new ArgumentException($"a shelf key is {KeySize} bytes", nameof(key));
        }

        if (blob.Length < HeaderSize || !blob[..Magic.Length].SequenceEqual(Magic))
        {
            return SealOutcome.NotSealed;
        }

        var nonce = blob.Slice(Magic.Length, NonceSize);
        var tag = blob.Slice(Magic.Length + NonceSize, TagSize);
        var cipher = blob[HeaderSize..];
        var output = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, output, Encoding.UTF8.GetBytes(context));
        }
        catch (AuthenticationTagMismatchException)
        {
            CryptographicOperations.ZeroMemory(output);
            return SealOutcome.Tampered;
        }

        plaintext = output;
        return SealOutcome.Opened;
    }
}
