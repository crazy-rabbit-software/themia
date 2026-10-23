using Themia.Core.Localization;

namespace Themia.Core.Settings;

/// <summary>
/// [S-1072] The addresses About shows, the same ones the Store listing gives: Lometics' privacy
/// policy and Themia's support mailbox.
/// </summary>
public static class AboutLinks
{
    /// <summary>The support mailbox, as About shows it.</summary>
    public const string SupportAddress = "support@themia.app";

    /// <summary>A mail to <see cref="SupportAddress"/>.</summary>
    public static Uri Support { get; } = new("mailto:" + SupportAddress);

    /// <summary>The privacy policy in German for a German interface, else in English: the two languages it is written in.</summary>
    public static Uri Privacy(Locale locale)
        => new(locale == Locale.De ? "https://lometics.com/de/legal/privacy/" : "https://lometics.com/en/legal/privacy/");
}
