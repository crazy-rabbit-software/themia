using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Office;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11940] [S-11965] Contacts' place on its view's links: a person's item is that person (<see cref="LinkKeys.Person"/> of
/// their address, else their name). The pointer on it, or the keyboard cursor ([S-3307]), publishes them
/// (<see cref="LinkTopics.HoveredPerson"/>); while a person is pointed at elsewhere on the view (another contacts widget), the
/// item that is them takes the item layer's hover fill, in place. A mail they sent or a meeting they organise is no person
/// and lights nothing here.
/// </summary>
internal sealed partial class ContactsView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<PersonLink>? _people;

    private LinkLights<PersonLink> People => _people ??= new LinkLights<PersonLink>(_links, LinkTopics.HoveredPerson);

    private void LinkRow(FrameworkElement row, Contact contact) => People.Row(row, PersonOf(contact));

    private static PersonLink? PersonOf(Contact contact) => LinkKeys.PersonLink(contact.Email, contact.Name);
}
