using Themia.Core.Links;
using Themia.Modules.Tests.Modules;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-11930] The app under the pointer beyond screen time: who lights it, and that only screen time picks.</summary>
public sealed class ViewLinksAppKindsTests
{
    [Theory]
    [InlineData("topProcesses")]
    [InlineData("mixer")]
    [InlineData("notifications")]
    [InlineData("shortcuts")]
    public void S11930_a_list_of_apps_publishes_the_hover_and_shows_the_pick_without_setting_it(string id)
    {
        var links = ModuleRegistry.Widgets.Single(m => m.Descriptor.Id.Value == id).Descriptor.Links;

        Assert.Contains(LinkTopicId.HoveredApp, links.Publishes);
        Assert.DoesNotContain(LinkTopicId.SelectedApp, links.Publishes);
        Assert.Contains(LinkTopicId.HoveredApp, links.Follows);
        Assert.Contains(LinkTopicId.SelectedApp, links.Follows);
    }

    [Fact]
    public void S11930_only_the_screen_time_widgets_pick_an_app()
    {
        var pickers = ModuleRegistry.Widgets
            .Where(m => m.Descriptor.Links.Publishes.Contains(LinkTopicId.SelectedApp))
            .Select(m => m.Descriptor.Id.Value)
            .ToList();

        Assert.NotEmpty(pickers);
        Assert.All(pickers, id => Assert.StartsWith("screenTime", id, StringComparison.Ordinal));
    }
}
