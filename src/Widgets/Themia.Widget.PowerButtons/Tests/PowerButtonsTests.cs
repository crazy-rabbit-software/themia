using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.PowerButtons.Tests;

/// <summary>[S-6901] Each kind of the family stands for its one action, from its own config.</summary>
public sealed class PowerButtonsTests
{
    /// <summary>[S-7400] The Shutdown power button stands for its one action, from its own config.</summary>
    [Fact]
    public void S7400_its_kind_stands_for_its_action_and_arms_first()
    {
        var kind = new WidgetId("shutdown");
        var config = Assert.IsType<ShutdownConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.Shutdown, config.Action);
        Assert.Equal(PowerAction.Shutdown, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.Shutdown));
        Assert.True(PowerActionRules.NeedsArming(PowerAction.Shutdown));
    }

    /// <summary>[S-7500] The Restart power button stands for its one action, from its own config.</summary>
    [Fact]
    public void S7500_its_kind_stands_for_its_action_and_arms_first()
    {
        var kind = new WidgetId("restart");
        var config = Assert.IsType<RestartConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.Restart, config.Action);
        Assert.Equal(PowerAction.Restart, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.Restart));
        Assert.True(PowerActionRules.NeedsArming(PowerAction.Restart));
    }

    /// <summary>[S-7600] The Sleep power button stands for its one action, from its own config.</summary>
    [Fact]
    public void S7600_its_kind_stands_for_its_action_and_runs_at_once()
    {
        var kind = new WidgetId("sleep");
        var config = Assert.IsType<SleepConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.Sleep, config.Action);
        Assert.Equal(PowerAction.Sleep, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.Sleep));
        Assert.False(PowerActionRules.NeedsArming(PowerAction.Sleep));
    }

    /// <summary>[S-7700] The Hibernate power button stands for its one action, from its own config.</summary>
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

    /// <summary>[S-7800] The Lock power button stands for its one action, from its own config.</summary>
    [Fact]
    public void S7800_its_kind_stands_for_its_action_and_runs_at_once()
    {
        var kind = new WidgetId("lock");
        var config = Assert.IsType<LockConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.Lock, config.Action);
        Assert.Equal(PowerAction.Lock, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.Lock));
        Assert.False(PowerActionRules.NeedsArming(PowerAction.Lock));
    }

    /// <summary>[S-7900] The SignOut power button stands for its one action, from its own config.</summary>
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

    /// <summary>[S-8000] The ScreenOff power button stands for its one action, from its own config.</summary>
    [Fact]
    public void S8000_its_kind_stands_for_its_action_and_runs_at_once()
    {
        var kind = new WidgetId("screenOff");
        var config = Assert.IsType<ScreenOffConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.ScreenOff, config.Action);
        Assert.Equal(PowerAction.ScreenOff, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.ScreenOff));
        Assert.False(PowerActionRules.NeedsArming(PowerAction.ScreenOff));
    }
}
