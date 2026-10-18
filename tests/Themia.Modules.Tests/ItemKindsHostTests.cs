using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;
using System.Text.RegularExpressions;
using Themia.Core.Geometry;
using Themia.Tests;

namespace Themia.Modules.Tests;

/// <summary>[S-6570] Which widgets have items, asked of the catalogue with every module of the build installed. Moved from Core's ItemKindsTests, unchanged in what it asserts.</summary>
public sealed class ItemKindsHostTests
{

    [Fact]
    public void S6570_lists_and_meters_have_items_and_a_plain_readout_does_not()
    {
        Assert.True(ItemKinds.Has(TasksConfig.Descriptor.Id));
        Assert.True(ItemKinds.Has(new WidgetId("cpu")));
        Assert.True(ItemKinds.Has(ClipboardConfig.Descriptor.Id));
        Assert.False(ItemKinds.Has(AlarmConfig.Descriptor.Id));
        Assert.False(ItemKinds.Has(GalleryConfig.Descriptor.Id));
    }
}
