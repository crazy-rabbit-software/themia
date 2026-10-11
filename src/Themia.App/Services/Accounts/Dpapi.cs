using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Themia.Core.Accounts;

namespace Themia.App.Services.Accounts;

/// <summary>How opening a stored secret went.</summary>
internal enum SecretState
{
    /// <summary>Nothing was stored.</summary>
    Absent,

    /// <summary>Opened; the value is in hand.</summary>
    Open,

    /// <summary>
    /// Stored, but not openable here: sealed for another Windows user or another PC, or damaged.
    /// Only signing in again mends it; the account is kept and says so.
    /// </summary>
    Unreadable,

    /// <summary>Stored as plain text: never written by this app, so it is refused.</summary>
    Plaintext,
}

/// <summary>A secret, opened or not.</summary>
internal readonly record struct OpenedSecret(SecretState State, string? Value);

/// <summary>
/// [S-525] Secrets at rest: DPAPI for the current Windows user (<c>CryptProtectData</c>), in the
/// <c>dpapi:v1:</c> envelope.
/// <para>
/// CurrentUser scope with no extra entropy. The bytes are tied to this user on this PC:
/// <c>accounts.json</c> copied elsewhere, or read by another account here, gives up nothing.
/// </para>
/// <para>
/// Sealing never falls back to plain text: that is how a token ends up on disk in the clear. A
/// failure here throws and nothing is written.
/// </para>
/// </summary>
internal static partial class Dpapi
{
    /// <summary>Never show UI: these calls run from background refreshes.</summary>
    private const uint CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public uint Size;
        public nint Data;
    }

    [LibraryImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptProtectData(
        in DataBlob dataIn,
        nint description,
        nint entropy,
        nint reserved,
        nint prompt,
        uint flags,
        out DataBlob dataOut);

    [LibraryImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptUnprotectData(
        in DataBlob dataIn,
        nint description,
        nint entropy,
        nint reserved,
        nint prompt,
        uint flags,
        out DataBlob dataOut);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);

    /// <summary>Seal a secret for this user. Throws <see cref="CryptographicException"/> when Windows cannot.</summary>
    public static string Seal(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        try
        {
            return SecretEnvelope.Wrap(Transform(bytes, protect: true));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Seal when there is something to seal; null stays null.</summary>
    public static string? SealOrNull(string? plaintext) => string.IsNullOrEmpty(plaintext) ? null : Seal(plaintext);

    /// <summary>Open a sealed secret. Never throws: an unreadable secret is a state, not a crash.</summary>
    public static OpenedSecret Open(string? stored)
    {
        switch (SecretEnvelope.TryUnwrap(stored, out var blob))
        {
            case EnvelopeKind.Absent:
                return new OpenedSecret(SecretState.Absent, null);
            case EnvelopeKind.Plaintext:
                return new OpenedSecret(SecretState.Plaintext, stored);
            case EnvelopeKind.Damaged:
                return new OpenedSecret(SecretState.Unreadable, null);
        }

        try
        {
            var plain = Transform(blob, protect: false);
            try
            {
                return new OpenedSecret(SecretState.Open, Encoding.UTF8.GetString(plain));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
        catch (CryptographicException)
        {
            return new OpenedSecret(SecretState.Unreadable, null);
        }
    }

    /// <summary>
    /// [S-2603] Seal raw bytes (the sync shelves' key) for this user. Throws
    /// <see cref="CryptographicException"/> when Windows cannot; never falls back to plain bytes.
    /// </summary>
    public static byte[] SealBytes(byte[] plain)
    {
        ArgumentNullException.ThrowIfNull(plain);
        return Transform(plain, protect: true);
    }

    /// <summary>[S-2603] Open bytes sealed by <see cref="SealBytes"/>; null when they will not open here.</summary>
    public static byte[]? OpenBytes(byte[] blob)
    {
        ArgumentNullException.ThrowIfNull(blob);

        try
        {
            return Transform(blob, protect: false);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static unsafe byte[] Transform(byte[] input, bool protect)
    {
        fixed (byte* data = input)
        {
            var inBlob = new DataBlob { Size = (uint)input.Length, Data = (nint)data };
            DataBlob outBlob = default;

            try
            {
                var ok = protect
                    ? CryptProtectData(in inBlob, 0, 0, 0, 0, CryptProtectUiForbidden, out outBlob)
                    : CryptUnprotectData(in inBlob, 0, 0, 0, 0, CryptProtectUiForbidden, out outBlob);

                if (!ok || outBlob.Data == 0)
                {
                    throw new CryptographicException(Marshal.GetLastPInvokeError());
                }

                var result = new byte[outBlob.Size];
                Marshal.Copy(outBlob.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                // The output buffer is LocalAlloc'd by DPAPI and is ours to free on every path,
                // and it may hold the plaintext: wipe it first.
                if (outBlob.Data != 0)
                {
                    new Span<byte>((void*)outBlob.Data, (int)outBlob.Size).Clear();
                    _ = LocalFree(outBlob.Data);
                }
            }
        }
    }
}
