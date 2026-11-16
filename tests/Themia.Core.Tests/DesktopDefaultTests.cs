using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5500] [S-5594] [S-5595] The desktop's icons always show and always stand on the icon grid; Auto arrange
/// icons is a one-shot command: none of them is a preference.
/// </summary>
public sealed class DesktopDefaultTests
{
    [Fact]
    public void S5500_S5594_S5595_the_preferences_hold_no_show_auto_arrange_or_align_setting()
    {
        var json = JsonSerializer.Serialize(new AppPreferences(), AppStateJsonContext.Default.AppPreferences);

        Assert.DoesNotContain("showDesktopIcons", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("autoArrange", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alignToGrid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(typeof(AppPreferences).GetProperties(), p => p.Name is "ShowDesktopIcons" or "DesktopAutoArrange" or "DesktopAlignToGrid");
    }
}