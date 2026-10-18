using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-4863] The bin's Empty rule (the SDK's RecycleBinState), which the widget's button and the Shortcuts menu both read. Moved from ShortcutsTests, which went with the Shortcuts widget, unchanged.</summary>
public sealed class RecycleBinStateTests
{
    [Fact]
    public void S4863_empty_is_offered_only_with_items_and_not_twice_at_once()
    {
        Assert.True(RecycleBinState.CanEmpty(new RecycleBinState(3, 100), emptying: false));
        Assert.False(RecycleBinState.CanEmpty(new RecycleBinState(3, 100), emptying: true));
        Assert.False(RecycleBinState.CanEmpty(new RecycleBinState(0, 0), emptying: false));
        Assert.False(RecycleBinState.CanEmpty(null, emptying: false));
    }
}
