using Themia.Core.Links;
using Themia.Modules.Tests.Modules;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-11940] The person under the pointer: who points at one and who lights for one.</summary>
public sealed class ViewLinksPersonKindsTests
{
    [Theory]
    [InlineData("email")]
    [InlineData("contacts")]
    [InlineData("nextMeeting")]
    [InlineData("upNext")]
    [InlineData("calendar")]
    public void S11940_a_sender_a_contact_and_an_organiser_point_at_a_person_and_light_for_one(string id)
    {
        var links = ModuleRegistry.Widgets.Single(m => m.Descriptor.Id.Value == id).Descriptor.Links;

        Assert.Contains(LinkTopicId.HoveredPerson, links.Publishes);
        Assert.Contains(LinkTopicId.HoveredPerson, links.Follows);
    }
}
