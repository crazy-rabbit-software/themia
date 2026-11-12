using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11940] [S-11942] Mail's place on its view's links: a row is its sender (<see cref="LinkKeys.Person"/>
/// of the address, else the name). The pointer on a row publishes the sender
/// (<see cref="LinkTopics.HoveredPerson"/>); while a person is pointed at anywhere on the view, the rows
/// they sent take the item layer's hover fill, lit in place on the rows the kit's list drew.
/// </summary>
internal sealed partial class EmailView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<PersonLink>? _people;

    private LinkLights<PersonLink> People => _people ??= new LinkLights<PersonLink>(_links, LinkTopics.HoveredPerson);

    /// <summary>A row the kit's list drew: its message's sender.</summary>
    private void LinkRow(KitItem item, FrameworkElement row)
    {
        if (_source?.Message(item.Id) is { } message)
        {
            People.Row(row, LinkKeys.PersonLink(message.FromAddress, message.FromName));
        }
    }

    /// <summary>The kit's list draws its rows again: the old ones are forgotten.</summary>
    private void ForgetRows() => People.Clear();
}
