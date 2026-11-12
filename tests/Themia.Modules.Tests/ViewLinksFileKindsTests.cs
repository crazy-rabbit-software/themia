using Themia.Core.Links;
using Themia.Modules.Tests.Modules;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-11945] The file under the pointer: who points at one and who lights for one.</summary>
public sealed class ViewLinksFileKindsTests
{
    [Theory]
    [InlineData("folder")]
    [InlineData("recentFiles")]
    [InlineData("shortcuts")]
    [InlineData("desktopItem")]
    public void S11945_a_folder_entry_a_recent_file_a_shortcut_and_a_desktop_item_point_at_a_file_and_light_for_one(string id)
    {
        var links = ModuleRegistry.Widgets.Single(m => m.Descriptor.Id.Value == id).Descriptor.Links;

        Assert.Contains(LinkTopicId.HoveredFile, links.Publishes);
        Assert.Contains(LinkTopicId.HoveredFile, links.Follows);
    }
}
