using Themia.Core.Localization;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1072] About's privacy and support links.</summary>
public sealed class AboutLinksTests
{
    [Fact]
    public void S1072_the_privacy_policy_is_german_for_german_and_english_for_every_other_language()
    {
        Assert.Equal("https://lometics.com/de/legal/privacy/", AboutLinks.Privacy(Locale.De).AbsoluteUri);
        foreach (var locale in Enum.GetValues<Locale>().Where(l => l != Locale.De))
        {
            Assert.Equal("https://lometics.com/en/legal/privacy/", AboutLinks.Privacy(locale).AbsoluteUri);
        }
    }

    [Fact]
    public void S1072_support_is_a_mail_to_the_support_address()
    {
        Assert.Equal("mailto:support@themia.app", AboutLinks.Support.OriginalString);
    }
}
