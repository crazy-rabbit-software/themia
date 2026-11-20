using Themia.Core.Localization;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1106] Every locale has its own formatting data: a locale without any is a bug, never quietly English.</summary>
public sealed class LocaleDataTests
{
    [Fact]
    public void S1106_every_locale_has_its_own_data()
    {
        var data = Enum.GetValues<Locale>().Select(LocaleData.For).ToList();

        Assert.Equal(Enum.GetValues<Locale>().Length, data.Distinct().Count());
    }

    [Fact]
    public void S1106_an_unknown_locale_is_refused_not_read_as_english()
        => Assert.Throws<ArgumentOutOfRangeException>(() => LocaleData.For((Locale)(-1)));
}
