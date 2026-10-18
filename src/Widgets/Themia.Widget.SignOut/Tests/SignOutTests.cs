using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.SignOut.Tests;

/// <summary>[S-7900] The SignOut power button stands for its one action, from its own config.</summary>
public sealed class SignOutTests
{
    [Fact]
    public void S7900_its_kind_stands_for_its_action_and_arms_first()
    {
        var kind = new WidgetId("signOut");
        var config = Assert.IsType<SignOutConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.SignOut, config.Action);
        Assert.Equal(PowerAction.SignOut, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.SignOut));
        Assert.True(PowerActionRules.NeedsArming(PowerAction.SignOut));
    }
}
