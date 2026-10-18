using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-1211] The screen-time widgets' configs under the root's rules (the app list's ceiling, 0 meaning as many as fit): the configs are their modules', so they run here. Moved from Core's ScreenTimeRulesTests, unchanged in what they assert.</summary>
public sealed class ScreenTimeHostTests
{
    [Fact]
    public void S1211_the_setting_is_a_ceiling_and_0_means_as_many_as_fit()
    {
        Assert.Equal(ScreenTimeRules.MaxAppsLimit, ScreenTimeRules.Ceiling(0));
        Assert.Equal(ScreenTimeRules.MaxAppsLimit, ScreenTimeRules.Ceiling(-1));
        Assert.Equal(3, ScreenTimeRules.Ceiling(3));
        Assert.Equal(ScreenTimeRules.MaxAppsLimit, ScreenTimeRules.Ceiling(500));
        Assert.Equal(0, ScreenTimeRules.EffectiveMaxApps(-4));
        Assert.Equal(0, new ScreenTimeAppsConfig().MaxApps);
    }
}
