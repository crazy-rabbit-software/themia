using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Office;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11940] [S-11965] Contacts' place on its view's links: a person's row (or cell) is that person
/// (<see cref="LinkKeys.Person"/> of their address, else their name). The pointer on it, or the selection the
/// keys move ([S-3307]), publishes them (<see cref="LinkTopics.HoveredPerson"/>); while a person is pointed at
/// elsewhere on the view (another contacts widget's row, the next meeting's organiser line), the row that is them
/// takes the item layer's hover fill, in place. A mail they sent or a meeting they organise is no person and lights
/// nothing here.
/// </summary>
internal sealed partial class ContactsView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<PersonLink>? _people;

    private LinkLights<PersonLink> People => _people ??= new LinkLights<PersonLink>(_links, LinkTopics.HoveredPerson);

    private void LinkRow(FrameworkElement row, Contact contact) => People.Row(row, PersonOf(contact));

    /// <summary>[S-3307] The keys moved the selection: its person is the hover; none when it let go.</summary>
    private void PointSelected()
        => People.Point(_visible.FirstOrDefault(c => c.Id == _selectedId) is { } contact ? PersonOf(contact) : null);

    private static PersonLink? PersonOf(Contact contact) => LinkKeys.PersonLink(contact.Email, contact.Name);
}
