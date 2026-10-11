using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;

namespace Themia.App.Interop;

/// <summary>
/// [S-3772] Windows' regional format, read AOT-safely through <c>GetLocaleInfoEx</c> for the user's
/// default locale: no <c>CultureInfo</c>, which <c>InvariantGlobalization</c> would answer with the
/// invariant culture. Read again on every call — it is three cheap kernel calls, and the user can
/// change the format in Windows Settings while Themia runs.
/// </summary>
internal static partial class SystemRegion
{
    private const string Scope = "i18n";

    private const uint LocaleSShortTime = 0x79;
    private const uint LocaleIFirstDayOfWeek = 0x100C;
    private const uint LocaleIMeasure = 0x0D;

    /// <summary>The user's regional format; anything unreadable falls back to <paramref name="fallback"/>.</summary>
    public static RegionValues Read(RegionValues fallback)
        => RegionFormat.FromWindows(Text(LocaleSShortTime), Number(LocaleIFirstDayOfWeek), Number(LocaleIMeasure), fallback);

    private static int? Number(uint type)
        => int.TryParse(Text(type), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;

    private static unsafe string? Text(uint type)
    {
        const int Capacity = 80;
        var buffer = stackalloc char[Capacity];

        // A null locale name is LOCALE_NAME_USER_DEFAULT.
        var written = GetLocaleInfoEx(null, type, buffer, Capacity);
        if (written <= 0)
        {
            Log.Warn(Scope, $"could not read regional setting 0x{type:X} (error {Marshal.GetLastPInvokeError()}); using the language's");
            return null;
        }

        // The count includes the terminating null.
        return new string(buffer, 0, written - 1);
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial int GetLocaleInfoEx(string? localeName, uint lcType, char* data, int capacity);
}
