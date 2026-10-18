using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-3900] Which kinds are quick-setting toggles, over every widget of the build (the power and
/// Bluetooth widgets are module projects, which only this host has). The rest of the toggles'
/// tests are Core's QuickToggleTests.
/// </summary>
public sealed class QuickToggleHostTests
{
    [Fact]
    public void S3900_a_power_or_meter_kind_is_not_a_toggle()
    {
        Assert.Null(QuickToggleRules.ForKind(new WidgetId("shutdown")));
        Assert.Null(QuickToggleRules.ForKind(new WidgetId("bluetooth")));
        Assert.Null(QuickToggleRules.ForKind(VolumeConfig.Descriptor.Id));
    }
}
