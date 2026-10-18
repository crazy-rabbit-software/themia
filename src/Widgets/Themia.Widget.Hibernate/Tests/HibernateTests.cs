using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Hibernate.Tests;

/// <summary>[S-7700] The Hibernate power button stands for its one action, from its own config.</summary>
public sealed class HibernateTests
{
    [Fact]
    public void S7700_its_kind_stands_for_its_action_and_arms_first()
    {
        var kind = new WidgetId("hibernate");
        var config = Assert.IsType<HibernateConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.Hibernate, config.Action);
        Assert.Equal(PowerAction.Hibernate, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.Hibernate));
        Assert.True(PowerActionRules.NeedsArming(PowerAction.Hibernate));
    }
}
