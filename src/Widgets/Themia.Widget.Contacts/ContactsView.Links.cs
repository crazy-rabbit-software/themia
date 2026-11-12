using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Office;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11940] Contacts' place on its view's links: a person's row (or cell) is that person
/// (<see cref="LinkKeys.Person"/> of their address, else their name). The pointer on it, or the selection the
/// keys move ([S-3307]), publishes them (<see cref="LinkTopics.HoveredPerson"/>); while a person is pointed at
/// anywhere on the view (a mail's sender, a meeting's organiser), their row takes the item layer's hover fill,
/// in place.
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
