using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Drive.Tests;

/// <summary>[S-4675] The drive card. Moved with the widget from Core's DriveRulesTests, unchanged in what it asserts.</summary>
public sealed class DriveTests
{
    [Fact]
    public void S4675_a_new_drive_card_starts_without_a_header()
        => Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.DefaultHeader(new WidgetId("drive")));
}
