using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5500] With Explorer parity in, a new state shows Themia's items.</summary>
public sealed class DesktopDefaultTests
{
    [Fact]
    public void S5500_a_new_state_shows_themias_items()
    {
        Assert.Equal(DesktopShows.ThemiaItems, new AppPreferences().Desktop);
        Assert.Equal(DesktopShows.ThemiaItems, new AppState().Preferences.Desktop);
    }
}
