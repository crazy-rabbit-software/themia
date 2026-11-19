using Themia.Core.Links;
using Themia.Modules.Tests.Modules;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-11940] [S-11965] [S-11966] A part is keyed by what it is, never by what it carries: a contact is a person, a mail
/// is its message, an event its event; no mail, meeting, task or day lights for a person, and no event or task for a day.
/// </summary>
public sealed class ViewLinksPersonKindsTests
{
    private static LinkUse Links(string id) => ModuleRegistry.Widgets.Single(m => m.Descriptor.Id.Value == id).Descriptor.Links;

    [Fact]
    public void S11940_a_contact_alone_lights_for_a_person_and_the_next_meetings_organiser_line_points_at_one()
    {
        var followers = ModuleRegistry.Widgets
            .Where(m => m.Descriptor.Links.Follows.Contains(LinkTopicId.HoveredPerson))
            .Select(m => m.Descriptor.Id.Value);
        var publishers = ModuleRegistry.Widgets
            .Where(m => m.Descriptor.Links.Publishes.Contains(LinkTopicId.HoveredPerson))
            .Select(m => m.Descriptor.Id.Value)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["contacts"], followers);
        Assert.Equal(["contacts", "nextMeeting"], publishers);
    }

    [Fact]
    public void S11966_a_mail_is_its_message_never_its_sender()
    {
        Assert.Equal(LinkUse.Both(LinkTopicId.HoveredMessage).Topics, Links("email").Topics);
    }

    [Theory]
    [InlineData("upNext")]
    [InlineData("nextMeeting")]
    [InlineData("calendar")]
    [InlineData("weekPlanner")]
    public void S11965_an_event_is_its_event_never_its_organiser(string id)
    {
        var links = Links(id);

        Assert.Contains(LinkTopicId.HoveredEvent, links.Publishes);
        Assert.Contains(LinkTopicId.HoveredEvent, links.Follows);
        Assert.DoesNotContain(LinkTopicId.HoveredPerson, links.Follows);
    }

    [Fact]
    public void S11965_a_meeting_list_and_a_task_list_light_for_no_day()
    {
        Assert.DoesNotContain(LinkTopicId.HoveredDate, Links("upNext").Topics);
        Assert.Empty(Links("tasks").Topics);
    }
}
