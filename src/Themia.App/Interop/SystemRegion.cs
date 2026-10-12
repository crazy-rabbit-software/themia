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

    private static bool _countryWarned;

    /// <summary>
    /// [S-5143] The Windows region (Settings › Time &amp; language › Region) as an ISO 3166 code, for
    /// the exchange-rate widget's base currency; empty when Windows does not say (a UN M.49 area code
    /// is not a country and reads as none).
    /// </summary>
    public static unsafe string Country()
    {
        const int Capacity = 16;
        var buffer = stackalloc char[Capacity];

        var written = GetUserDefaultGeoName(buffer, Capacity);
        if (written <= 1)
        {
            // Read on every state change: said once, not once per drag frame.
            if (!_countryWarned)
            {
                _countryWarned = true;
                Log.Warn(Scope, $"could not read the Windows region (error {Marshal.GetLastPInvokeError()}); the euro is the default base");
            }

            return string.Empty;
        }

        var name = new string(buffer, 0, written - 1);
        return name.Length == 2 && char.IsAsciiLetter(name[0]) && char.IsAsciiLetter(name[1]) ? name : string.Empty;
    }

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

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static unsafe partial int GetUserDefaultGeoName(char* geoName, int capacity);
}
