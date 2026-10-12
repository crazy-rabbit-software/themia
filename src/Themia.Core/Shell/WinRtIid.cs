using System.Security.Cryptography;
using System.Text;

namespace Themia.Core.Shell;

/// <summary>
/// [S-4425] The IID of a parameterised WinRT interface or delegate (<c>TypedEventHandler&lt;A, B&gt;</c>),
/// computed as the WinRT type system defines it: a version-5 UUID, SHA-1 over the WinRT
/// parameterised-interface namespace and the UTF-8 type signature. Needed where Themia talks to a WinRT
/// class its SDK projection does not have (Windows' focus sessions, Windows 11) through raw vtables, so
/// the delegate it hands over answers to the IID Windows asks for.
/// </summary>
public static class WinRtIid
{
    /// <summary>The namespace every parameterised WinRT IID is made in: {11f47ad5-7b73-42c0-abae-878b1e16adee}.</summary>
    private static readonly byte[] Namespace =
        [0x11, 0xf4, 0x7a, 0xd5, 0x7b, 0x73, 0x42, 0xc0, 0xab, 0xae, 0x87, 0x8b, 0x1e, 0x16, 0xad, 0xee];

    /// <summary>The IID for <paramref name="signature"/>, e.g. <c>pinterface({faa585ea-6214-4217-afda-7f46de5869b3};string)</c>.</summary>
    public static Guid Of(string signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        var text = Encoding.UTF8.GetBytes(signature);
        var data = new byte[Namespace.Length + text.Length];
        Namespace.CopyTo(data, 0);
        text.CopyTo(data, Namespace.Length);

        // SHA-1 is what the WinRT type system fixes here, not a choice of hash for security.
#pragma warning disable CA5350
        var hash = SHA1.HashData(data);
#pragma warning restore CA5350

        // The first 16 bytes, big-endian fields, version 5 and the RFC 4122 variant.
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    /// <summary>The signature of a <c>TypedEventHandler&lt;TSender, Object&gt;</c> whose sender is a runtime class.</summary>
    public static string TypedEventHandlerOfClass(string runtimeClass, Guid defaultInterface)
        => $"pinterface({{9de1c534-6ae1-11e0-84e1-18a905bcc53f}};rc({runtimeClass};{{{defaultInterface:D}}});cinterface(IInspectable))";
}
